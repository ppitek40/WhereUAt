using Fences.V1;
using FluentAssertions;
using Grpc.Core;

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
}