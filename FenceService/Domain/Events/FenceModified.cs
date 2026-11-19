using Domain.ValueObjects;

namespace Domain.Events;

public sealed record FenceModified(
    FenceName Name,
    RadiusInMeters RadiusInMeters,
    Location Location) : IFenceEvent;