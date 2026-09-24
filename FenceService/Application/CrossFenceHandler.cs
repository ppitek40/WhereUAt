using Application.Abstractions;
using Domain.Models;
using WhereUAt.SharedKernel;

namespace Application;

public record CrossFenceCommand(Guid FenceId);

public class CrossFenceHandler(IEventStore eventStore)
{
    public async Task<Result> Handle(CrossFenceCommand request, CancellationToken cancellationToken)
    {
        var fenceEvents = await eventStore.GetAsync(request.FenceId, cancellationToken);
        var fence = Fence.Load(fenceEvents.Select(x => x.EventData).ToList()); 
        fence.Cross();
        return Result.Success();
    }
}