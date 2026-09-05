# Generated-compatible gRPC definitions for Backend/Protos/genai.proto.
# Regenerate with grpc_tools.protoc if the proto contract changes.

import grpc

import genai_pb2 as genai__pb2


class GenAiServiceStub:
    def __init__(self, channel):
        self.GenerateText = channel.unary_unary(
            "/genai.GenAiService/GenerateText",
            request_serializer=genai__pb2.GenerateTextRequest.SerializeToString,
            response_deserializer=genai__pb2.GenerateTextResponse.FromString,
        )


class GenAiServiceServicer:
    def GenerateText(self, request, context):
        context.set_code(grpc.StatusCode.UNIMPLEMENTED)
        context.set_details("Method not implemented.")
        raise NotImplementedError("Method not implemented.")


def add_GenAiServiceServicer_to_server(servicer, server):
    rpc_method_handlers = {
        "GenerateText": grpc.unary_unary_rpc_method_handler(
            servicer.GenerateText,
            request_deserializer=genai__pb2.GenerateTextRequest.FromString,
            response_serializer=genai__pb2.GenerateTextResponse.SerializeToString,
        ),
    }
    generic_handler = grpc.method_handlers_generic_handler(
        "genai.GenAiService",
        rpc_method_handlers,
    )
    server.add_generic_rpc_handlers((generic_handler,))
