using Content.Server.Body.Systems;
using Content.Server.Chemistry.Components;
using Content.Server.Nutrition.EntitySystems;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Robust.Shared.Prototypes;

namespace Content.Server.Chemistry.EntitySystems;

/// <summary>
/// Handles <see cref="SolutionInjectOnHitComponent"/> when a thrown entity collides with a mob.
/// </summary>
public sealed class SolutionInjectOnHitSystem : EntitySystem
{
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> HardsuitTag = "Hardsuit";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SolutionInjectOnHitComponent, ThrowDoHitEvent>(OnThrowDoHit,
            before: [typeof(CreamPieSystem)]);
    }

    private void OnThrowDoHit(Entity<SolutionInjectOnHitComponent> injector, ref ThrowDoHitEvent args)
    {
        var comp = injector.Comp;
        var target = args.Target;

        if (Deleted(target) || comp.Reagents.Count == 0)
            return;

        if (!TryComp<BloodstreamComponent>(target, out var bloodstream))
            return;

        var efficiency = Math.Clamp(comp.TransferEfficiency, 0f, 1f);

        if (!comp.PierceArmor
            && _inventory.TryGetSlotEntity(target, "outerClothing", out var suit)
            && _tag.HasTag(suit.Value, HardsuitTag))
        {
            var thrower = args.Component.Thrower;
            if (thrower != null)
            {
                _popup.PopupEntity(
                    Loc.GetString(comp.BlockedByHardsuitPopupMessage, ("weapon", injector.Owner), ("target", target)),
                    target,
                    thrower.Value,
                    PopupType.SmallCaution);
            }

            return;
        }

        if (comp.BlockSlots != SlotFlags.NONE)
        {
            var blocked = false;
            var slots = _inventory.GetSlotEnumerator(target, comp.BlockSlots);
            while (slots.MoveNext(out var container))
            {
                if (container.ContainedEntity != null)
                {
                    blocked = true;
                    break;
                }
            }

            if (blocked)
                return;
        }

        var inject = new Solution();
        foreach (var rq in comp.Reagents)
        {
            if (rq.Quantity <= FixedPoint2.Zero)
                continue;

            inject.AddReagent(rq);
        }

        if (inject.Volume <= FixedPoint2.Zero)
            return;

        inject = inject.SplitSolution(inject.Volume * efficiency);
        if (inject.Volume <= FixedPoint2.Zero)
            return;

        _bloodstream.TryAddToBloodstream((target, bloodstream), inject);
    }
}
