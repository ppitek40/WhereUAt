using Application.Abstractions;
using Domain.Models;
using Domain.ValueObjects;
using WhereUAt.SharedKernel;

namespace Application;

public record FenceEvaluateCommand(Guid FenceId, Location TargetLocation);

public class FenceEvaluateHandler(IEventStore eventStore)
{
    public async Task<Result> Handle(FenceEvaluateCommand request, CancellationToken cancellationToken)
    {
        var fenceEvents = await eventStore.GetAsync(request.FenceId, cancellationToken);
        var fenceResult = Fence.Load([.. fenceEvents.Select(x => x.EventData)]);

        if (fenceResult.IsFailure)
            return fenceResult;

        var fenceEvaluateResult = fenceResult.Value!
            .Evaluate(request.TargetLocation);

        if (fenceEvaluateResult.IsFailure)
            return fenceEvaluateResult;

        if (fenceEvaluateResult.Value == null)
            return Result.Success();

        var saveResult = await eventStore.SaveAsync(
            fenceResult.Value!.Id.Value,
            typeof(Fence),
            fenceEvaluateResult.Value,
            fenceEvents.Last().version + 1,
            cancellationToken);

        if (saveResult.IsFailure)
            return Result<FenceId>.From(saveResult);

        return Result.Success();
    }
}