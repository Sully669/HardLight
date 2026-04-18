using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.FloofStation.Traits;

[RegisterComponent, Access(typeof(MilkerSystem))]
public sealed partial class MilkerComponent : Component
{
    [DataField]
    public string SolutionName = "milker";

    public Entity<SolutionComponent>? Solution;

    [DataField]
    public FixedPoint2 QuantityPerUpdate = 1;

    [DataField]
    public TimeSpan TransferDelay = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextTransfer = TimeSpan.Zero;

    [DataField]
    public EntityUid? AttachedTo;

    [DataField]
    public MilkerMode Mode = MilkerMode.Milk;
}

public enum MilkerMode : byte
{
    Milk,
    Cum,
}
