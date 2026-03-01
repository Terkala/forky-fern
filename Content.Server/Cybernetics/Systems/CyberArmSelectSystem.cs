using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Events;
using Content.Shared.Cybernetics.Systems;
using Content.Shared.Cybernetics.UI;
using Content.Shared.Hands.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Power.Components;
using Robust.Server.GameObjects;

namespace Content.Server.Cybernetics.Systems;

public sealed class CyberArmSelectSystem : EntitySystem
{
    [Dependency] private readonly SharedCyberArmStorageSystem _cyberArmStorage = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandsComponent, EmptyHandActivateEvent>(OnEmptyHandActivateRef);
        Subs.BuiEvents<CyberLimbComponent>(CyberArmSelectUiKey.Key, sub => sub.Event<CyberArmSelectRequestMessage>(OnCyberArmSelectRequest));
    }

    private void OnEmptyHandActivateRef(Entity<HandsComponent> ent, ref EmptyHandActivateEvent ev)
    {
        if (ev.Handled)
            return;

        if (!HasComp<BodyComponent>(ev.User))
            return;

        var items = _cyberArmStorage.GetCyberArmStorageItems(ev.User)
            .Where(x => !HasComp<CyberLimbModuleComponent>(x.Item) && !HasComp<BatteryComponent>(x.Item))
            .ToList();
        if (items.Count == 0)
            return;

        var firstArmWithItems = items[0].Limb;
        if (!_ui.HasUi(firstArmWithItems, CyberArmSelectUiKey.Key))
            return;

        var state = new CyberArmSelectBoundUserInterfaceState(
            items.Select(x => new CyberArmSelectItemEntry(GetNetEntity(x.Item), Identity.Name(x.Item, EntityManager))).ToList());

        if (_ui.TryOpenUi(firstArmWithItems, CyberArmSelectUiKey.Key, ev.User))
        {
            _ui.SetUiState(firstArmWithItems, CyberArmSelectUiKey.Key, state);
            ev.Handled = true;
        }
    }

    private void OnCyberArmSelectRequest(Entity<CyberLimbComponent> ent, ref CyberArmSelectRequestMessage msg)
    {
        var user = msg.Actor;
        if (user == default)
            return;

        var selectedNet = msg.SelectedItem;
        if (!TryGetEntity(selectedNet, out var selectedEntity))
            return;

        var items = _cyberArmStorage.GetCyberArmStorageItems(user)
            .Where(x => !HasComp<CyberLimbModuleComponent>(x.Item) && !HasComp<BatteryComponent>(x.Item))
            .ToList();
        if (!items.Any(x => x.Item == selectedEntity))
            return;

        if (_virtualItem.TrySpawnVirtualItemInHand(selectedEntity.Value, user, out var virtualItem, false, null, false))
        {
            EnsureComp<CyberArmVirtualItemComponent>(virtualItem.Value);
            EnsureComp<UnremoveableComponent>(virtualItem.Value);
            _ui.CloseUi(ent.Owner, CyberArmSelectUiKey.Key, user);
        }
    }
}
