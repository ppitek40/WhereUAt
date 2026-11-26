using Application.Abstractions;
using Domain.Events;
using Domain.Models;
using Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

public class EventStoreTests(FenceApiFactory factory) : IClassFixture<FenceApiFactory>
{
      private static readonly Location Center = new(Latitude.From(52.2297), Longitude.From(21.0122));
      private static readonly Location FarAway = new(Latitude.From(52.2397), Longitude.From(21.0122)); // ~1.1 km north

      private static FenceCreated Created() =>
            new(
                  FenceId.From(Guid.CreateVersion7()),
                  FenceName.From("Home"),
                  CreatorId.From(Guid.CreateVersion7()),
                  TargetId.From(Guid.CreateVersion7()),
                  RadiusInMeters.From(100),
                  Center);

      [Fact]
      public async Task ShouldBeAbleToSaveAndLoadFence()
      {
            using var scope = factory.Services.CreateScope();
            var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();

            var createdEvent = Created();
            await eventStore.SaveAsync(createdEvent.Id.Value, typeof(Fence), createdEvent, 1, CancellationToken.None);
            var fence = Fence.Load([createdEvent]).Value!;

            var e1 = fence.Evaluate(Center, DateTimeOffset.Now);
            await eventStore.SaveAsync(fence.Id.Value, typeof(Fence), e1.Value, 2, CancellationToken.None);

            var e2 = fence.Evaluate(FarAway, DateTimeOffset.Now);
            await eventStore.SaveAsync(fence.Id.Value, typeof(Fence), e2.Value, 3, CancellationToken.None);

            var e3 = fence.Delete();
            await eventStore.SaveAsync(fence.Id.Value, typeof(Fence), e3.Value, 4, CancellationToken.None);

            var loadedEvents = await eventStore.GetAsync(fence.Id.Value, CancellationToken.None);
            loadedEvents.Should().HaveCount(4);

            var loadedFence = Fence.Load([..loadedEvents.Select(x => x.EventData)]);

            loadedFence.Value.Should().BeEquivalentTo(fence);

            var fences = await eventStore.GetFences(fence.TargetId.Value, CancellationToken.None);
            fences.Should().HaveCount(1);
            fences[0].FenceId.Should().Be(fence.Id.Value);
      }
}