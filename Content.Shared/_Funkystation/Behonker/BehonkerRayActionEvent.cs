using Content.Shared.Actions;
using Content.Shared.Damage;

namespace Content.Shared._Funkystation.Behonker;

/// <summary>
/// Elemental line attack used by mage-summoned Behonkers (world-target ray damage).
/// </summary>
public sealed partial class BehonkerRayActionEvent : WorldTargetActionEvent
{
    [DataField(required: true)]
    public DamageSpecifier Damage = default!;

    [DataField]
    public float Range = 12f;
}
