using Application.Abstractions;
using Domain.Models;
using Domain.ValueObjects;
using WhereUAt.SharedKernel;

namespace Application;

public record CreateFenceCommand(
    string Name,
    Guid CreatorId,
    Guid TargetId,
    int RadiusInMeters,
    double Latitude,
    double Longitude
);

public class CreateFenceCommandHandler(
    IPermissionService permissionService,
    IEventStore eventStore)
{
    public Result<FenceId> Handle(CreateFenceCommand command)
    {
        var name = FenceName.TryFrom(command.Name).ToResult();
        var radius = RadiusInMeters.TryFrom(command.RadiusInMeters).ToResult();
        var latitude = Latitude.TryFrom(command.Latitude).ToResult();
        var longitude = Longitude.TryFrom(command.Longitude).ToResult();
        var creatorId = CreatorId.TryFrom(command.CreatorId).ToResult();
        var targetId = TargetId.TryFrom(command.TargetId).ToResult();

        if (Result.AnyFailed([name, radius, latitude, longitude, creatorId], out var failedResult))
        {
            return Result<FenceId>.From(failedResult);
        } 

        var canWatchResult = permissionService.CanWatch(creatorId.Value, targetId.Value);

        if (canWatchResult.IsFailure)
            return Result<FenceId>.From(canWatchResult);

        var fenceCreatedResult = Fence.Create(
            name.Value,
            creatorId.Value,
            targetId.Value,
            radius.Value,
            new Location(latitude.Value, longitude.Value)
        );

        if (fenceCreatedResult.IsFailure)
            return Result<FenceId>.From(fenceCreatedResult);

        var fenceCreatedEvent = fenceCreatedResult.Value!;

        var saveResult = eventStore.Save(
            fenceCreatedEvent.Id.Value,
            typeof(Fence),
            fenceCreatedEvent,
            1);

        if (saveResult.IsFailure)
            return Result<FenceId>.From(saveResult);

        return fenceCreatedResult.Value!.Id;
    }
}