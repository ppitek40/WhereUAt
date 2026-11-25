using Application.Abstractions;
using Domain.Models;
using Domain.ValueObjects;
using WhereUAt.SharedKernel;

namespace Application;

public record ModifyFenceCommand(
    Guid FenceId,
    string Name,
    int RadiusInMeter,
    double Latitude,
    double Longitude
);

public class ModifyFenceHandler(IEventStore eventStore)
{
    public async Task<Result> Handle(ModifyFenceCommand command, CancellationToken cancellationToken)
    {
        var fenceIdResult = FenceId.TryFrom(command.FenceId).ToResult();
        var name = FenceName.TryFrom(command.Name).ToResult();
        var radiusInMeter = RadiusInMeters.TryFrom(command.RadiusInMeter).ToResult();
        var latitude = Latitude.TryFrom(command.Latitude).ToResult();
        var longitude = Longitude.TryFrom(command.Longitude).ToResult();

        if (Result.AnyFailed([name, radiusInMeter, latitude, longitude], out var failedResult))
            return Result<FenceId>.From(failedResult);

        if (fenceIdResult.IsFailure)
            return fenceIdResult;

        var fenceEvents = await eventStore.GetAsync(fenceIdResult.Value.Value, cancellationToken);
        var fenceResult = Fence.Load([.. fenceEvents.Select(x => x.EventData)]);

        if (fenceResult.IsFailure)
            return fenceResult;

        var modifyEventResult = fenceResult.Value!.Modify(
            name.Value,
            radiusInMeter.Value,
            new Location(latitude.Value, longitude.Value));

        if (modifyEventResult.IsFailure)
            return modifyEventResult;

        var saveResult = await eventStore.SaveAsync(
            fenceResult.Value.Id.Value,
            typeof(Fence),
            modifyEventResult.Value,
            fenceEvents.Last().version + 1,
            cancellationToken);

        if (saveResult.IsFailure)
            return saveResult;

        return Result.Success();
    }
}