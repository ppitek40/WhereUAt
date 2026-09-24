using Application.Abstractions;
using Domain.Models;
using Fences.V1;
using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

public class FencesGrpcServiceTests(FenceApiFactory factory) : IClassFixture<FenceApiFactory>
{
    [Fact]
    public async Task CreateFence_WithValidRequest_ReturnsFenceId()
    {
        // arrange
        var client = factory.CreateGrpcClient();

        var command = new CreateFenceRequest
        {
            Name = "Home",
            CreatorId = Guid.CreateVersion7().ToString(),
            TargetId = Guid.CreateVersion7().ToString(),
            RadiusInMeters = 100,
            Latitude = 52.2297,
            Longitude = 21.0122
        };

        // act
        var response = await client.CreateFenceAsync(command);

        // assert
        Guid.TryParse(response.FenceId, out _)
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task CreateFence_WithInvalidRequest_ShouldThrowRpcException()
    {
        // arrange
        var client = factory.CreateGrpcClient();

        var command = new CreateFenceRequest
        {
            Name = "Home",
            CreatorId = Guid.CreateVersion7().ToString(),
            TargetId = Guid.CreateVersion7().ToString(),
            RadiusInMeters = -100,
            Latitude = 52.2297,
            Longitude = 21.0122
        };

        // act
        var act = async () => await client.CreateFenceAsync(command);

        // assert
        var exception = await act.Should().ThrowAsync<RpcException>();
        exception.Which.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task ModifyFence_WithValidGuid_ShouldBeSuccess()
    {
        // arrange
        var client = factory.CreateGrpcClient();

        var command = new CreateFenceRequest
        {
            Name = "Home2",
            CreatorId = Guid.CreateVersion7().ToString(),
            TargetId = Guid.CreateVersion7().ToString(),
            RadiusInMeters = 100,
            Latitude = 52.2297,
            Longitude = 21.0122
        };
        var createResponse = await client.CreateFenceAsync(command);

        // act
        var modifiedRequest = new ModifyFenceRequest
        {
            FenceId = createResponse.FenceId,
            Name = "Modified",
            Latitude = 1.11,
            Longitude = 2.22,
            RadiusInMeters = 99
        };

        await client.ModifyFenceAsync(modifiedRequest);

        using var scope = factory.Services.CreateScope();
        var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();

        var events = await eventStore.GetAsync(Guid.Parse(createResponse.FenceId), CancellationToken.None);
        var fence = Fence.Load([.. events.Select(@event => @event.EventData)]);

        fence.Name.Value.Should().Be(modifiedRequest.Name);
        fence.Location.Lat.Value.Should().Be(modifiedRequest.Latitude);
        fence.Location.Lng.Value.Should().Be(modifiedRequest.Longitude);
        fence.RadiusInMeters.Value.Should().Be(modifiedRequest.RadiusInMeters);
    }

    [Fact]
    public async Task DeleteFence_WithValidGuid_ShouldBeSuccess()
    {
        // arrange
        var client = factory.CreateGrpcClient();

        var command = new CreateFenceRequest
        {
            Name = "Home2",
            CreatorId = Guid.CreateVersion7().ToString(),
            TargetId = Guid.CreateVersion7().ToString(),
            RadiusInMeters = 100,
            Latitude = 52.2297,
            Longitude = 21.0122
        };
        var createResponse = await client.CreateFenceAsync(command);

        // act + assert
        var func = async () => await client.DeleteFenceAsync(new DeleteFenceRequest() { FenceId = createResponse.FenceId });
        await func.Should().NotThrowAsync();
    }
}