using Application.Abstractions;
using Domain.Models;
using Domain.ValueObjects;
using Vogen;
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
        var name = FenceName.TryFrom(command.Name);
        var radius = RadiusInMeters.TryFrom(command.RadiusInMeters);
        var latitude = Latitude.TryFrom(command.Latitude);
        var longitude = Longitude.TryFrom(command.Longitude);
        var creatorId = CreatorId.TryFrom(command.CreatorId);
        var targetId = TargetId.TryFrom(command.TargetId);

        var errors = new[]
            {
                name.Error,
                radius.Error,
                latitude.Error,
                longitude.Error,
                creatorId.Error,
                targetId.Error
            }
            .Where(e => e != Validation.Ok)
            .Select(e => e.ErrorMessage)
            .ToArray();

        if (errors.Length != 0)
            return Result<FenceId>.Failure(errors);

        var canWatchResult = permissionService.CanWatch(creatorId.ValueObject, targetId.ValueObject);

        if (canWatchResult.IsFailure)
            return Result<FenceId>.From(canWatchResult);

        var fenceCreatedResult = Fence.Create(
            name.ValueObject,
            creatorId.ValueObject,
            targetId.ValueObject,
            radius.ValueObject,
            new Location(latitude.ValueObject, longitude.ValueObject)
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