using Domain.ValueObjects;

namespace Domain.Events;

public sealed record FenceModified(
    FenceId Id,
    FenceName Name,
    RadiusInMeters RadiusInMeters,
    Location Location) : IFenceEvent;