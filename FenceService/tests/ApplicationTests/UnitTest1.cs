using Application;
using Infrastructure;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace ApplicationTests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        foreach (var serializer in BsonValueObjectConverter.BsonSerializers)
            BsonSerializer.RegisterSerializer(serializer.ValueType, serializer);

        var command = new CreateFenceCommand(
            "FenceName",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            10,
            5,
            7
        );

        var handler = new CreateFenceCommandHandler(new PermissionService(), new EventStore());

        handler.Handle(command);
    }
}