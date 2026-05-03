using Robust.Shared.Serialization;

namespace Content.Shared.FloofStation.Traits;

[Serializable, NetSerializable]
public enum MilkerVisuals : byte
{
    BreastAttached,
    GroinAttached,
    Active,
}
