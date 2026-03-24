using Content.Shared.Emp;
using Robust.Shared.Map;

namespace Content.Shared._CE.Actions.Spells;

/// <summary>
/// Runs <see cref="SharedEmpSystem.EmpPulse"/> at the spell target (or world position).
/// Pulse radius, drain strength, and disable duration are independent YAML fields — unlike <c>!type:Emp</c>
/// entity effects, where range is derived from <c>rangeModifier * scale</c> capped by <c>maxRange</c>.
/// </summary>
public sealed partial class CESpellEmpPulse : CESpellEffect
{
    /// <summary>
    /// Pulse radius in meters (passed to entity lookup). Independent of <see cref="EnergyConsumption"/> and <see cref="Duration"/>.
    /// </summary>
    [DataField]
    public float Range = 1.5f;

    /// <summary>
    /// Energy removed from affected sources, in joules (same units as stock EMP).
    /// </summary>
    [DataField]
    public float EnergyConsumption = 12500f;

    /// <summary>
    /// How long hit entities stay EMP-disabled when applicable.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(15);

    public override void Effect(EntityManager entManager, CESpellEffectBaseArgs args)
    {
        var xform = entManager.System<SharedTransformSystem>();
        var emp = entManager.System<SharedEmpSystem>();

        MapCoordinates coords;
        if (args.Target is { } target)
            coords = xform.GetMapCoordinates(target);
        else if (args.Position is { } pos)
            coords = xform.ToMapCoordinates(pos);
        else
            return;

        emp.EmpPulse(coords, Range, EnergyConsumption, Duration, args.User);
    }
}
