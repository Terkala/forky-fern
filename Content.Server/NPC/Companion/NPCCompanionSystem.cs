using System.Linq;
using System.Numerics;
using Content.Server.NPC;
using Content.Server.NPC.Companion.Components;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Map;

namespace Content.Server.NPC.Companion;

/// <summary>
/// Core companion system. Handles proxy retaliation (companion attacks entities that attack the owner),
/// self-defense (companion becomes hostile to entities that damage it), and updates last known owner
/// position for off-grid tracking.
/// </summary>
public sealed class NPCCompanionSystem : EntitySystem
{
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly PathfindingSystem _pathfinding = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly NPCSystem _npc = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CompanionOwnerComponent, DamageChangedEvent>(OnOwnerDamaged);
        SubscribeLocalEvent<NPCCompanionComponent, DamageChangedEvent>(OnCompanionDamaged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NPCCompanionComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var companion, out var xform))
        {
            if (companion.Owner is not { } owner || !Exists(owner))
                continue;

            if (!TryComp(owner, out TransformComponent? ownerXform))
                continue;

            var ownerOnGrid = ownerXform.GridUid != null;
            if (ownerOnGrid)
            {
                companion.LastKnownOwnerPosition = _transform.GetMapCoordinates(owner, ownerXform);
                companion.OwnerWasOnGrid = true;
            }
            else
            {
                companion.OwnerWasOnGrid = false;
            }

            UpdateCombatDoorNav(uid);
        }
    }

    /// <summary>
    /// When the companion has hostiles (in combat), disable door interaction so it doesn't path through
    /// doors to acquire weapons mid-fight. Restore when combat ends.
    /// </summary>
    private void UpdateCombatDoorNav(EntityUid uid)
    {
        if (!TryComp<HTNComponent>(uid, out var htn))
            return;

        var hasHostiles = TryComp<FactionExceptionComponent>(uid, out var faction) &&
            faction.Hostiles.Count > 0 &&
            faction.Hostiles.Any(h => Exists(h) && !TerminatingOrDeleted(h));

        var hasHands = HasComp<HandsComponent>(uid);
        var allowDoors = !hasHostiles && hasHands;

        var blackboard = htn.Blackboard;
        var currentAllowDoors = blackboard.TryGetValue<bool>(NPCBlackboard.NavInteract, out var interact, EntityManager) && interact;

        if (allowDoors == currentAllowDoors)
            return;

        _npc.SetBlackboard(uid, NPCBlackboard.NavInteract, allowDoors, htn);
        _npc.SetBlackboard(uid, NPCBlackboard.NavPry, allowDoors, htn);
        _npc.SetBlackboard(uid, NPCBlackboard.NavSmash, allowDoors, htn);

        if (TryComp<NPCSteeringComponent>(uid, out var steering))
        {
            steering.PathfindToken?.Cancel();
            steering.PathfindToken = null;
            steering.CurrentPath.Clear();
            steering.Flags = _pathfinding.GetFlags(uid);
        }
    }

    private void OnOwnerDamaged(Entity<CompanionOwnerComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin is not { } attacker)
            return;

        if (!HasComp<MobStateComponent>(attacker))
            return;

        foreach (var companion in ent.Comp.Companions)
        {
            if (!Exists(companion) || TerminatingOrDeleted(companion))
                continue;

            if (_npcFaction.IsEntityFriendly(ent.Owner, attacker))
                continue;

            _npcFaction.AggroEntity(companion, attacker);
        }
    }

    private void OnCompanionDamaged(Entity<NPCCompanionComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin is not { } attacker)
            return;

        if (!HasComp<MobStateComponent>(attacker))
            return;

        var owner = ent.Comp.Owner;

        if (owner is { } ownerId && Exists(ownerId))
        {
            if (_npcFaction.IsEntityFriendly(ownerId, attacker))
                return;
        }

        _npcFaction.AggroEntity(ent.Owner, attacker);
    }

    /// <summary>
    /// Binds a companion to an owner. Call this when establishing the owner-companion relationship.
    /// </summary>
    public void BindCompanion(EntityUid owner, EntityUid companion)
    {
        var ownerComp = EnsureComp<CompanionOwnerComponent>(owner);
        var companionComp = EnsureComp<NPCCompanionComponent>(companion);

        companionComp.Owner = owner;
        ownerComp.Companions.Add(companion);

        _npc.SetBlackboard(companion, NPCBlackboard.FollowTarget, new EntityCoordinates(owner, Vector2.Zero));
    }

    /// <summary>
    /// Unbinds a companion from its owner.
    /// </summary>
    public void UnbindCompanion(EntityUid owner, EntityUid companion)
    {
        if (TryComp<CompanionOwnerComponent>(owner, out var ownerComp))
        {
            ownerComp.Companions.Remove(companion);
            if (ownerComp.Companions.Count == 0)
                RemComp<CompanionOwnerComponent>(owner);
        }

        if (TryComp<NPCCompanionComponent>(companion, out var companionComp) && companionComp.Owner == owner)
        {
            companionComp.Owner = null;
            RemComp<NPCCompanionComponent>(companion);
        }
    }
}
