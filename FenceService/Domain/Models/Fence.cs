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
    public FenceArea FenceArea { get; private set; }
    public bool IsDeleted { get; private set; }
    public TargetPosition TargetPosition { get; private set; }

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

        var @event = new FenceDeleted();

        Apply(@event);

        return @event;
    }

    public Result<IFenceEvent?> Evaluate(Location targetLocation)
    {
        if (IsDeleted)
            return Result<IFenceEvent?>.Failure("Fence is deleted.");

        var newPosition = FenceArea.Contains(targetLocation)
            ? TargetPosition.Inside
            : TargetPosition.Outside;

        IFenceEvent? @event = (TargetPosition, newPosition) switch
        {
            (TargetPosition.Unknown, _) => new FenceTargetPositionEstablished(newPosition),
            (TargetPosition.Outside, TargetPosition.Inside) => new FenceEntered(),
            (TargetPosition.Inside, TargetPosition.Outside) => new FenceExited(),
            _ => null
        };

        if (@event is not null)
            Apply(@event);

        return Result<IFenceEvent?>.Success(@event);
    }

    private void Apply(FenceDeleted _)
    {
        IsDeleted = true;
    }

    private void Apply(FenceCreated @event)
    {
        Id = @event.Id;
        Name = @event.Name;
        CreatorId = @event.CreatorId;
        TargetId = @event.TargetId;
        FenceArea = new FenceArea(@event.Location, @event.RadiusInMeters);
        TargetPosition = TargetPosition.Unknown;
    }

    private void Apply(FenceModified @event)
    {
        Name = @event.Name;
        FenceArea = new FenceArea(@event.Location, @event.RadiusInMeters);
        TargetPosition = TargetPosition.Unknown;
    }

    private void Apply(FenceEntered _)
    {
        TargetPosition = TargetPosition.Inside;
    }

    private void Apply(FenceExited _)
    {
        TargetPosition = TargetPosition.Outside;
    }

    private void Apply(FenceTargetPositionEstablished @event)
    {
        TargetPosition = @event.TargetPosition;
    }

    public static Result<Fence> Load(ICollection<IFenceEvent>? events)
    {
        if (events == null || events.Count == 0)
            return Result.Failure<Fence>("Fence not found");

        var fence = new Fence();
        foreach (var @event in events)
            fence.Apply(@event);

        return fence;
    }

    private void Apply(IFenceEvent @event)
    {
        switch (@event)
        {
            case FenceCreated e:
                Apply(e);
                break;
            case FenceExited e:
                Apply(e);
                break;
            case FenceDeleted e:
                Apply(e);
                break;
            case FenceModified e:
                Apply(e);
                break;
            case FenceEntered e:
                Apply(e);
                break;
            case FenceTargetPositionEstablished e:
                Apply(e);
                break;
            default:
                throw new InvalidOperationException($"Unknown event type: {@event.GetType().Name}");
        }
    }
}