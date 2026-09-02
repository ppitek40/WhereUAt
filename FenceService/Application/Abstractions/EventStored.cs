namespace Application.Abstractions;

public record EventStored<T>(T EventData, int version, Type EventType);