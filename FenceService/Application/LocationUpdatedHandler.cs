namespace Application;

public class LocationUpdatedHandler
{
   public Task Handle(string location)
   {
      Console.WriteLine(location);
      return Task.CompletedTask;
   }
}