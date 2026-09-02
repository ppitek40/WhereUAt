using Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

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
        return services;
    }
}