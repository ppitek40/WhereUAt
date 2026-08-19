namespace Infrastructure;

public class Event<T>
{
    public Guid StreamId { get; set; }
    public string StreamType { get; set; }
    public int Version { get; set; }
    public string EventType { get; set; }
    public T EventData { get; set; }
    public bool Published { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
}