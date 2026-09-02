using Domain.ValueObjects;

namespace Domain.Events;

public sealed record FenceDeleted(FenceId Id) : IFenceEvent;
