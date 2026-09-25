namespace Application.Abstractions;

public record EventStored<T>(T EventData, int Version, Type EventType);