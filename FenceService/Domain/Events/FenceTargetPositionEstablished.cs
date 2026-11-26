using Domain.ValueObjects;

namespace Domain.Events;

public sealed record FenceTargetPositionEstablished(TargetPosition TargetPosition, DateTimeOffset OccurredAt) : IFenceEvent;