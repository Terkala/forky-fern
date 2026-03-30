using System.Numerics;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Funkystation.Projectile;

/// <summary>
/// Computes ricochet direction and applies velocity via <see cref="SharedGunSystem.ShootProjectile"/>.
/// </summary>
public sealed class SharedBouncingSpellProjectileSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <summary>
    /// Returns whether the projectile still has budget to ricochet (does not increment counter).
    /// </summary>
    public bool CanStillBounce(BouncingSpellProjectileComponent bounce)
    {
        return bounce.BounceCount < bounce.MaxBounces && _timing.CurTime < bounce.ExpireAt;
    }

    /// <summary>
    /// Applies the next segment velocity. Call only when <see cref="CanStillBounce"/> is true.
    /// </summary>
    public void ApplyBounce(EntityUid projUid,
        EntityUid hitUid,
        Vector2 incomingMapVelocity,
        BouncingSpellProjectileComponent bounce,
        ProjectileComponent projectile)
    {
        EntityUid? owner = null;
        if (TryGetEntity(bounce.Owner, out var ownerUid))
            owner = ownerUid;

        var isMob = _mobState.IsAlive(hitUid);
        var mapPos = _transform.GetMapCoordinates(projUid).Position;
        var dir = ResolveBounceDirection(projUid, hitUid, incomingMapVelocity, bounce, isMob, mapPos, owner);

        _gun.ShootProjectile(projUid, dir, Vector2.Zero, projectile.Weapon, user: owner, bounce.ProjectileSpeed);
    }

    private Vector2 ResolveBounceDirection(EntityUid projUid,
        EntityUid hitUid,
        Vector2 incomingMapVelocity,
        BouncingSpellProjectileComponent bounce,
        bool hitIsLivingMob,
        Vector2 projMapPos,
        EntityUid? owner)
    {
        if (!hitIsLivingMob || bounce.Mode == BouncingSpellProjectileMode.Random)
        {
            if (!hitIsLivingMob)
                return WallBandReflectDirection(incomingMapVelocity);

            return RandomUnitDirection();
        }

        // NearbyTarget + mob hit
        if (TryPickNearbyMobDirection(projUid, hitUid, projMapPos, bounce, owner, out var toTarget))
            return toTarget;

        return RandomUnitDirection();
    }

    private Vector2 WallBandReflectDirection(Vector2 incomingMapVelocity)
    {
        var vel = incomingMapVelocity.LengthSquared() < 1e-6f
            ? Vector2.UnitX
            : incomingMapVelocity.Normalized();

        var reverse = -vel;
        var baseAngle = reverse.ToWorldAngle();
        var min = baseAngle - MathF.PI / 6f; // 30°
        var max = baseAngle + MathF.PI / 6f;
        var t = _random.NextFloat();
        var turn = new Angle(min + (max - min) * t);
        return turn.ToWorldVec();
    }

    private Vector2 RandomUnitDirection()
    {
        var a = _random.NextFloat() * MathF.Tau;
        return new Vector2(MathF.Cos(a), MathF.Sin(a));
    }

    private bool TryPickNearbyMobDirection(EntityUid projUid,
        EntityUid hitUid,
        Vector2 projMapPos,
        BouncingSpellProjectileComponent bounce,
        EntityUid? owner,
        out Vector2 direction)
    {
        direction = Vector2.Zero;
        var coords = Transform(projUid).Coordinates;
        var ents = _lookup.GetEntitiesInRange(coords, bounce.TargetSearchRadius, LookupFlags.Uncontained);

        EntityUid? best = null;
        var bestDistSq = float.MaxValue;

        foreach (var uid in ents)
        {
            if (uid == projUid || uid == hitUid)
                continue;

            if (owner is { } o && uid == o)
                continue;

            if (!_mobState.IsAlive(uid))
                continue;

            if (Deleted(uid) || TerminatingOrDeleted(uid))
                continue;

            var otherPos = _transform.GetMapCoordinates(uid).Position;
            var delta = otherPos - projMapPos;
            var distSq = delta.LengthSquared();
            if (distSq < 0.001f)
                continue;

            if (bounce.UseLosFilter)
            {
                var xform = Transform(projUid);
                var origin = new MapCoordinates(projMapPos, xform.MapID);
                var dest = new MapCoordinates(otherPos, xform.MapID);
                if (!_interaction.InRangeUnobstructed(origin, dest, delta.Length() + 0.5f))
                    continue;
            }

            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = uid;
            }
            else if (MathF.Abs(distSq - bestDistSq) < 1e-6f && best is { } b && uid.CompareTo(b) < 0)
            {
                best = uid;
            }
        }

        if (best is not { } target)
            return false;

        var d = _transform.GetMapCoordinates(target).Position - projMapPos;
        if (d.LengthSquared() < 1e-6f)
            return false;

        direction = d.Normalized();
        return true;
    }
}
