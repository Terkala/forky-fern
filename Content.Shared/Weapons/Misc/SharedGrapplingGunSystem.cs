// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024-2025 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Roudenn <149893554+Roudenn@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 AJCM-git <60196617+AJCM-git@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Sir Warock <67167466+SirWarock@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 PJB3005 <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2025 Vasilis The Pikachu <vasilis@pikachu.systems>
// SPDX-FileCopyrightText: 2025 slarticodefast <161409025+slarticodefast@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Hayden <banditoz@protonmail.com>
// SPDX-FileCopyrightText: 2026 āda <ss.adasts@gmail.com>
// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Numerics;
using Content.Shared.CombatMode;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.Weapons.Misc;

public abstract class SharedGrapplingGunSystem : VirtualController
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public const string GrapplingJoint = "grappling";

    /// <summary>
    /// Collision mask for rope path raycasts: anchored pathing-blockers including walls (Opaque), excluding players (MidImpassable).
    /// </summary>
    private const int RopePathCollisionMask = (int) (CollisionGroup.Opaque | CollisionGroup.Impassable | CollisionGroup.HighImpassable | CollisionGroup.LowImpassable);

    /// <summary>
    /// Minimum time between full path recomputations when the rope is bent.
    /// </summary>
    private static readonly TimeSpan PathUpdateInterval = TimeSpan.FromSeconds(0.1);

    /// <summary>
    /// Maximum waypoints for the bent rope path (prevents infinite loops on concave geometry).
    /// </summary>
    private const int MaxPathWaypoints = 10;

    /// <summary>
    /// Extra angle (radians) past 180 degrees required before un-anchoring from a corner.
    /// Prevents instant un-anchor from jitter or floating-point error.
    /// </summary>
    private const float UnAnchorAngleLeeway = 0.5f;

    private List<Vector2> _pathBuffer = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<GrapplingProjectileComponent, ProjectileEmbedEvent>(OnGrappleCollide);
        SubscribeLocalEvent<GrapplingProjectileComponent, JointRemovedEvent>(OnGrappleJointRemoved);
        SubscribeLocalEvent<CanWeightlessMoveEvent>(OnWeightlessMove);
        SubscribeAllEvent<RequestGrapplingReelMessage>(OnGrapplingReel);

        // TODO: After step trigger refactor, dropping a grappling gun should manually try and activate step triggers it's suppressing.
        SubscribeLocalEvent<GrapplingGunComponent, GunShotEvent>(OnGrapplingShot);
        SubscribeLocalEvent<GrapplingGunComponent, ActivateInWorldEvent>(OnGunActivate);
        SubscribeLocalEvent<GrapplingGunComponent, HandDeselectedEvent>(OnGrapplingDeselected);

        UpdatesBefore.Add(typeof(SharedJointSystem)); // We want to run before joints are solved
        base.Initialize();
    }

    private void OnGrappleJointRemoved(EntityUid uid, GrapplingProjectileComponent component, JointRemovedEvent args)
    {
        if (_netManager.IsServer)
            QueueDel(uid);
    }

    private void OnGrapplingShot(EntityUid uid, GrapplingGunComponent component, ref GunShotEvent args)
    {
        foreach (var (shotUid, _) in args.Ammo)
        {
            if (!HasComp<GrapplingProjectileComponent>(shotUid))
                continue;

            //todo: this doesn't actually support multigrapple
            // Rope rendering is handled by GrapplingRopeOverlay on the client (gun-centric, supports PVS-culled hook and bent paths).
            component.Projectile = shotUid.Value;
            DirtyField(uid, component, nameof(GrapplingGunComponent.Projectile));
        }

        TryComp<AppearanceComponent>(uid, out var appearance);
        _appearance.SetData(uid, SharedTetherGunSystem.TetherVisualsStatus.Key, false, appearance);
    }

    private void OnGrapplingDeselected(EntityUid uid, GrapplingGunComponent component, HandDeselectedEvent args)
    {
        SetReeling(uid, component, false, args.User);
    }

    private void OnGrapplingReel(RequestGrapplingReelMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        if (!_hands.TryGetActiveItem(player, out var activeItem) ||
            !TryComp<GrapplingGunComponent>(activeItem, out var grappling))
        {
            return;
        }

        if (msg.Reeling &&
            (!TryComp<CombatModeComponent>(player, out var combatMode) ||
             !combatMode.IsInCombatMode))
        {
            return;
        }

        SetReeling(activeItem.Value, grappling, msg.Reeling, player);
    }

    private void OnWeightlessMove(ref CanWeightlessMoveEvent ev)
    {
        if (ev.CanMove || !TryComp<JointRelayTargetComponent>(ev.Uid, out var relayComp))
            return;

        foreach (var relay in relayComp.Relayed)
        {
            if (TryComp<JointComponent>(relay, out var jointRelay) && jointRelay.GetJoints.ContainsKey(GrapplingJoint))
            {
                ev.CanMove = true;
                return;
            }
        }
    }

    /// <summary>
    /// Ungrapples the grappling hook, destroying the hook and severing the joint
    /// </summary>
    /// <param name="grapple">Entity for the grappling gun</param>
    /// <param name="isBreak">Whether to play the sound for the rope breaking</param>
    /// <param name="user">The user responsible for the ungrapple. Optional</param>
    public void Ungrapple(Entity<GrapplingGunComponent> grapple, bool isBreak, EntityUid? user = null)
    {
        if (!Timing.IsFirstTimePredicted || grapple.Comp.Projectile is not { } projectile)
            return;

        if(isBreak)
            _audio.PlayPredicted(grapple.Comp.BreakSound, grapple.Owner, user);

        _appearance.SetData(grapple.Owner, SharedTetherGunSystem.TetherVisualsStatus.Key, true);

        if (_netManager.IsServer)
            QueueDel(projectile);

        SetReeling(grapple.Owner, grapple.Comp, false, user);
        grapple.Comp.Projectile = null;
        grapple.Comp.RopeEndPosition = null;
        grapple.Comp.AnchorAngle = null;
        grapple.Comp.RopePath.Clear();
        DirtyField(grapple.Owner, grapple.Comp, nameof(GrapplingGunComponent.Projectile));
        DirtyField(grapple.Owner, grapple.Comp, nameof(GrapplingGunComponent.RopeEndPosition));
        DirtyField(grapple.Owner, grapple.Comp, nameof(GrapplingGunComponent.RopePath));
        _gun.ChangeBasicEntityAmmoCount(grapple.Owner, 1);
    }

    private void OnGunActivate(EntityUid uid, GrapplingGunComponent component, ActivateInWorldEvent args)
    {
        if (!Timing.IsFirstTimePredicted || args.Handled || !args.Complex)
            return;

        _audio.PlayPredicted(component.CycleSound, uid, args.User);
        Ungrapple((uid, component), false, args.User);

        args.Handled = true;
    }

    private void SetReeling(EntityUid uid, GrapplingGunComponent component, bool value, EntityUid? user)
    {
        if (TryComp<JointComponent>(uid, out var jointComp) &&
            jointComp.GetJoints.TryGetValue(GrapplingJoint, out var joint) &&
            joint is DistanceJoint distance)
        {
            if (distance.MaxLength <= distance.MinLength + component.RopeFullyReeledMargin)
                value = false;
        }

        if (component.Reeling == value)
            return;

        if (value)
        {
            // We null-coalesce here because playing the sound again will cause it to become eternally stuck playing
            component.Stream ??= _audio.PlayPredicted(component.ReelSound, uid, user)?.Entity;
        }
        else if (!value && component.Stream.HasValue && Timing.IsFirstTimePredicted)
        {
            // The IsFirstTimePredicted check is important here because otherwise component.Stream will be set to null from an early cancellation if this isn't FirstTimePredicted
            component.Stream = _audio.Stop(component.Stream);
        }

        component.Reeling = value;

        DirtyField(uid, component, nameof(GrapplingGunComponent.Reeling));
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        var query = EntityQueryEnumerator<GrapplingGunComponent, JointComponent>();

        while (query.MoveNext(out var uid, out var grappling, out var jointComp))
        {
            if (!jointComp.GetJoints.TryGetValue(GrapplingJoint, out var joint) ||
                joint is not DistanceJoint distance ||
                !_entities.TryGetComponent<JointComponent>(joint.BodyAUid, out var hookJointComp))
            {
                if (_netManager.IsServer) // Client might not receive the joint due to PVS culling, so lets not spam them with 23895739 mispredicted ungrapples
                    Ungrapple((uid, grappling), true);
                continue;
            }

            // If the joint breaks, it gets disabled
            if (distance.Enabled == false)
            {
                Ungrapple((uid, grappling), true);
                continue;
            }

            var physicalGrapple = jointComp.Relay.HasValue ? jointComp.Relay.Value : joint.BodyBUid;
            var physicalHook = hookJointComp.Relay.HasValue ? hookJointComp.Relay.Value : joint.BodyAUid;

            // HACK: preventing both ends of the grappling hook from sleeping if neither are on the same grid, so that grid movement works as expected
            if (_transform.GetGrid(physicalHook) != _transform.GetGrid(physicalGrapple))
            {
                _physics.WakeBody(physicalHook);
                _physics.WakeBody(physicalGrapple);
            }
            // END OF HACK

            var bodyAWorldPos = _transform.GetWorldPosition(physicalHook);
            var bodyBWorldPos = _transform.GetWorldPosition(physicalGrapple);
            var gunWorldPos = _transform.GetWorldPosition(uid);

            // Update RopeEndPosition for client rendering when hook may be PVS-culled.
            // Only dirty when position changes significantly to reduce network jitter.
            var newRopeEnd = bodyAWorldPos;
            var ropeEndChanged = !grappling.RopeEndPosition.HasValue ||
                (grappling.RopeEndPosition.Value - newRopeEnd).LengthSquared() > 0.04f; // ~0.2m threshold
            grappling.RopeEndPosition = newRopeEnd;
            if (ropeEndChanged)
                DirtyField(uid, grappling, nameof(GrapplingGunComponent.RopeEndPosition));

            // Compute rope path from GUN to hook (rope is attached to gun, not player center).
            // Run on both server and client so client has path for overlay; server is authoritative for physics.
            var mapId = _transform.GetMapId(uid);
            var (pathLength, pathChanged) = ComputeRopePath(uid, grappling, gunWorldPos, bodyAWorldPos, mapId);

            var straightDist = (bodyAWorldPos - gunWorldPos).Length();
            var effectivePathLength = pathLength ?? straightDist;

            // The solver does not handle setting the rope's length, but we still need to work with a copy of it to prevent jank.
            var ropeLength = (bodyAWorldPos - bodyBWorldPos).Length();

            // Set MaxLength to path length each tick to allow spooling out when not reeling.
            // Never set below current rope length to prevent instant snap from path computation errors.
            distance.MaxLength = MathF.Max(effectivePathLength + grappling.RopeMargin, ropeLength + grappling.RopeMargin);

            if (pathChanged && !prediction)
                DirtyField(uid, grappling, nameof(GrapplingGunComponent.RopePath));

            // Rope should just break, instantly, if the user is teleported past its max length
            if (ropeLength >= distance.MaxLength + grappling.RopeMargin)
            {
                Ungrapple((uid, grappling), true);
                continue;
            }

            if (!grappling.Reeling)
            {
                // Just in case.
                if (grappling.Stream.HasValue && Timing.IsFirstTimePredicted)
                    grappling.Stream = _audio.Stop(grappling.Stream);

                continue;
            }


            // TODO: Contracting DistanceJoints should be in engine
            if (distance.MaxLength >= ropeLength + grappling.RopeMargin)
            {
                distance.MaxLength = MathF.Max(distance.MinLength + grappling.RopeMargin, distance.MaxLength - grappling.ReelRate * frameTime);
                distance.MaxLength = MathF.Max(ropeLength + grappling.RopeMargin, distance.MaxLength);
                ropeLength = MathF.Min(distance.MaxLength, ropeLength);

                distance.Length = ropeLength;
            }

            if (ropeLength <= distance.MinLength + grappling.RopeFullyReeledMargin)
            {
                SetReeling(uid, grappling, false, null);
            }
            else if (ropeLength >= distance.MaxLength - grappling.RopeMargin)
            {
                // Pull toward the closest waypoint on the path (bent point), not straight toward the hook
                var pullTarget = GetClosestWaypointToPlayer(grappling.RopePath, bodyBWorldPos, bodyAWorldPos);
                var targetDirection = (pullTarget - bodyBWorldPos).Normalized();

                var grapplerUidA = _container.TryGetOuterContainer(physicalHook, Transform(physicalHook), out var containerA) ? containerA.Owner : physicalHook;
                var grapplerBodyA = Comp<PhysicsComponent>(grapplerUidA);

                var massFactorA = MathF.Min(grapplerBodyA.InvMass * grappling.ReelMassCoefficient, 1f);
                _physics.ApplyLinearImpulse(grapplerUidA, targetDirection * grappling.ReelForce * massFactorA * frameTime * -1, body: grapplerBodyA);

                var grapplerUidB = _container.TryGetOuterContainer(physicalGrapple, Transform(physicalGrapple), out var containerB) ? containerB.Owner : physicalGrapple;
                var grapplerBodyB = Comp<PhysicsComponent>(grapplerUidB);

                var massFactorB = MathF.Min(grapplerBodyB.InvMass * grappling.ReelMassCoefficient, 1f);
                _physics.ApplyLinearImpulse(grapplerUidB, targetDirection * grappling.ReelForce * massFactorB * frameTime, body: grapplerBodyB);
            }

            Dirty(uid, jointComp);
        }
    }

    /// <summary>
    /// Checks whether the entity is hooked to something via grappling gun.
    /// </summary>
    /// <param name="entity">Entity to check.</param>
    /// <returns>True if hooked, false otherwise.</returns>
    public bool IsEntityHooked(Entity<JointRelayTargetComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return false;

        foreach (var uid in entity.Comp.Relayed)
        {
            if (HasComp<GrapplingGunComponent>(uid))
                return true;
        }

        return false;
    }

    private void OnGrappleCollide(EntityUid uid, GrapplingProjectileComponent component, ref ProjectileEmbedEvent args)
    {
        if (!Timing.IsFirstTimePredicted || !args.Weapon.HasValue || !_entities.TryGetComponent<GrapplingGunComponent>(args.Weapon, out var grapple))
            return;

        var grapplePos = _transform.GetWorldPosition(args.Weapon.Value);
        var hookPos = _transform.GetWorldPosition(uid);
        if ((grapplePos - hookPos).Length() >= grapple.RopeMaxLength)
        {
            Ungrapple((args.Weapon.Value, grapple), true);
            return;
        }

        var joint = _joints.CreateDistanceJoint(uid, args.Weapon.Value, id: GrapplingJoint);
        joint.MaxLength = joint.Length + grapple.RopeMargin;
        joint.Stiffness = grapple.RopeStiffness;
        joint.MinLength = grapple.RopeMinLength; // Length of a tile to prevent pulling yourself into / through walls
        joint.Breakpoint = grapple.RopeBreakPoint;

        var jointCompHook = _entities.GetComponent<JointComponent>(uid); // we use get here because if the component doesn't exist then something has fucked up bigtime
        var jointCompGrapple = _entities.GetComponent<JointComponent>(args.Weapon.Value);

        _joints.SetRelay(uid, args.Embedded, jointCompHook);
        _joints.RefreshRelay(args.Weapon.Value, jointCompGrapple);
    }

    /// <summary>
    /// Computes the rope path from gun to hook, updating the component's RopePath.
    /// Fast path: raycast gun→hook; if clear, path is straight.
    /// Full path: when hit, iterative raycasts around obstacles (throttled).
    /// </summary>
    /// <returns>Path length and whether the path was updated (for dirtying), or (null, false) if computation failed.</returns>
    private (float? Length, bool PathChanged) ComputeRopePath(EntityUid gunUid, GrapplingGunComponent grappling, Vector2 gunPos, Vector2 hookPos, MapId mapId)
    {
        var projectile = grappling.Projectile;
        if (projectile is null)
            return (null, false);

        var dir = hookPos - gunPos;
        var straightDist = dir.Length();
        if (straightDist < 0.001f)
            return (0f, false);

        // Exclude gun, projectile, mobs, and the entity the hook is embedded in (the wall at the endpoint).
        // Without excluding the embedded entity, we hit it at the ray end, then hit it again when iterating,
        // triggering the "concave corner" fallback and forcing a straight path.
        var embeddedInto = CompOrNull<EmbeddableProjectileComponent>(projectile)?.EmbeddedIntoUid;
        var ray = new CollisionRay(gunPos, dir / straightDist, RopePathCollisionMask);
        var predicate = (EntityUid uid, (EntityUid Gun, EntityUid? Projectile, EntityUid? EmbeddedInto) state) =>
            uid == state.Gun || uid == state.Projectile || uid == state.EmbeddedInto || HasComp<MobStateComponent>(uid);

        var results = _physics.IntersectRayWithPredicate(mapId, ray, (gunUid, projectile, embeddedInto), predicate, straightDist, returnOnFirstHit: true);
        var firstHit = results.FirstOrNull();

        if (!firstHit.HasValue)
        {
            // Straight ray is clear. If we have an anchored bent path, stay anchored until player has swung
            // more than 180 degrees around the corner from when the bend was created.
            if (grappling.RopePath.Count > 2 && grappling.AnchorAngle.HasValue)
            {
                var corner = grappling.RopePath[1];
                var toGun = gunPos - corner;
                if (toGun.LengthSquared() > 0.0001f)
                {
                    var currentAngle = MathF.Atan2(toGun.Y, toGun.X);
                    var angleDiff = currentAngle - grappling.AnchorAngle.Value;
                    // Normalize to [-Pi, Pi]
                    while (angleDiff > MathF.PI) angleDiff -= 2 * MathF.PI;
                    while (angleDiff < -MathF.PI) angleDiff += 2 * MathF.PI;

                    // Un-anchor when player has swung more than 180° + leeway around the corner
                    if (MathF.Abs(angleDiff) < MathF.PI + UnAnchorAngleLeeway)
                    {
                        // Update endpoints to current positions; keep corner waypoints fixed
                        grappling.RopePath[0] = gunPos;
                        grappling.RopePath[^1] = hookPos;

                        var anchoredPathLength = 0f;
                        for (var i = 0; i < grappling.RopePath.Count - 1; i++)
                            anchoredPathLength += (grappling.RopePath[i + 1] - grappling.RopePath[i]).Length();

                        return (anchoredPathLength, true); // pathChanged: endpoints updated
                    }
                }
                grappling.AnchorAngle = null;
            }

            // Re-straighten: no anchor or player has swung past 180 degrees
            var pathChanged = grappling.RopePath.Count != 2 ||
                (grappling.RopePath.Count >= 2 && ((grappling.RopePath[0] - gunPos).LengthSquared() > 0.01f || (grappling.RopePath[1] - hookPos).LengthSquared() > 0.01f));
            grappling.AnchorAngle = null;
            grappling.RopePath.Clear();
            grappling.RopePath.Add(gunPos);
            grappling.RopePath.Add(hookPos);
            return (straightDist, pathChanged);
        }

        // Hit something - run full iterative path (throttled)
        if (Timing.CurTime < grappling.NextPathUpdate)
        {
            // Use cached path; compute length from existing path
            if (grappling.RopePath.Count >= 2)
            {
                var len = 0f;
                for (var i = 0; i < grappling.RopePath.Count - 1; i++)
                    len += (grappling.RopePath[i + 1] - grappling.RopePath[i]).Length();
                return (len, false);
            }
            return (straightDist, false);
        }

        grappling.NextPathUpdate = Timing.CurTime + PathUpdateInterval;

        _pathBuffer.Clear();
        _pathBuffer.Add(gunPos);

        var from = gunPos;
        var target = hookPos;
        var seenEntities = new HashSet<EntityUid>();
        var iter = 0;

        while (iter++ < MaxPathWaypoints)
        {
            var toTarget = target - from;
            var dist = toTarget.Length();
            if (dist < 0.001f)
                break;

            ray = new CollisionRay(from, toTarget / dist, RopePathCollisionMask);
            results = _physics.IntersectRayWithPredicate(mapId, ray, (gunUid, projectile, embeddedInto), predicate, dist, returnOnFirstHit: true);
            firstHit = results.FirstOrNull();

            if (!firstHit.HasValue)
            {
                _pathBuffer.Add(target);
                break;
            }

            var hit = firstHit.Value;
            if (seenEntities.Add(hit.HitEntity))
            {
                _pathBuffer.Add(hit.HitPos);
                from = hit.HitPos;
                // Offset along hit normal to avoid immediate re-hit (approximate - we don't have normal, use small offset toward target)
                from += toTarget.Normalized() * 0.25f; // Offset past surface to avoid immediate re-hit
            }
            else
            {
                // Concave corner - fall back to straight path
                _pathBuffer.Clear();
                _pathBuffer.Add(gunPos);
                _pathBuffer.Add(hookPos);
                grappling.AnchorAngle = null;
                grappling.RopePath.Clear();
                grappling.RopePath.AddRange(_pathBuffer);
                return (straightDist, true);
            }
        }

        var hadBentPath = grappling.RopePath.Count > 2;
        grappling.RopePath.Clear();
        grappling.RopePath.AddRange(_pathBuffer);

        // Store angle from corner to gun only when first creating a bend (straight -> bent transition).
        // Do not overwrite on rebuild, so the reference angle stays fixed.
        if (grappling.RopePath.Count > 2 && !hadBentPath)
        {
            var corner = grappling.RopePath[1];
            var toGun = gunPos - corner;
            if (toGun.LengthSquared() > 0.0001f)
                grappling.AnchorAngle = MathF.Atan2(toGun.Y, toGun.X);
        }

        var pathLength = 0f;
        for (var i = 0; i < grappling.RopePath.Count - 1; i++)
            pathLength += (grappling.RopePath[i + 1] - grappling.RopePath[i]).Length();

        return (pathLength, true);
    }

    /// <summary>
    /// Returns the closest waypoint on the path to the player for pull direction.
    /// When path is straight, returns the hook position.
    /// </summary>
    private static Vector2 GetClosestWaypointToPlayer(List<Vector2> path, Vector2 playerPos, Vector2 hookPos)
    {
        if (path.Count == 0)
            return hookPos;

        var closest = hookPos;
        var minDistSq = (hookPos - playerPos).LengthSquared();

        foreach (var wp in path)
        {
            var d = (wp - playerPos).LengthSquared();
            if (d < minDistSq)
            {
                minDistSq = d;
                closest = wp;
            }
        }

        return closest;
    }

    [Serializable, NetSerializable]
    protected sealed class RequestGrapplingReelMessage : EntityEventArgs
    {
        public bool Reeling;

        public RequestGrapplingReelMessage(bool reeling)
        {
            Reeling = reeling;
        }
    }
}
