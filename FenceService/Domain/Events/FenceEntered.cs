namespace Domain.Events;

public sealed record FenceEntered(DateTimeOffset OccurredAt) : IFenceEvent;