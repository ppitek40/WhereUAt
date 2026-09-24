using Application;
using Fences.V1;
using Grpc.Core;

namespace Api.Services;

internal sealed class FencesGrpcService(CreateFenceHandler handler, DeleteFenceHandler deleteFenceHandler) : Fences.V1.Fences.FencesBase
{
    public override async Task<CreateFenceResponse> CreateFence(
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

        var result = await handler.Handle(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.InvalidArgument, string.Join("; ", result.Errors)));

        return new CreateFenceResponse
        {
            FenceId = result.Value!.Value.ToString()
        };
    }

    public override async Task<DeleteFenceResponse> DeleteFence(DeleteFenceRequest request, ServerCallContext context)
    {
        var command = new DeleteFenceCommand(Guid.Parse(request.FenceId));

        var result = await deleteFenceHandler.Handle(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.InvalidArgument, string.Join("; ", result.Errors)));

        return new DeleteFenceResponse();
    }
}