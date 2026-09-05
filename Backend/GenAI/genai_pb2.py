# Generated-compatible protobuf definitions for Backend/Protos/genai.proto.
# Regenerate with grpc_tools.protoc if the proto contract changes.

from google.protobuf import descriptor_pb2 as _descriptor_pb2
from google.protobuf import descriptor_pool as _descriptor_pool
from google.protobuf.internal import builder as _builder


_file_descriptor = _descriptor_pb2.FileDescriptorProto()
_file_descriptor.name = "genai.proto"
_file_descriptor.package = "genai"
_file_descriptor.syntax = "proto3"
_file_descriptor.options.csharp_namespace = "SocialMediaWeb.Protos"

_request = _file_descriptor.message_type.add()
_request.name = "GenerateTextRequest"
_request_prompt = _request.field.add()
_request_prompt.name = "prompt"
_request_prompt.number = 1
_request_prompt.label = _descriptor_pb2.FieldDescriptorProto.LABEL_OPTIONAL
_request_prompt.type = _descriptor_pb2.FieldDescriptorProto.TYPE_STRING

_response = _file_descriptor.message_type.add()
_response.name = "GenerateTextResponse"
_response_text = _response.field.add()
_response_text.name = "text"
_response_text.number = 1
_response_text.label = _descriptor_pb2.FieldDescriptorProto.LABEL_OPTIONAL
_response_text.type = _descriptor_pb2.FieldDescriptorProto.TYPE_STRING

_service = _file_descriptor.service.add()
_service.name = "GenAiService"
_method = _service.method.add()
_method.name = "GenerateText"
_method.input_type = ".genai.GenerateTextRequest"
_method.output_type = ".genai.GenerateTextResponse"

DESCRIPTOR = _descriptor_pool.Default().AddSerializedFile(_file_descriptor.SerializeToString())

_builder.BuildMessageAndEnumDescriptors(DESCRIPTOR, globals())
_builder.BuildTopDescriptorsAndMessages(DESCRIPTOR, "genai_pb2", globals())
