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
        var fence = Fence.Load([.. fenceEvents.Select(x => x.EventData)]);
        fence.Evaluate(request.TargetLocation);

        //TODO: MONGO
        return Result.Success();
    }
}