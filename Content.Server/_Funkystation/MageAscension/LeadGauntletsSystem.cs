using Content.Shared._CE.MageAscension;
using Content.Shared.Access.Systems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;

namespace Content.Server._Funkystation.MageAscension;

public sealed class LeadGauntletsSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LeadGauntletsComponent, BeingUnequippedAttemptEvent>(OnUnequipAttempt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = AllEntityQuery<LeadGauntletsComponent, TransformComponent>();
        while (query.MoveNext(out var glovesUid, out _, out var xform))
        {
            var wearer = xform.ParentUid;
            if (!wearer.IsValid())
                continue;

            if (!_inventory.TryGetContainingSlot(glovesUid, out var slot) || slot.Name != "gloves")
                continue;

            if (!TryComp<BatteryComponent>(wearer, out var battery))
                continue;

            if (battery.LastCharge <= 0f)
                continue;

            _battery.SetCharge((wearer, battery), 0f);
        }
    }

    private void OnUnequipAttempt(Entity<LeadGauntletsComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        if (_access.IsAllowed(args.Unequipee, ent.Owner))
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString("lead-gauntlets-strip-denied"), args.Unequipee, args.Unequipee);
    }
}
