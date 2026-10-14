using WhereUAt.SharedKernel;

namespace Infrastructure;

public class EventBroker
{
   public void Publish<T>(T @event) where T : class
   {
   }
}