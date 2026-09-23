using Application;
using Fences.V1;
using Grpc.Core;

namespace Api.Services;

internal sealed class FencesGrpcService(CreateFenceCommandHandler handler) : Fences.V1.Fences.FencesBase
{
    public override Task<CreateFenceResponse> CreateFence(
        CreateFenceRequest request,
        ServerCallContext context)
    {
        var command = new CreateFenceCommand(
            Name: request.Name,
            CreatorId: Guid.Parse(request.CreatorId),
            TargetId: Guid.Parse(request.TargetId),
            RadiusInMeters: request.RadiusInMeters,
            Latitude: request.Latitude,
            Longitude: request.Longitude);

        var result = handler.Handle(command);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.InvalidArgument, string.Join("; ", result.Errors)));

        return Task.FromResult(new CreateFenceResponse
        {
            FenceId = result.Value!.Value.ToString()
        });
    }
}