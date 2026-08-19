using Application.Abstractions;
using WhereUAt.SharedKernel;

namespace Infrastructure;

public class EventStore : IEventStore
{
    public Result Save<T>(T @event)
    {
        return Result.Success();
    }
}