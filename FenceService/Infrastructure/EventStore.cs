using Application.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using WhereUAt.SharedKernel;

namespace Infrastructure;

public class EventStore : IEventStore
{
    public Result Save<T>(
        Guid streamId,
        Type streamType,
        T eventData,
        int version)
    {
        var @event = new Event<T>
        {
            EventData = eventData,
            EventType = typeof(T).Name,
            StreamId =  streamId,
            StreamType = streamType.Name,
            PublishedAt = DateTime.UtcNow,
            Published = false,
            Version = version
        };

        var client = new MongoClient("mongodb://user:password1234@localhost:27017");
        var collection = client.GetDatabase("WhereUAt")
            .GetCollection<BsonDocument>("fences");

        collection.InsertOne(@event.ToBsonDocument());

        return Result.Success();
    }
}