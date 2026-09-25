using Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IEventStore, EventStore>();
        services.AddScoped<IPermissionService, PermissionService>();

        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        foreach (var serializer in BsonValueObjectConverter.BsonSerializers)
        {
            BsonSerializer.RegisterSerializer(serializer.ValueType, serializer);
        }

        services.AddSingleton<IMongoDatabase>(serviceProvider =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<MongoDbSettings>>().Value;
            var database = new MongoClient(settings.ConnectionString)
                .GetDatabase(settings.DatabaseName);

            var keys = Builders<Event>.IndexKeys
                .Ascending(e => e.StreamId)
                .Ascending(e => e.Version);

            database.GetCollection<Event>("fences")
                .Indexes.CreateOne(new CreateIndexModel<Event>(keys, new CreateIndexOptions { Unique = true }));

            return database;
        });

        return services;
    }
}