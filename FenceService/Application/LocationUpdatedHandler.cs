using Application.Abstractions;
using Domain.ValueObjects;

namespace Application;

public record LocationUpdatedCommand(
    Guid UserId,
    double Latitude,
    double Longitude,
    DateTimeOffset OccurredAt);

public class LocationUpdatedHandler(
    IEventStore eventStore,
    IPermissionService permissionService,
    FenceEvaluateHandler fenceEvaluateHandler)
{
    public async Task Handle(LocationUpdatedCommand location)
    {
        var fences = await eventStore.GetFences(location.UserId, CancellationToken.None);
        foreach (var fenceInfo in fences)
        {
            if (permissionService.CanWatch(CreatorId.From(fenceInfo.CreatorId), TargetId.From(location.UserId)).IsFailure)
                continue;

            var result = await fenceEvaluateHandler.Handle(
                new FenceEvaluateCommand(
                    fenceInfo.FenceId,
                    new Location(Latitude.From(location.Latitude), Longitude.From(location.Longitude)),
                    location.OccurredAt),
                CancellationToken.None);

            // TODO : What should we do if fence evaluation fails?
            //if (result.IsFailure)
        }
    }
}