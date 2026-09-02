using Domain.Events;
using WhereUAt.SharedKernel;

namespace Application.Abstractions;

public interface IEventStore
{
    public Result Save<T>(
        Guid streamId,
        Type streamType,
        T eventData,
        int version);

    public Task<IList<EventStored<IFenceEvent>>> Get(Guid streamId);
}