using Application.Abstractions;
using Domain.Events;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using WhereUAt.SharedKernel;

namespace Infrastructure;

public class EventStore(IMongoDatabase mongoDatabase) : IEventStore
{
    public async Task<Result> SaveAsync<T>(
        Guid streamId,
        Type streamType,
        T eventData,
        int version,
        CancellationToken cancellationToken)
    {
        var @event = new Event
        {
            EventData = eventData.ToBsonDocument(),
            EventType = typeof(T).AssemblyQualifiedName!,
            StreamId =  streamId,
            StreamType = streamType.Name,
            PublishedAt = DateTime.UtcNow,
            Published = false,
            Version = version
        };
    
        try
        {
            await mongoDatabase.GetCollection<BsonDocument>("fences")
                .InsertOneAsync(@event.ToBsonDocument(), cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return Result.Failure($"Concurrency conflict: stream {streamId} already has version {version}.");
        }

        return Result.Success();
    }

    public async Task<IList<EventStored<IFenceEvent>>> GetAsync(Guid streamId, CancellationToken cancellationToken)
    {
        var collection = mongoDatabase.GetCollection<Event>("fences");

        var events = await collection.Find(x => x.StreamId.ToString() == streamId.ToString())
            .Sort(Builders<Event>.Sort.Ascending(x => x.Version)).ToListAsync(cancellationToken);

    return
    [
        .. events.Select(x =>
        {
            var eventType = Type.GetType(x.EventType)
                            ?? throw new InvalidOperationException($"Cannot resolve event type '{x.EventType}'.");

            var eventData = (IFenceEvent)BsonSerializer.Deserialize(x.EventData, eventType);

            return new EventStored<IFenceEvent>(eventData, x.Version, eventType);
        })
    ];}
}