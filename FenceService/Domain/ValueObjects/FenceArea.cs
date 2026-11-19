namespace Domain.ValueObjects;

public readonly record struct FenceArea(
    Location Center,
    RadiusInMeters RadiusInMeters)
{
    public bool Contains(Location point) =>
        Center.DistanceTo(point)
        <= RadiusInMeters.Value;
}