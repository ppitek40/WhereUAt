using WhereUAt.SharedKernel;

namespace Application.Abstractions;

public interface IEventStore
{
    public Result Save<T>(T @event);
}