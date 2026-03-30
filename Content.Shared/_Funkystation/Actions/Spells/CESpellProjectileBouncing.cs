using System.Numerics;
using Content.Shared._CE.Actions.Spells;
using Content.Shared._Funkystation.Projectile;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Funkystation.Actions.Spells;

/// <summary>
/// Launches a projectile and configures <see cref="BouncingSpellProjectileComponent"/> for ricochets.
/// </summary>
public sealed partial class CESpellProjectileBouncing : CESpellEffect
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public float ProjectileSpeed = 20f;

    [DataField]
    public float Spread;

    [DataField]
    public int ProjectileCount = 1;

    [DataField]
    public bool SaveVelocity;

    [DataField]
    public int MaxBounces = 10;

    [DataField]
    public float MaxDurationSeconds = 3f;

    [DataField]
    public BouncingSpellProjectileMode BounceMode = BouncingSpellProjectileMode.NearbyTarget;

    [DataField]
    public float TargetSearchRadius = 10f;

    [DataField]
    public float DamageFalloffPerBounce = 1f;

    [DataField]
    public bool UseLosFilter;

    public override void Effect(EntityManager entManager, CESpellEffectBaseArgs args)
    {
        EntityCoordinates? targetPoint = null;

        if (args.Target is not null &&
            entManager.TryGetComponent<TransformComponent>(args.Target.Value, out var transformComponent))
            targetPoint = transformComponent.Coordinates;
        else if (args.Position is not null)
            targetPoint = args.Position;

        if (targetPoint is null || args.User is null)
            return;

        var transform = entManager.System<SharedTransformSystem>();
        var physics = entManager.System<SharedPhysicsSystem>();
        var gunSystem = entManager.System<SharedGunSystem>();
        var mapManager = IoCManager.Resolve<IMapManager>();
        var random = IoCManager.Resolve<IRobustRandom>();
        var timing = IoCManager.Resolve<IGameTiming>();

        if (!entManager.TryGetComponent<TransformComponent>(args.User.Value, out var xform))
            return;

        var fromCoords = xform.Coordinates;
        var userVelocity = physics.GetMapLinearVelocity(args.User.Value);

        var fromMap = transform.ToMapCoordinates(fromCoords);

        var spawnCoords = mapManager.TryFindGridAt(fromMap, out var gridUid, out _)
            ? transform.WithEntityId(fromCoords, gridUid)
            : new EntityCoordinates(mapManager.GetMapEntityId(fromMap.MapId), fromMap.Position);

        for (var i = 0; i < ProjectileCount; i++)
        {
            var offsetedTargetPoint = targetPoint.Value.Offset(new Vector2(
                (float) (random.NextDouble() * 2 - 1) * Spread,
                (float) (random.NextDouble() * 2 - 1) * Spread));

            if (fromCoords == offsetedTargetPoint)
                continue;

            var ent = entManager.PredictedSpawnAtPosition(Prototype, spawnCoords);

            var bounce = entManager.EnsureComponent<BouncingSpellProjectileComponent>(ent);
            bounce.Mode = BounceMode;
            bounce.MaxBounces = MaxBounces;
            bounce.BounceCount = 0;
            bounce.ExpireAt = timing.CurTime + TimeSpan.FromSeconds(MaxDurationSeconds);
            bounce.ProjectileSpeed = ProjectileSpeed;
            bounce.TargetSearchRadius = TargetSearchRadius;
            bounce.DamageFalloffPerBounce = DamageFalloffPerBounce;
            bounce.UseLosFilter = UseLosFilter;
            bounce.Owner = entManager.GetNetEntity(args.User.Value);
            entManager.Dirty(ent, bounce);

            var direction = offsetedTargetPoint.ToMapPos(entManager, transform) -
                            spawnCoords.ToMapPos(entManager, transform);

            gunSystem.ShootProjectile(ent, direction, SaveVelocity ? userVelocity : new Vector2(), args.User.Value, args.User, ProjectileSpeed);
        }
    }
}
