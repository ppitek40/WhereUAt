using MongoDB.Bson;

namespace Infrastructure;

public class Event
{
    public ObjectId Id { get; set; }
    public Guid StreamId { get; set; }
    public string StreamType { get; set; }
    public int Version { get; set; }
    public string EventType { get; set; }
    public BsonDocument EventData { get; set; }
    public bool Published { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
}