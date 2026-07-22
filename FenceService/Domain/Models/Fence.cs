using Domain.ValueObjects;

namespace Domain.Models;

public class Fence
{
    public FenceId Id { get; private set; }
    public FenceName Name { get; private set; }
    public CreatorId CreatorId { get; private set; }
    public TargetId TargetId { get; private set; }
    public RadiusInMeters RadiusInMeters { get; private set; }
    public Location Location { get; private set; }

    public static Fence Create(
        FenceId id,
        FenceName name,
        CreatorId creatorId,
        TargetId targetId,
        RadiusInMeters radiusInMeters,
        Location location)
    {
        if (creatorId.Value == targetId.Value)
            throw new ArgumentException("You can't put fence on yourself.");

        return new Fence
        {
            Id = id,
            Name = name,
            CreatorId = creatorId,
            TargetId = targetId,
            RadiusInMeters = radiusInMeters,
            Location = location,
        };
    }
}