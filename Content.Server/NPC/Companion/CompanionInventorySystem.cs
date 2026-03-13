using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Hands.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Tools.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using EntityPrototype = Robust.Shared.Prototypes.EntityPrototype;

namespace Content.Server.NPC.Companion;

/// <summary>
/// Provides inventory management for companion NPCs. Handles storing and retrieving items
/// with sensible defaults (e.g. prefer belt for tools, scan belt and backpack for items).
/// </summary>
public sealed class CompanionInventorySystem : EntitySystem
{
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    /// <summary>
    /// Slots to check for storage, in order of preference for tools.
    /// Belt first (toolbelt), then back (backpack), then pockets.
    /// </summary>
    private static readonly string[] StorageSlotOrder = ["belt", "back", "pocket1", "pocket2", "suitstorage"];

    /// <summary>
    /// Attempts to store an item. Prefers belt for tools, otherwise tries belt, backpack, pockets.
    /// If the item is held, it will be dropped from hands before storing.
    /// </summary>
    /// <param name="companion">The companion entity.</param>
    /// <param name="item">The item to store.</param>
    /// <param name="preferBeltForTools">If true, tools (items with ToolComponent) are preferentially stored on belt.</param>
    /// <returns>True if the item was stored successfully.</returns>
    public bool TryStore(EntityUid companion, EntityUid item, bool preferBeltForTools = true)
    {
        if (!HasComp<HandsComponent>(companion) || !HasComp<InventoryComponent>(companion))
            return false;

        EnsureItemNotInHands(companion, item);

        var isTool = HasComp<ToolComponent>(item);
        var slotsToTry = preferBeltForTools && isTool
            ? StorageSlotOrder
            : ["back", "belt", "pocket1", "pocket2", "suitstorage"];

        foreach (var slot in slotsToTry)
        {
            if (!_inventory.HasSlot(companion, slot))
                continue;

            if (_inventory.TryGetSlotEntity(companion, slot, out var slotEntity))
            {
                var slotEnt = slotEntity.Value;
                if (TryComp<StorageComponent>(slotEnt, out var storage) &&
                    _storage.CanInsert(slotEnt, item, out _, storage))
                {
                    return _storage.Insert(slotEnt, item, out _, user: companion, storageComp: storage, playSound: false);
                }
            }

            if (!_inventory.TryGetSlotContainer(companion, slot, out var container, out _))
                continue;

            if (container.ContainedEntity != null)
                continue;

            return _inventory.TryEquip(companion, companion, item, slot, silent: true, force: true, checkDoafter: false);
        }

        return false;
    }

    private void EnsureItemNotInHands(EntityUid companion, EntityUid item)
    {
        if (_hands.IsHolding(companion, item))
            _hands.TryDrop(companion, item);
        else if (_container.TryGetContainingContainer(item, out var container))
            _container.Remove(item, container, reparent: true);
    }

    /// <summary>
    /// Attempts to retrieve an item matching the filter and put it in the companion's active hand.
    /// Searches belt (and its storage), backpack (and its storage), pockets, suit storage.
    /// </summary>
    /// <param name="companion">The companion entity.</param>
    /// <param name="filter">Filter for the item (prototype ID or tag).</param>
    /// <param name="item">The retrieved item if successful.</param>
    /// <returns>True if an item was found and equipped.</returns>
    public bool TryRetrieve(EntityUid companion, CompanionItemFilter filter, [NotNullWhen(true)] out EntityUid? item)
    {
        item = null;
        if (!HasComp<HandsComponent>(companion) || !HasComp<InventoryComponent>(companion))
            return false;

        if (!TryFindItem(companion, filter, out item))
            return false;

        var itemVal = item.Value;
        if (_hands.IsHolding(companion, itemVal))
        {
            item = itemVal;
            return true;
        }

        if (_container.TryGetContainingContainer(itemVal, out var container))
            _container.Remove(itemVal, container, reparent: true);

        _transform.DropNextTo(itemVal, companion);

        if (!_hands.TryPickupAnyHand(companion, itemVal, checkActionBlocker: false))
            return false;

        item = itemVal;
        return true;
    }

    /// <summary>
    /// Finds an item in the companion's inventory (belt, backpack, pockets) matching the filter.
    /// Does not equip it.
    /// </summary>
    public bool TryFindItem(EntityUid companion, CompanionItemFilter filter, [NotNullWhen(true)] out EntityUid? item)
    {
        item = null;
        foreach (var ent in EnumerateInventoryItems(companion))
        {
            if (filter.Matches(ent, EntityManager, _proto, _tag))
            {
                item = ent;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the companion has an item matching the filter.
    /// </summary>
    public bool HasItem(EntityUid companion, CompanionItemFilter filter)
    {
        return TryFindItem(companion, filter, out _);
    }

    /// <summary>
    /// Enumerates all items in the companion's inventory: belt, back, pockets, suit storage,
    /// and recursively inside any storage (toolbelt, backpack).
    /// </summary>
    public IEnumerable<EntityUid> EnumerateInventoryItems(EntityUid companion)
    {
        if (!TryComp<InventoryComponent>(companion, out var inv))
            yield break;

        foreach (var slot in inv.Slots)
        {
            if (!_container.TryGetContainer(companion, slot.Name, out var container))
                continue;

            foreach (var child in container.ContainedEntities)
            {
                foreach (var item in EnumerateItemsRecursive(child))
                    yield return item;
            }
        }
    }

    private IEnumerable<EntityUid> EnumerateItemsRecursive(EntityUid entity)
    {
        yield return entity;

        if (!TryComp<StorageComponent>(entity, out var storage))
            yield break;

        foreach (var child in storage.Container.ContainedEntities)
        {
            foreach (var item in EnumerateItemsRecursive(child))
                yield return item;
        }
    }
}

/// <summary>
/// Filter for finding items in companion inventory. Supports prototype ID or tag matching.
/// </summary>
public readonly struct CompanionItemFilter
{
    public readonly ProtoId<EntityPrototype>? PrototypeId;
    public readonly ProtoId<TagPrototype>? TagId;

    public CompanionItemFilter(ProtoId<EntityPrototype>? prototypeId = null, ProtoId<TagPrototype>? tagId = null)
    {
        PrototypeId = prototypeId;
        TagId = tagId;
    }

    public static CompanionItemFilter ByPrototype(ProtoId<EntityPrototype> id) => new(prototypeId: id);
    public static CompanionItemFilter ByTag(ProtoId<TagPrototype> id) => new(tagId: id);

    public bool Matches(EntityUid entity, IEntityManager entMan, IPrototypeManager proto, TagSystem tag)
    {
        if (PrototypeId is { } pid)
        {
            var entityProtoId = entMan.GetComponent<MetaDataComponent>(entity).EntityPrototype?.ID;
            if (string.IsNullOrEmpty(entityProtoId))
                return false;
            if (entityProtoId != pid.Id && !proto.EnumerateParents<EntityPrototype>(entityProtoId, includeSelf: true).Any(p => p.ID == pid.Id))
                return false;
        }

        if (TagId is { } tid && !tag.HasTag(entity, tid))
            return false;

        return true;
    }
}
