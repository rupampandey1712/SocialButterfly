from concurrent import futures
import os

import grpc
from dotenv import load_dotenv
from openai import OpenAI

import genai_pb2
import genai_pb2_grpc


class GenAiService(genai_pb2_grpc.GenAiServiceServicer):
    def __init__(self):
        load_dotenv()

        self.client = OpenAI(
            base_url=os.environ["NIM_BASE_URL"],
            api_key=os.environ["NVIDIA_API_KEY"],
        )
        self.model = os.environ["NIM_MODEL"]

    def GenerateText(self, request, context):
        if not request.prompt.strip():
            context.abort(grpc.StatusCode.INVALID_ARGUMENT, "Prompt is required.")

        try:
            completion = self.client.chat.completions.create(
                model=self.model,
                messages=[{"role": "user", "content": request.prompt}],
                max_tokens=250,
            )
        except Exception as exc:
            context.abort(grpc.StatusCode.UNAVAILABLE, f"NVIDIA NIM request failed: {exc}")

        text = completion.choices[0].message.content or ""
        return genai_pb2.GenerateTextResponse(text=text)


def serve():
    port = os.getenv("GENAI_GRPC_PORT", "50051")
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
    genai_pb2_grpc.add_GenAiServiceServicer_to_server(GenAiService(), server)
    server.add_insecure_port(f"[::]:{port}")
    server.start()
    print(f"Python GenAI gRPC service running on port {port}")
    server.wait_for_termination()


if __name__ == "__main__":
    serve()
