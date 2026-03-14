using Content.Server.Atmos.Piping.Unary.EntitySystems;
using Content.Shared.Chemistry.Components;

namespace Content.Server.Atmos.Piping.Unary.Components;

/// <summary>
/// Used for an entity that converts units of reagent into moles of gas.
/// </summary>
[RegisterComponent]
[Access(typeof(GasEvaporatorSystem))]
public sealed partial class GasEvaporatorComponent : Component
{
    /// <summary>
    /// The ID for the pipe node.
    /// </summary>
    [DataField]
    public string Outlet = "pipe";

    /// <summary>
    /// The ID for the solution.
    /// </summary>
    [DataField]
    public string SolutionId = "tank";

    /// <summary>
    /// The solution that reagents are evaporated from.
    /// </summary>
    [ViewVariables]
    public Entity<SolutionComponent>? Solution = null;

    /// <summary>
    /// For an evaporator, how many moles of gas are given per each U of reagent.
    /// </summary>
    /// <remarks>
    /// This is the inverse of MolesToReagentMultiplier in GasCondenserComponent.
    /// 1 / 0.2137 = 4.679
    /// </remarks>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ReagentToMolesMultiplier = 4.679f;
}
