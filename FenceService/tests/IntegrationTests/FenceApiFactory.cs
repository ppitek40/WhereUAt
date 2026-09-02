using Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MongoDb;

namespace IntegrationTests;

public class FenceApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:8")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            $"{MongoDbSettings.SectionName}:{nameof(MongoDbSettings.ConnectionString)}",
            _mongo.GetConnectionString());

        builder.UseSetting(
            $"{MongoDbSettings.SectionName}:{nameof(MongoDbSettings.DatabaseName)}",
            "WhereUAt_IntegrationTests");
    }

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _mongo.DisposeAsync();
        await base.DisposeAsync();
    }
}