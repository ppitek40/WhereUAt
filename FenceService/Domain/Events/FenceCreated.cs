using Domain.ValueObjects;

namespace Domain.Events;

public sealed record FenceCreated(
    FenceId Id,
    FenceName Name,
    CreatorId CreatorId,
    TargetId TargetId,
    RadiusInMeters RadiusInMeters,
    Location Location) : IFenceEvent;
