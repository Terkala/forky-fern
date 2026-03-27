using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;

namespace Content.Server.Chemistry.Components;

/// <summary>
/// When this entity hits a mob while thrown (throw collision / ThrowDoHit),
/// builds a one-shot solution from <see cref="Reagents"/> and injects it into the target's bloodstream.
/// Runs before cream-pie splat when ordered accordingly on the system subscription.
/// </summary>
[RegisterComponent]
public sealed partial class SolutionInjectOnHitComponent : Component
{
    /// <summary>
    /// Reagents to mix into a temporary solution and inject. Serialized like other solutions (ReagentId + Quantity).
    /// </summary>
    [DataField]
    public List<ReagentQuantity> Reagents = new();

    /// <summary>
    /// Fraction of the built solution volume that enters the bloodstream; the rest is discarded.
    /// </summary>
    [DataField]
    public float TransferEfficiency = 1f;

    /// <summary>
    /// If false, targets wearing an outer hardsuit tagged Hardsuit are not injected.
    /// </summary>
    [DataField]
    public bool PierceArmor = true;

    /// <summary>
    /// If any of these slots have an equipped item, injection is skipped for that target.
    /// </summary>
    [DataField]
    public SlotFlags BlockSlots = SlotFlags.NONE;

    /// <summary>
    /// Shown to the thrower when injection fails due to a hardsuit (see <see cref="PierceArmor"/>).
    /// Passed values: $weapon and $target
    /// </summary>
    [DataField]
    public LocId BlockedByHardsuitPopupMessage = "melee-inject-failed-hardsuit";
}
