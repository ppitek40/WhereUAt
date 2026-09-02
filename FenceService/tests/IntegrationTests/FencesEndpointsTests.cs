using System.Net;
using System.Net.Http.Json;
using Application;
using FluentAssertions;

namespace IntegrationTests;

public class FencesEndpointsTests(FenceApiFactory factory) : IClassFixture<FenceApiFactory>
{
    [Fact]
    public async Task CreateFence_ReturnsCreatedStatusCode()
    {
        // arrange
        var client = factory.CreateClient();

        var command = new CreateFenceCommand(
            Name: "Home",
            CreatorId: Guid.CreateVersion7(),
            TargetId: Guid.CreateVersion7(),
            RadiusInMeters: 100,
            Latitude: 52.2297,
            Longitude: 21.0122
        );

        // act
        var response = await client.PostAsJsonAsync("/api/fences", command);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}