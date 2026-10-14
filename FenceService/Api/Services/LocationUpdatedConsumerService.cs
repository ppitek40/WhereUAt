using Infrastructure;
using WhereUAt.SharedKernel;

namespace Api.Services;

public class LocationUpdatedConsumerService(EventBroker eventBroker) : EventConsumerService<LocationUpdatedEvent>(eventBroker)
{
    protected override string TopicId => "location.location-updated.v1";
    protected override string GroupId => "location-updated.consumer";

    protected override Task<Result> Handle(IServiceProvider serviceProvider, LocationUpdatedEvent text)
    {
        var handler = serviceProvider.GetRequiredService<ILocationUpdatedHandler>();
        return handler.Handle(text);
    }
}

public class LocationUpdatedEvent
{
}