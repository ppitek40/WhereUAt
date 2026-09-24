using Domain.Events;
using WhereUAt.SharedKernel;

namespace Application.Abstractions;

public interface IEventStore
{
    public Task<Result> SaveAsync<T>(
        Guid streamId,
        Type streamType,
        T eventData,
        int version,
        CancellationToken cancellationToken);

    public Task<IList<EventStored<IFenceEvent>>> GetAsync(Guid streamId, CancellationToken cancellationToken);
}