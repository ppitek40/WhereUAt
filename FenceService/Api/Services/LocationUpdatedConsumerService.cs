using Application;
using Microsoft.Extensions.Options;
using WhereUAt.Contracts.Location.V1;
using WhereUAt.SharedKernel;

namespace Api.Services;

public class LocationUpdatedConsumerService(IServiceScopeFactory scopeFactory, IOptions<KafkaSettings> kafkaSettings)
    : EventConsumerService<LocationUpdated>(scopeFactory, kafkaSettings)
{
    protected override string TopicId => "location.location-updated.v1";
    protected override string GroupId => "location-updated.consumer";

    protected override async Task<Result> Handle(IServiceScopeFactory serviceScopeFactory, LocationUpdated text)
    {                                                                   
        using var scope = serviceScopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<LocationUpdatedHandler>();
        await handler.Handle(text.UserId.ToString());
        return Result.Success();
    }
}