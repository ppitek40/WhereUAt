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
        var creatorId = CreatorId.From(command.CreatorId);
        var targetId = TargetId.From(command.TargetId);

        var canWatchResult = permissionService.CanWatch(creatorId, targetId);

        if (canWatchResult.IsFailure)
            return Result<FenceId>.From(canWatchResult);

        var fenceCreatedResult = Fence.Create(
            FenceName.From(command.Name),
            creatorId,
            targetId,
            RadiusInMeters.From(command.RadiusInMeters),
            new Location(Latitude.From(command.Latitude), Longitude.From(command.Longitude))
        );

        if(fenceCreatedResult.IsFailure)
            return Result<FenceId>.From(fenceCreatedResult);

        var saveResult = eventStore.Save(fenceCreatedResult.Value);

        if(saveResult.IsFailure)
            return Result<FenceId>.From(saveResult);

        return fenceCreatedResult.Value!.Id;
    }
}