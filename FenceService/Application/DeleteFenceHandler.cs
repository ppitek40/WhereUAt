using Application.Abstractions;
using Domain.Models;
using Domain.ValueObjects;
using WhereUAt.SharedKernel;

namespace Application;
                     
public record DeleteFenceCommand(Guid FenceId);

public class DeleteFenceHandler(IEventStore eventStore)
{
    public async Task<Result> Handle(DeleteFenceCommand command, CancellationToken cancellationToken)
    {
        var fenceIdResult = FenceId.TryFrom(command.FenceId).ToResult();

        if (fenceIdResult.IsFailure)
            return fenceIdResult;

        var fenceEvents = await eventStore.GetAsync(fenceIdResult.Value.Value, cancellationToken);
        var fence = Fence.Load([.. fenceEvents.Select(x => x.EventData)]);

        var deleteEventResult = fence.Delete();

        if (deleteEventResult.IsFailure)
            return deleteEventResult;

        var saveResult = await eventStore.SaveAsync(fenceIdResult.Value.Value,
            typeof(Fence),
            deleteEventResult.Value,
            fenceEvents.Last().Version + 1,
            cancellationToken);
        
        if (saveResult.IsFailure)
            return saveResult;

        return Result.Success();
    }
}