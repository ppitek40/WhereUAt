using Domain.Events;
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
    public bool IsDeleted { get; private set; }
    public bool IsCrossed { get; private set; }

    public static FenceCreated Create(
        FenceName name,
        CreatorId creatorId,
        TargetId targetId,
        RadiusInMeters radiusInMeters,
        Location location)
    {
        if (creatorId.Value == targetId.Value)
            throw new ArgumentException("You can't put fence on yourself.");

        var fence = new Fence();

        var @event = new FenceCreated(
            FenceId.From(Guid.CreateVersion7()),
            name,
            creatorId,
            targetId,
            radiusInMeters,
            location);

        fence.Apply(@event);

        return @event;
    }

    public FenceModified Modify(
        FenceName name,
        RadiusInMeters radiusInMeters,
        Location location)
    {
        var @event = new FenceModified(
            Id,
            name,
            radiusInMeters,
            location);

        Apply(@event);

        return @event;
    }

    public FenceDeleted Delete()
    {
        if (IsDeleted)
            throw new InvalidOperationException("Fence is already deleted.");

        var @event = new FenceDeleted(Id);

        Apply(@event);

        return @event;
    }

    public FenceCrossed Cross()
    {
        if (IsCrossed)
            throw new InvalidOperationException("Fence is already crossed.");

        var @event = new FenceCrossed(Id, Name, CreatorId, TargetId);

        Apply(@event);

        return @event;
    }

    public FenceUncrossed Uncross()
    {
        if (IsCrossed)
            throw new InvalidOperationException("Fence is already not crossed.");

        var @event = new FenceUncrossed(Id);

        Apply(@event);

        return @event;
    }

    private void Apply(FenceDeleted @event)
    {
        IsDeleted = true;
    }

    private void Apply(FenceCreated @event)
    {
        Id = @event.Id;
        Name = @event.Name;
        CreatorId = @event.CreatorId;
        TargetId = @event.TargetId;
        RadiusInMeters = @event.RadiusInMeters;
        Location = @event.Location;
    }

    private void Apply(FenceModified @event)
    {
        Name = @event.Name;
        RadiusInMeters = @event.RadiusInMeters;
        Location = @event.Location;
    }

    private void Apply(FenceCrossed @event)
    {
        IsCrossed = true;
    }

    private void Apply(FenceUncrossed @event)
    {
        IsCrossed = false;
    }
}