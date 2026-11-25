using Domain.Events;
using Domain.Models;
using Domain.ValueObjects;
using FluentAssertions;

namespace DomainTests;

public class FenceTests
{
    private static readonly Location Center = new(Latitude.From(52.2297), Longitude.From(21.0122));
    private static readonly Location FarAway = new(Latitude.From(52.2397), Longitude.From(21.0122)); // ~1.1 km north

    private static FenceCreated Created() =>
        new(
            FenceId.From(Guid.CreateVersion7()),
            FenceName.From("Home"),
            CreatorId.From(Guid.CreateVersion7()),
            TargetId.From(Guid.CreateVersion7()),
            RadiusInMeters.From(100),
            Center);

    [Fact]
    public void Create_WhenCreatorIsTarget_ReturnsFailure()
    {
        // given
        var userId = Guid.CreateVersion7();

        // when
        var result = Fence.Create(
            FenceName.From("Home"),
            CreatorId.From(userId),
            TargetId.From(userId),
            RadiusInMeters.From(100),
            Center);

        // then
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Modify_WhenFenceDeleted_ReturnsFailure()
    {
        // given
        var fence = Fence.Load([Created(), new FenceDeleted()]).Value!;

        // when
        var result = fence.Modify(FenceName.From("New"), RadiusInMeters.From(50), FarAway);

        // then
        result.IsFailure.Should().BeTrue();
        fence.Name.Value.Should().Be("Home");
    }

    [Fact]
    public void Delete_WhenFenceAlreadyDeleted_ReturnsFailure()
    {
        // given
        var fence = Fence.Load([Created(), new FenceDeleted()]).Value!;

        // when
        var result = fence.Delete();

        // then
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WhenTargetWasOutsideAndMovesInside_EmitsFenceEntered()
    {
        // given
        var fence = Fence.Load([
            Created(),
            new FenceTargetPositionEstablished(TargetPosition.Outside)
        ]).Value!;

        // when
        var result = fence.Evaluate(Center);

        // then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<FenceEntered>();
        fence.TargetPosition.Should().Be(TargetPosition.Inside);
    }

    [Fact]
    public void Evaluate_WhenTargetWasInsideAndMovesOutside_EmitsFenceExited()
    {
        // given
        var fence = Fence.Load([
            Created(),
            new FenceTargetPositionEstablished(TargetPosition.Inside)
        ]).Value!;

        // when
        var result = fence.Evaluate(FarAway);

        // then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<FenceExited>();
        fence.TargetPosition.Should().Be(TargetPosition.Outside);
    }

    [Fact]
    public void Evaluate_WhenTargetWasUnknownAndMovesInside_EmitsFenceTargetPositionEstablished()
    {
        // given
        var fence = Fence.Load([
            Created(),
        ]).Value!;

        // when
        var result = fence.Evaluate(Center);

        // then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<FenceTargetPositionEstablished>();
        fence.TargetPosition.Should().Be(TargetPosition.Inside);
    }

    [Fact]
    public void Evaluate_WhenTargetWasUnknownAndMovesOutside_EmitsFenceTargetPositionEstablished()
    {
        // given
        var fence = Fence.Load([
            Created(),
        ]).Value!;

        // when
        var result = fence.Evaluate(FarAway);

        // then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<FenceTargetPositionEstablished>();
        fence.TargetPosition.Should().Be(TargetPosition.Outside);
    }

    [Fact]
    public void Evaluate_WhenTargetWasOutsideAndMovesOutside_EmitsNothing()
    {
        // given
        var fence = Fence.Load([
            Created(),
            new FenceTargetPositionEstablished(TargetPosition.Outside)
        ]).Value!;

        // when
        var result = fence.Evaluate(FarAway);

        // then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        fence.TargetPosition.Should().Be(TargetPosition.Outside);
    }

    [Fact]
    public void Evaluate_WhenTargetWasInsideAndMovesInside_EmitsNothing()
    {
        // given
        var fence = Fence.Load([
            Created(),
            new FenceTargetPositionEstablished(TargetPosition.Inside)
        ]).Value!;

        // when
        var result = fence.Evaluate(Center);

        // then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        fence.TargetPosition.Should().Be(TargetPosition.Inside);
    }

    [Fact]
    public void Evaluate_AfterFenceAreaModified_ReestablishesPositionInsteadOfEmittingEntered()
    {
        // given
        var fence = Fence.Load([
            Created(),
            new FenceTargetPositionEstablished(TargetPosition.Outside),
            new FenceModified(FenceName.From("Home"), RadiusInMeters.From(100), FarAway)
        ]).Value!;

        // when
        var result = fence.Evaluate(FarAway);

        // then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<FenceTargetPositionEstablished>()
            .Which.TargetPosition.Should().Be(TargetPosition.Inside);
        fence.TargetPosition.Should().Be(TargetPosition.Inside);
    }

    [Fact]
    public void Evaluate_WhenFenceDeleted_ReturnsFailure()
    {
        // given
        var fence = Fence.Load([
            Created(),
            new FenceDeleted()
        ]).Value!;

        // when
        var result = fence.Evaluate(Center);

        // then
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        fence.TargetPosition.Should().Be(TargetPosition.Unknown);
    }
}