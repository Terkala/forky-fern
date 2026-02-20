using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Events;
using Content.Shared.Hands.Components;
using Content.Shared.Cybernetics.UI;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Cybernetics.Systems;

public sealed class CyberArmSelectSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly ProtoId<OrganCategoryPrototype> ArmLeft = "ArmLeft";
    private static readonly ProtoId<OrganCategoryPrototype> ArmRight = "ArmRight";

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

        var items = GetCyberArmStorageItems(ev.User).ToList();
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

        var items = GetCyberArmStorageItems(user).ToList();
        if (!items.Any(x => x.Item == selectedEntity))
            return;

        if (_virtualItem.TrySpawnVirtualItemInHand(selectedEntity.Value, user, out var virtualItem, false, null, false))
        {
            EnsureComp<UnremoveableComponent>(virtualItem.Value);
            _ui.CloseUi(ent.Owner, CyberArmSelectUiKey.Key, user);
        }
    }

    private IEnumerable<(EntityUid Limb, EntityUid Item)> GetCyberArmStorageItems(EntityUid body)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp) || bodyComp.Organs == null)
            yield break;

        foreach (var organ in _body.GetAllOrgans(body))
        {
            if (!HasComp<CyberLimbComponent>(organ))
                continue;

            if (!TryComp<OrganComponent>(organ, out var organComp))
                continue;

            if (organComp.Category != ArmLeft && organComp.Category != ArmRight)
                continue;

            if (!TryComp<StorageComponent>(organ, out var storage) || storage.Container == null)
                continue;

            foreach (var item in storage.Container.ContainedEntities)
            {
                yield return (organ, item);
            }
        }
    }
}
