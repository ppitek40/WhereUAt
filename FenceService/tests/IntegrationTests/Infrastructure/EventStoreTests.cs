using Application.Abstractions;
using Domain.Events;
using Domain.Models;
using Domain.ValueObjects;
using Fences.V1;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Infrastructure;

[Collection(nameof(FenceApiCollection))]
public class EventStoreTests(FenceApiFactory factory)
{
    [Fact]
    public async Task SaveAsync_WhenTwoWritersUseSameVersion_SecondFails()
    {
        // arrange: a real stream with one event in it
        var client = factory.CreateGrpcClient();
        var created = await client.CreateFenceAsync(new CreateFenceRequest
        {
            Name = "Race",
            CreatorId = Guid.CreateVersion7().ToString(),
            TargetId = Guid.CreateVersion7().ToString(),
            RadiusInMeters = 100,
            Latitude = 52.2297,
            Longitude = 21.0122
        });
        var streamId = Guid.Parse(created.FenceId);

        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();
        var storeA = scopeA.ServiceProvider.GetRequiredService<IEventStore>();
        var storeB = scopeB.ServiceProvider.GetRequiredService<IEventStore>();

        // both writers read the stream before either writes
        var eventsA = await storeA.GetAsync(streamId, CancellationToken.None);
        var eventsB = await storeB.GetAsync(streamId, CancellationToken.None);
        var nextVersion = eventsA.Last().Version + 1;
        eventsB.Last().Version.Should().Be(eventsA.Last().Version);

        // act
        var first = await storeA.SaveAsync(streamId, typeof(Fence),
            new FenceDeleted(FenceId.From(streamId)), nextVersion, CancellationToken.None);
        var second = await storeB.SaveAsync(streamId, typeof(Fence),
            new FenceDeleted(FenceId.From(streamId)), nextVersion, CancellationToken.None);

        // assert
        first.IsSuccess.Should().BeTrue();
        second.IsFailure.Should().BeTrue();
        second.Errors.Should().ContainSingle(e => e.Message.Contains("Concurrency conflict"));

        var stored = await storeA.GetAsync(streamId, CancellationToken.None);
        stored.Should().HaveCount(2);
    }
}