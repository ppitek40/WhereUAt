using Domain.ValueObjects;

namespace Domain.Events;

public sealed record FenceCrossed(FenceId Id, FenceName Name, CreatorId CreatorId, TargetId TargetId);