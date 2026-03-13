using System.Numerics;
using Content.Server.NPC.Companion.Components;
using Content.Server.NPC.Systems;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Timing;

namespace Content.Server.NPC.Companion;

/// <summary>
/// When the owner goes off-grid, the companion travels to their last known position,
/// then either jetpacks toward them or waits and defends.
/// </summary>
public sealed class CompanionOffGridSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly SharedJetpackSystem _jetpack = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private const float ArriveRange = 1.5f;
    private const float OffGridMoveSpeed = 1f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NPCCompanionComponent, InputMoverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var companion, out var mover, out var xform))
        {
            if (companion.Owner is not { } owner || !Exists(owner))
                continue;

            if (!TryComp(owner, out TransformComponent? ownerXform))
                continue;

            // Owner must be off-grid for us to act.
            if (ownerXform.GridUid != null)
                continue;

            // Need a valid last known position (owner was on grid at some point).
            if (companion.LastKnownOwnerPosition == default)
                continue;

            var companionMapPos = _transform.GetMapCoordinates(uid, xform);
            var ownerMapPos = _transform.GetMapCoordinates(owner, ownerXform);

            // Companion is on grid: pathfind to last known position.
            if (xform.GridUid != null)
            {
                var lastKnownCoords = _transform.ToCoordinates(companion.LastKnownOwnerPosition);
                var distToLastKnown = (companionMapPos.Position - companion.LastKnownOwnerPosition.Position).Length();

                if (distToLastKnown > ArriveRange)
                {
                    // Pathfind to last known position.
                    _steering.TryRegister(uid, lastKnownCoords);
                }
                else
                {
                    // At last known position. If we have a jetpack, enable it and move off-grid.
                    if (TryGetJetpack(uid, out var jetpackUid, out var jetpackComp) && jetpackUid is { } jp && jetpackComp is { } jc)
                    {
                        _steering.Unregister(uid);
                        _jetpack.SetEnabled(jp, jc, true, uid);
                    }
                    // No jetpack: stay (steering will keep us at last known; combat/defense continues via HTN).
                }
            }
            else
            {
                // Companion is off-grid: move directly toward owner.
                _steering.Unregister(uid);

                var dir = ownerMapPos.Position - companionMapPos.Position;
                var length = dir.Length();
                if (length > 0.1f)
                {
                    var wishDir = dir / length;
                    mover.CurTickSprintMovement = wishDir * OffGridMoveSpeed;
                    mover.LastInputTick = _timing.CurTick;
                    mover.LastInputSubTick = ushort.MaxValue;
                    Dirty(uid, mover);
                }

                // Keep jetpack enabled if we have one.
                if (TryGetJetpack(uid, out var jetpackUid, out var jetpackComp) && jetpackUid is { } jp && jetpackComp is { } jc && !_jetpack.IsUserFlying(uid))
                {
                    _jetpack.SetEnabled(jp, jc, true, uid);
                }
            }
        }
    }

    private bool TryGetJetpack(EntityUid uid, out EntityUid? jetpackUid, out JetpackComponent? jetpackComp)
    {
        jetpackUid = null;
        jetpackComp = null;

        if (!_inventory.TryGetSlotEntity(uid, "back", out var backEntity))
            return false;

        var back = backEntity!.Value;
        if (!TryComp(back, out jetpackComp) || jetpackComp == null)
            return false;

        jetpackUid = back;
        return true;
    }
}
