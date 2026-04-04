using Content.Server.CombatMode;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Blob;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Item;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Robust.Shared.Maths;
using Content.Shared.Mobs.Components;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;
using Robust.Shared.Utility;

namespace Content.Server.Blob;

public sealed class BlobSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ActionContainerSystem _actions = default!;
    [Dependency] private readonly SharedActionsSystem _sharedActions = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly CombatModeSystem _combatMode = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

    private static readonly ProtoId<TagPrototype> HighRiskTag = "HighRiskItem";

    private readonly Dictionary<EntityUid, TimeSpan> _genPenaltyUntil = new();

    private static readonly Vector2i[] Cardinals =
    [
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
    ];

    private static readonly EntProtoId[] PostDeployActions =
    [
        "ActionBlobSpread",
        "ActionBlobRepair",
        "ActionBlobConsume",
        "ActionBlobAbsorb",
        "ActionBlobPromoteNucleus",
        "ActionBlobDevourItem",
        "ActionBlobBuildBridge",
        "ActionBlobEvoGenRate",
        "ActionBlobEvoQuickSpread",
        "ActionBlobEvoSpreadChance",
        "ActionBlobEvoAttack",
        "ActionBlobEvoFireResist",
        "ActionBlobEvoPoisonResist",
        "ActionBlobEvoUnlockDevour",
        "ActionBlobEvoUnlockBridge",
        "ActionBlobEvoUnlockLauncher",
        "ActionBlobEvoUnlockPlasmaphyll",
        "ActionBlobEvoUnlockEctothermid",
        "ActionBlobEvoUnlockReflective",
    ];

    private const int StarterSpreadTileThreshold = 25;

    private static readonly EntProtoId BlobDeployActionProto = "ActionBlobDeploy";

    /// <summary>Chebyshev tile distance from target: a blob anchor must exist within this range on the same grid.</summary>
    private const int BlobAttackMaxChebyshev = 5;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobTileComponent, EntityTerminatingEvent>(OnBlobTileTerminating);

        SubscribeLocalEvent<BlobDeployActionEvent>(OnDeploy);
        SubscribeLocalEvent<BlobSpreadActionEvent>(OnSpread);
        SubscribeLocalEvent<BlobOvermindComponent, BlobPrimaryStrikeAttemptEvent>(OnPrimaryStrikeAttempt);
        SubscribeLocalEvent<BlobConsumeActionEvent>(OnConsume);
        SubscribeLocalEvent<BlobRepairActionEvent>(OnRepair);
        SubscribeLocalEvent<BlobAbsorbActionEvent>(OnAbsorb);
        SubscribeLocalEvent<BlobPromoteNucleusActionEvent>(OnPromote);
        SubscribeLocalEvent<BlobChangeColorActionEvent>(OnChangeColor);

        SubscribeLocalEvent<BlobDevourItemActionEvent>(OnDevour);
        SubscribeLocalEvent<BlobBuildBridgeActionEvent>(OnBuildBridge);

        SubscribeLocalEvent<BlobTileComponent, BlobTileSwapChoiceMessage>(OnBlobTileSwapChoice);
        SubscribeLocalEvent<BlobTileComponent, GetVerbsEvent<AlternativeVerb>>(OnBlobTileAltVerb);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var cur = _timing.CurTime;
        var query = EntityQueryEnumerator<BlobHiveComponent>();
        while (query.MoveNext(out var uid, out var hive))
        {
            if (!hive.Deployed)
                continue;

            var mult = hive.GenerationMultiplier;
            if (_genPenaltyUntil.TryGetValue(uid, out var until) && cur < until)
                mult *= 0.5f;

            var delta = (hive.GenerationPerSecond + hive.RibosomeGenerationBonus) * mult * frameTime;
            hive.BioPoints = Math.Min(hive.BioMax, hive.BioPoints + (int) float.Ceiling(delta));
            Dirty(uid, hive);
        }
    }

    private void OnBlobTileTerminating(Entity<BlobTileComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!TryGetEntity(ent.Comp.Hive, out var hiveEnt) || hiveEnt is not { } hiveUid)
            return;
        if (!TryComp<BlobHiveComponent>(hiveUid, out var hive))
            return;

        if (ent.Comp.Kind != BlobTileKind.Bridge)
            hive.TileCount = Math.Max(0, hive.TileCount - 1);

        if (ent.Comp.Kind == BlobTileKind.Nucleus)
            OnNucleusDestroyed(hiveUid, hive);

        Dirty(hiveUid, hive);
    }

    private void OnNucleusDestroyed(EntityUid hiveUid, BlobHiveComponent hive)
    {
        hive.LivingNuclei = Math.Max(0, hive.LivingNuclei - 1);
        hive.BioPoints = 0;
        _genPenaltyUntil[hiveUid] = _timing.CurTime + TimeSpan.FromSeconds(60);
    }

    private bool TryGetHive(EntityUid performer, out EntityUid hiveUid, [NotNullWhen(true)] out BlobHiveComponent? hive)
    {
        hiveUid = performer;
        if (!TryComp(performer, out hive) || !hive.Deployed)
        {
            hive = null;
            return false;
        }
        return true;
    }

    private static int GetMaxBlobTiles(BlobHiveComponent hive)
    {
        if (hive.MaxBlobTilesPerNucleus <= 0)
            return int.MaxValue;
        var nuclei = Math.Max(1, hive.LivingNuclei);
        return nuclei * hive.MaxBlobTilesPerNucleus;
    }

    private static bool IsAtBlobTileCap(BlobHiveComponent hive) =>
        hive.MaxBlobTilesPerNucleus > 0 && hive.TileCount >= GetMaxBlobTiles(hive);

    private bool TrySpendBio(EntityUid hiveUid, BlobHiveComponent hive, int amount)
    {
        if (hive.BioPoints < amount)
            return false;

        hive.BioPoints -= amount;
        Dirty(hiveUid, hive);
        return true;
    }

    private bool TryResolveTile(EntityCoordinates coords, out EntityUid gridUid, out MapGridComponent grid, out Vector2i indices)
    {
        gridUid = default;
        grid = default!;
        indices = default;

        var mapCoords = _xform.ToMapCoordinates(coords);
        if (!_mapManager.TryFindGridAt(mapCoords, out gridUid, out var gridNullable) || gridNullable is not { } gridResolved)
            return false;

        grid = gridResolved;
        indices = _map.TileIndicesFor(gridUid, grid, mapCoords);
        return true;
    }

    private IEnumerable<EntityUid> EntitiesOnTile(EntityUid gridUid, MapGridComponent grid, Vector2i indices, LookupFlags flags = LookupFlags.Uncontained)
    {
        var tileRef = _map.GetTileRef(gridUid, grid, indices);
        return _lookup.GetEntitiesInTile(tileRef, flags);
    }

    private EntityUid? GetBlobOnTile(EntityUid gridUid, MapGridComponent grid, Vector2i indices, NetEntity hiveNet)
    {
        foreach (var e in EntitiesOnTile(gridUid, grid, indices))
        {
            if (TryComp<BlobTileComponent>(e, out var bt) && bt.Hive == hiveNet)
                return e;
        }
        return null;
    }

    private bool CardinalAdjacentToBlob(EntityUid gridUid, MapGridComponent grid, Vector2i indices, NetEntity hiveNet)
    {
        foreach (var c in Cardinals)
        {
            var n = indices + c;
            if (GetBlobOnTile(gridUid, grid, n, hiveNet) != null)
                return true;
        }
        return false;
    }

    /// <summary>
    /// True if some hive blob tile is close enough and has a non-occluded ray to the target tile center (walls block).
    /// </summary>
    private bool CanBlobStrikeTile(
        NetEntity hiveNet,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i targetIndices,
        EntityUid performer)
    {
        var targetLocal = _map.GridTileToLocal(gridUid, grid, targetIndices);
        var targetMap = _xform.ToMapCoordinates(targetLocal);

        for (var dx = -BlobAttackMaxChebyshev; dx <= BlobAttackMaxChebyshev; dx++)
        {
            for (var dy = -BlobAttackMaxChebyshev; dy <= BlobAttackMaxChebyshev; dy++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) > BlobAttackMaxChebyshev)
                    continue;

                var anchor = targetIndices + new Vector2i(dx, dy);
                if (GetBlobOnTile(gridUid, grid, anchor, hiveNet) == null)
                    continue;

                var anchorLocal = _map.GridTileToLocal(gridUid, grid, anchor);
                var anchorMap = _xform.ToMapCoordinates(anchorLocal);

                bool Ignore(EntityUid uid)
                {
                    if (uid == performer)
                        return true;
                    return TryComp<BlobTileComponent>(uid, out var bt) && bt.Hive == hiveNet;
                }

                if (_interaction.InRangeUnobstructed(anchorMap, targetMap,
                        range: 0f,
                        collisionMask: SharedInteractionSystem.InRangeUnobstructedMask,
                        predicate: Ignore))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// One-time deploy: strip the action so the core cannot be placed again.
    /// </summary>
    private void RemoveDeployAction(EntityUid overmind)
    {
        if (!TryComp<ActionsContainerComponent>(overmind, out var containerComp))
            return;

        foreach (var actionId in containerComp.Container.ContainedEntities.ToArray())
        {
            if (MetaData(actionId).EntityPrototype?.ID != BlobDeployActionProto.Id)
                continue;

            if (!TryComp<ActionComponent>(actionId, out var actionComp))
                return;

            // Detaches from the action container and strips from the action bar; deletes if Temporary.
            _actions.RemoveAction((actionId, actionComp));
            return;
        }
    }

    private void GrantPostDeployActions(EntityUid overmind)
    {
        foreach (var id in PostDeployActions)
            _actions.AddAction(overmind, id.Id);

        if (TryComp<ActionsComponent>(overmind, out var actionsComp) &&
            TryComp<ActionsContainerComponent>(overmind, out var containerComp))
            _sharedActions.GrantContainedActions((overmind, actionsComp), (overmind, containerComp));
    }

    private void OnDeploy(BlobDeployActionEvent ev)
    {
        if (ev.Handled || !TryComp<BlobHiveComponent>(ev.Performer, out var hive))
            return;

        var hiveUid = ev.Performer;

        if (hive.Deployed)
        {
            _popup.PopupEntity(Loc.GetString("blob-already-deployed"), ev.Performer, ev.Performer);
            return;
        }

        if (!TryResolveTile(ev.Target, out var gridUid, out var grid, out var indices))
            return;

        var refTile = _map.GetTileRef(gridUid, grid, indices);
        if (refTile.Tile.IsEmpty || _turf.IsSpace(refTile))
        {
            _popup.PopupEntity(Loc.GetString("blob-deploy-invalid-tile"), ev.Performer, ev.Performer);
            return;
        }

        var hiveNet = GetNetEntity(hiveUid);
        foreach (var c in Cardinals)
        {
            var p = indices + c;
            if (GetBlobOnTile(gridUid, grid, p, hiveNet) != null)
            {
                _popup.PopupEntity(Loc.GetString("blob-deploy-too-close"), ev.Performer, ev.Performer);
                return;
            }
        }

        ev.Handled = true;

        hive.Deployed = true;
        hive.LivingNuclei = 1;
        hive.TileCount = 0;

        SpawnBlobTile("MobBlobTileNucleus", gridUid, grid, indices, hiveNet);
        foreach (var c in Cardinals)
            SpawnBlobTile("MobBlobTile", gridUid, grid, indices + c, hiveNet);

        RecountTiles(hiveUid, hive);

        RemoveDeployAction(ev.Performer);
        GrantPostDeployActions(ev.Performer);
        _combatMode.SetInCombatMode(ev.Performer, true);
        _popup.PopupEntity(Loc.GetString("blob-deploy-success"), ev.Performer, ev.Performer);
        Dirty(hiveUid, hive);
    }

    private void OnPrimaryStrikeAttempt(EntityUid uid, BlobOvermindComponent _, BlobPrimaryStrikeAttemptEvent ev)
    {
        if (!TryGetHive(uid, out var hiveUid, out var hive))
            return;

        var coords = GetCoordinates(ev.Coordinates);
        TryPrimaryBlobAttack(uid, hiveUid, hive, coords);
    }

    private void RecountTiles(EntityUid hiveUid, BlobHiveComponent hive)
    {
        var net = GetNetEntity(hiveUid);
        var n = 0;
        var q = EntityQueryEnumerator<BlobTileComponent>();
        while (q.MoveNext(out _, out var bt))
        {
            if (bt.Hive == net && bt.Kind != BlobTileKind.Bridge)
                n++;
        }
        hive.TileCount = n;
        hive.BioMax = 40 + n / 2;
        CheckEvoThreshold(hiveUid, hive);
        Dirty(hiveUid, hive);
    }

    private void CheckEvoThreshold(EntityUid hiveUid, BlobHiveComponent hive)
    {
        while (hive.TileCount >= hive.NextEvoAtTiles)
        {
            hive.EvoPoints += 1;
            hive.NextEvoAtTiles += 20;
        }
        Dirty(hiveUid, hive);
    }

    private void SpawnBlobTile(EntProtoId proto, EntityUid gridUid, MapGridComponent grid, Vector2i indices, NetEntity hiveNet)
    {
        var coords = _map.GridTileToLocal(gridUid, grid, indices);
        var ent = Spawn(proto, coords);
        var bt = Comp<BlobTileComponent>(ent);
        bt.Hive = hiveNet;
        Dirty(ent, bt);
    }

    private void OnSpread(BlobSpreadActionEvent ev)
    {
        if (ev.Handled || !TryGetHive(ev.Performer, out var hiveUid, out var hive))
            return;

        if (hive.TileCount >= StarterSpreadTileThreshold && _timing.CurTime < hive.NextSpreadAllowed)
        {
            _popup.PopupEntity(Loc.GetString("blob-spread-cooldown"), ev.Performer, ev.Performer);
            return;
        }

        if (IsAtBlobTileCap(hive))
        {
            _popup.PopupEntity(Loc.GetString("blob-spread-at-cap",
                ("current", hive.TileCount),
                ("max", GetMaxBlobTiles(hive))), ev.Performer, ev.Performer);
            return;
        }

        const int cost = 2;
        if (!TrySpendBio(hiveUid, hive, cost))
        {
            _popup.PopupEntity(Loc.GetString("blob-not-enough-bio"), ev.Performer, ev.Performer);
            return;
        }

        if (!TryResolveTile(ev.Target, out var gridUid, out var grid, out var indices))
            return;

        var hiveNet = GetNetEntity(hiveUid);
        if (GetBlobOnTile(gridUid, grid, indices, hiveNet) != null)
            return;

        if (!CardinalAdjacentToBlob(gridUid, grid, indices, hiveNet))
        {
            _popup.PopupEntity(Loc.GetString("blob-spread-not-adjacent"), ev.Performer, ev.Performer);
            return;
        }

        var refTile = _map.GetTileRef(gridUid, grid, indices);
        if (refTile.Tile.IsEmpty || _turf.IsSpace(refTile))
        {
            if (!hive.UnlockBridge)
            {
                _hiveRefundBio(hiveUid, hive, cost);
                _popup.PopupEntity(Loc.GetString("blob-spread-space"), ev.Performer, ev.Performer);
                return;
            }

            if (!_tileDef.TryGetDefinition("FloorSteel", out var floorDef))
            {
                _hiveRefundBio(hiveUid, hive, cost);
                return;
            }

            _map.SetTile(gridUid, grid, indices, new Tile(floorDef.TileId));
        }

        ev.Handled = true;
        SpawnBlobTile("MobBlobTile", gridUid, grid, indices, hiveNet);
        RecountTiles(hiveUid, hive);

        if (hive.TileCount >= StarterSpreadTileThreshold)
        {
            var cd = Math.Max(1f, 3f - hive.QuickSpreadPurchases);
            hive.NextSpreadAllowed = _timing.CurTime + TimeSpan.FromSeconds(cd);
            Dirty(hiveUid, hive);
        }

        // Spread-upgrade bonus rolls (simplified)
        var bonusChance = 0.4f * hive.SpreadChancePurchases;
        while (bonusChance > 0 && _random.Prob(MathF.Min(1f, bonusChance)))
        {
            bonusChance -= 1f;
            if (!TryBonusSpread(hiveUid, hive, gridUid, grid, hiveNet))
                break;
        }
    }

    private void _hiveRefundBio(EntityUid hiveUid, BlobHiveComponent hive, int cost)
    {
        hive.BioPoints += cost;
        Dirty(hiveUid, hive);
    }

    private bool TryBonusSpread(EntityUid hiveUid, BlobHiveComponent hive, EntityUid gridUid, MapGridComponent grid, NetEntity hiveNet)
    {
        var candidates = new List<Vector2i>();
        var q = EntityQueryEnumerator<BlobTileComponent>();
        while (q.MoveNext(out var touid, out var bt))
        {
            if (bt.Hive != hiveNet || bt.Kind == BlobTileKind.Bridge)
                continue;

            var tileXform = Transform(touid);
            if (tileXform.GridUid != gridUid)
                continue;

            var idx = _map.TileIndicesFor(gridUid, grid, tileXform.Coordinates);
            foreach (var c in Cardinals)
            {
                var n = idx + c;
                if (GetBlobOnTile(gridUid, grid, n, hiveNet) != null)
                    continue;
                var tr = _map.GetTileRef(gridUid, grid, n);
                if (tr.Tile.IsEmpty || _turf.IsSpace(tr))
                    continue;
                candidates.Add(n);
            }
        }

        if (candidates.Count == 0)
            return false;

        if (IsAtBlobTileCap(hive))
            return false;

        var pick = _random.Pick(candidates);
        if (!TrySpendBio(hiveUid, hive, 2))
            return false;

        SpawnBlobTile("MobBlobTile", gridUid, grid, pick, hiveNet);
        RecountTiles(hiveUid, hive);
        return true;
    }

    /// <summary>Primary fire (melee/light attack) while deployed: damage on clicked tile plus lash damage from nearby normal tiles.</summary>
    private void TryPrimaryBlobAttack(EntityUid performer, EntityUid hiveUid, BlobHiveComponent hive, EntityCoordinates targetCoords)
    {
        if (!TryResolveTile(targetCoords, out var gridUid, out var grid, out var indices))
            return;

        var hiveNet = GetNetEntity(hiveUid);
        if (!CanBlobStrikeTile(hiveNet, gridUid, grid, indices, performer))
            return;

        if (!TrySpendBio(hiveUid, hive, 1))
            return;

        var dmg = new DamageSpecifier();
        dmg.DamageDict["Blunt"] = 12 + 4 * hive.AttackPurchases;

        foreach (var e in EntitiesOnTile(gridUid, grid, indices, LookupFlags.Uncontained))
        {
            if (HasComp<BlobTileComponent>(e))
                continue;
            _damageable.TryChangeDamage(e, dmg, origin: performer);
        }

        var originMap = _xform.ToMapCoordinates(_map.GridTileToLocal(gridUid, grid, indices));
        var lash = new DamageSpecifier();
        lash.DamageDict["Blunt"] = 6;
        var lashRadius = 1.5f * grid.TileSize;
        var lashRadiusSq = lashRadius * lashRadius;

        EntityUid? nearestLashTile = null;
        var nearestSq = float.MaxValue;
        var q = EntityQueryEnumerator<BlobTileComponent, TransformComponent>();
        while (q.MoveNext(out var uid, out var bt, out var xform))
        {
            if (bt.Hive != hiveNet || bt.Kind != BlobTileKind.Normal)
                continue;
            var pos = _xform.GetMapCoordinates(uid, xform);
            var d2 = (pos.Position - originMap.Position).LengthSquared();
            if (d2 > lashRadiusSq)
                continue;
            if (d2 < nearestSq)
            {
                nearestSq = d2;
                nearestLashTile = uid;
            }
        }

        if (nearestLashTile is { } tileUid)
        {
            var tx = Transform(tileUid);
            if (tx.GridUid != null)
            {
                var tIdx = _map.TileIndicesFor(tx.GridUid.Value, Comp<MapGridComponent>(tx.GridUid.Value), tx.Coordinates);
                var tGrid = Comp<MapGridComponent>(tx.GridUid.Value);
                foreach (var e in EntitiesOnTile(tx.GridUid.Value, tGrid, tIdx, LookupFlags.Uncontained))
                {
                    if (HasComp<BlobTileComponent>(e))
                        continue;
                    _damageable.TryChangeDamage(e, lash, origin: performer);
                }
            }

            RaiseNetworkEvent(new PlayBlobLashWiggleEvent(GetNetEntity(tileUid)),
                Filter.Pvs(tileUid, entityManager: EntityManager));
        }

        Dirty(hiveUid, hive);
    }

    private void OnConsume(BlobConsumeActionEvent ev)
    {
        if (ev.Handled || !TryGetHive(ev.Performer, out var hiveUid, out var hive))
            return;

        if (!TrySpendBio(hiveUid, hive, 10))
            return;

        if (!TryResolveTile(ev.Target, out var gridUid, out var grid, out var indices))
            return;

        var hiveNet = GetNetEntity(hiveUid);
        var existing = GetBlobOnTile(gridUid, grid, indices, hiveNet);
        if (existing == null || existing.Value == EntityUid.Invalid)
            return;

        if (Comp<BlobTileComponent>(existing.Value).Kind == BlobTileKind.Nucleus)
        {
            _hiveRefundBio(hiveUid, hive, 10);
            return;
        }

        ev.Handled = true;
        QueueDel(existing.Value);
        hive.BioPoints += 4;
        RecountTiles(hiveUid, hive);
    }

    private void OnRepair(BlobRepairActionEvent ev)
    {
        if (ev.Handled || !TryGetHive(ev.Performer, out var hiveUid, out var hive))
            return;

        if (!TrySpendBio(hiveUid, hive, 1))
            return;

        if (!TryResolveTile(ev.Target, out var gridUid, out var grid, out var indices))
            return;

        var hiveNet = GetNetEntity(hiveUid);
        var existing = GetBlobOnTile(gridUid, grid, indices, hiveNet);
        if (existing == null || !TryComp<DamageableComponent>(existing, out var dmg))
            return;

        ev.Handled = true;
        var heal = new DamageSpecifier();
        heal.DamageDict["Blunt"] = -20;
        _damageable.TryChangeDamage(existing.Value, heal);
        Dirty(hiveUid, hive);
    }

    private void OnAbsorb(BlobAbsorbActionEvent ev)
    {
        if (ev.Handled || !TryGetHive(ev.Performer, out var hiveUid, out var hive))
            return;

        if (!TryResolveTile(ev.Target, out var gridUid, out var grid, out var indices))
            return;

        var hiveNet = GetNetEntity(hiveUid);
        if (GetBlobOnTile(gridUid, grid, indices, hiveNet) == null)
            return;

        var lethal = new DamageSpecifier();
        lethal.DamageDict["Blunt"] = 200;

        ev.Handled = true;
        foreach (var e in EntitiesOnTile(gridUid, grid, indices, LookupFlags.Uncontained))
        {
            if (!HasComp<HumanoidProfileComponent>(e) && !HasComp<MobStateComponent>(e))
                continue;
            _damageable.TryChangeDamage(e, lethal, origin: ev.Performer);
            hive.EvoPoints += TryComp<HumanoidProfileComponent>(e, out _) ? 2 : 1;
            break;
        }
        Dirty(hiveUid, hive);
    }

    private void OnPromote(BlobPromoteNucleusActionEvent ev)
    {
        if (ev.Handled || !TryGetHive(ev.Performer, out var hiveUid, out var hive))
            return;

        if (!TryResolveTile(ev.Target, out var gridUid, out var grid, out var indices))
            return;

        var hiveNet = GetNetEntity(hiveUid);
        var existing = GetBlobOnTile(gridUid, grid, indices, hiveNet);
        if (existing == null || Comp<BlobTileComponent>(existing.Value).Kind != BlobTileKind.Normal)
            return;

        if (hive.LivingNuclei >= 6)
        {
            _popup.PopupEntity(Loc.GetString("blob-nucleus-cap"), ev.Performer, ev.Performer);
            return;
        }

        ev.Handled = true;
        QueueDel(existing.Value);
        SpawnBlobTile("MobBlobTileNucleus", gridUid, grid, indices, hiveNet);
        hive.LivingNuclei++;
        RecountTiles(hiveUid, hive);
    }

    private void OnChangeColor(BlobChangeColorActionEvent ev)
    {
        if (ev.Handled || !TryComp<BlobHiveComponent>(ev.Performer, out var hive))
            return;

        ev.Handled = true;
        var hue = _random.NextFloat();
        hive.Tint = Color.FromHsv(new Vector4(hue, 0.55f, 0.85f, 1f));
        Dirty(ev.Performer, hive);
    }

    private void OnBlobTileSwapChoice(Entity<BlobTileComponent> ent, ref BlobTileSwapChoiceMessage args)
    {
        TryApplySpecialistConversion(args.Actor, ent.Owner, args.Kind);
    }

    private void OnBlobTileAltVerb(EntityUid uid, BlobTileComponent tile, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!HasComp<BlobOvermindComponent>(args.User))
            return;

        if (!TryComp<BlobHiveComponent>(args.User, out var hive) || !hive.Deployed)
            return;

        if (tile.Kind != BlobTileKind.Normal)
            return;

        if (tile.Hive != GetNetEntity(args.User))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("blob-verb-specialist-menu"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
            Act = () => _uiSystem.OpenUi(uid, BlobTileSwapUiKey.Key, args.User),
            Priority = 1,
        });
    }

    /// <summary>
    /// Converts a normal blob tile to a specialist type (radial menu / tests).
    /// </summary>
    public bool TryApplySpecialistConversion(EntityUid overmind, EntityUid normalBlobTile, BlobTileKind targetKind)
    {
        if (!TryGetHive(overmind, out var hiveUid, out var hive))
            return false;

        if (!TryComp<BlobTileComponent>(normalBlobTile, out var bt))
            return false;

        if (bt.Hive != GetNetEntity(hiveUid) || bt.Kind != BlobTileKind.Normal)
            return false;

        if (!BlobSpecialistRecipes.TryGetRecipe(targetKind, out var recipe))
            return false;

        if (BlobSpecialistRecipes.IsMenuOptionLocked(hive, targetKind))
        {
            _popup.PopupEntity(Loc.GetString("blob-locked"), overmind, overmind);
            return false;
        }

        return TryConvertNormalBlobTile(overmind, hiveUid, hive, normalBlobTile, targetKind, recipe.EntityPrototype, recipe.BioCost);
    }

    private bool TryConvertNormalBlobTile(EntityUid performer, EntityUid hiveUid, BlobHiveComponent hive,
        EntityUid normalTileUid, BlobTileKind kind, EntProtoId newProto, int bioCost)
    {
        if (!TrySpendBio(hiveUid, hive, bioCost))
        {
            _popup.PopupEntity(Loc.GetString("blob-not-enough-bio"), performer, performer);
            return false;
        }

        var coords = Transform(normalTileUid).Coordinates;
        if (!TryResolveTile(coords, out var gridUid, out var grid, out var indices))
        {
            _hiveRefundBio(hiveUid, hive, bioCost);
            return false;
        }

        var hiveNet = GetNetEntity(hiveUid);
        var existing = GetBlobOnTile(gridUid, grid, indices, hiveNet);
        if (existing != normalTileUid || Comp<BlobTileComponent>(normalTileUid).Kind != BlobTileKind.Normal)
        {
            _hiveRefundBio(hiveUid, hive, bioCost);
            return false;
        }

        QueueDel(normalTileUid);
        var ent = Spawn(newProto, coords);
        var newBt = Comp<BlobTileComponent>(ent);
        newBt.Hive = hiveNet;
        newBt.Kind = kind;
        Dirty(ent, newBt);

        if (kind == BlobTileKind.Ribosome)
        {
            hive.RibosomeGenerationBonus += 0.15f;
            Dirty(hiveUid, hive);
        }

        RecountTiles(hiveUid, hive);
        return true;
    }

    private void OnDevour(BlobDevourItemActionEvent ev)
    {
        if (ev.Handled || !TryGetHive(ev.Performer, out var hiveUid, out var hive))
            return;

        if (!hive.UnlockDevour)
        {
            _popup.PopupEntity(Loc.GetString("blob-locked"), ev.Performer, ev.Performer);
            return;
        }

        if (!TrySpendBio(hiveUid, hive, 3))
            return;

        if (!TryResolveTile(ev.Target, out var gridUid, out var grid, out var indices))
            return;

        var hiveNet = GetNetEntity(hiveUid);
        if (GetBlobOnTile(gridUid, grid, indices, hiveNet) == null
            && !CardinalAdjacentToBlob(gridUid, grid, indices, hiveNet))
        {
            _hiveRefundBio(hiveUid, hive, 3);
            return;
        }

        ev.Handled = true;
        foreach (var e in EntitiesOnTile(gridUid, grid, indices, LookupFlags.Uncontained).ToList())
        {
            if (!HasComp<ItemComponent>(e))
                continue;
            if (_tag.HasTag(e, HighRiskTag))
                continue;
            QueueDel(e);
            break;
        }
        Dirty(hiveUid, hive);
    }

    private void OnBuildBridge(BlobBuildBridgeActionEvent ev)
    {
        if (ev.Handled || !TryGetHive(ev.Performer, out var hiveUid, out var hive))
            return;

        if (!hive.UnlockBridge)
        {
            _popup.PopupEntity(Loc.GetString("blob-locked"), ev.Performer, ev.Performer);
            return;
        }

        if (!TrySpendBio(hiveUid, hive, 5))
            return;

        if (!TryResolveTile(ev.Target, out var gridUid, out var grid, out var indices))
            return;

        if (!CardinalAdjacentToBlob(gridUid, grid, indices, GetNetEntity(hiveUid)))
        {
            _hiveRefundBio(hiveUid, hive, 5);
            return;
        }

        var refTile = _map.GetTileRef(gridUid, grid, indices);
        if (!_turf.IsSpace(refTile))
        {
            _hiveRefundBio(hiveUid, hive, 5);
            return;
        }

        if (!_tileDef.TryGetDefinition("FloorSteel", out var floorDef))
        {
            _hiveRefundBio(hiveUid, hive, 5);
            return;
        }

        ev.Handled = true;
        _map.SetTile(gridUid, grid, indices, new Tile(floorDef.TileId));
        Dirty(hiveUid, hive);
    }
}
