using Robust.Shared.GameStates;

namespace Content.Server.FloofStation.Traits;

[RegisterComponent, Access(typeof(MilkerSystem))]
public sealed partial class MilkerAttachedComponent : Component
{
    [ViewVariables]
    public EntityUid? VisualEntity;

    [ViewVariables]
    public bool BreastAttached;

    [ViewVariables]
    public bool GroinAttached;
}
