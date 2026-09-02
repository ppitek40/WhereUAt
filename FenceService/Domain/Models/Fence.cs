using Domain.Events;
using Domain.ValueObjects;
using WhereUAt.SharedKernel;

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

    private Fence()
    {
    }

    public static Result<FenceCreated> Create(
        FenceName name,
        CreatorId creatorId,
        TargetId targetId,
        RadiusInMeters radiusInMeters,
        Location location)
    {
        if (creatorId.Value == targetId.Value)
            return Result<FenceCreated>.Failure("You can't put fence on yourself.");

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

    public Result<FenceModified> Modify(
        FenceName name,
        RadiusInMeters radiusInMeters,
        Location location)
    {
        if (IsDeleted)
            return Result<FenceModified>.Failure("Fence is already deleted.");

        var @event = new FenceModified(
            Id,
            name,
            radiusInMeters,
            location);

        Apply(@event);

        return @event;
    }

    public Result<FenceDeleted> Delete()
    {
        if (IsDeleted)
            return Result<FenceDeleted>.Failure("Fence is already deleted.");

        var @event = new FenceDeleted(Id);

        Apply(@event);

        return @event;
    }

    public Result<FenceCrossed> Cross()
    {
        if (IsDeleted)
            return Result<FenceCrossed>.Failure("Fence is already deleted.");

        if (IsCrossed)
            return Result<FenceCrossed>.Failure("Fence is already crossed.");

        var @event = new FenceCrossed(Id, Name, CreatorId, TargetId);

        Apply(@event);

        return @event;
    }

    public Result<FenceUncrossed> Uncross()
    {
        if (IsDeleted)
            return Result<FenceUncrossed>.Failure("Fence is already deleted.");

        if (!IsCrossed)
            return Result<FenceUncrossed>.Failure("Fence is already not crossed.");

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

    public static Fence Load(ICollection<IFenceEvent> events)
    {                                    
        if (events == null || events.Count == 0)
            throw new ArgumentNullException(nameof(events));

        var fence = new Fence();
        foreach(var @event in events)
            fence.When(@event);

        return fence;
    }

    private void When(IFenceEvent @event)
    {
        switch (@event)
        {
            case FenceCreated e:
            Apply(e);
            break;
            case FenceUncrossed e:
            Apply(e);
            break;
            case FenceDeleted e:
            Apply(e);
            break;
            case FenceModified e:
            Apply(e);
            break;            
            case FenceCrossed e:
            Apply(e);
            break;
            default:
                throw new InvalidOperationException($"Unknown event type: {@event.GetType().Name}");
        }
    }
}