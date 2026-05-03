using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Server.FloofStation.Traits;

[RegisterComponent, Access(typeof(MilkerSystem))]
public sealed partial class MilkerComponent : Component
{
    [DataField]
    public string SolutionName = "milker";

    [DataField]
    public bool EnsureOwnSolution = true;

    public Entity<SolutionComponent>? Solution;

    [DataField]
    public FixedPoint2 QuantityPerUpdate = 5;

    [DataField]
    public TimeSpan TransferDelay = TimeSpan.FromSeconds(2);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextTransfer = TimeSpan.Zero;

    [DataField]
    public EntityUid? AttachedTo;

    [DataField]
    public MilkerMode Mode = MilkerMode.Milk;

    [DataField]
    public EntityUid? LinkedEntity;

    [DataField]
    public string? LinkedSolutionName;

    [DataField]
    public string? LinkToContainerSlotId;

    [DataField]
    public SpriteSpecifier? LinkSprite;

    [DataField]
    public string MilkerAttachedPrototype = "HandMilkerAttachedVisual";

    public EntityUid? LinkVisualEntity;

    public bool IsActivelyDrawing;
}

public enum MilkerMode : byte
{
    Milk,
    Cum,
}
