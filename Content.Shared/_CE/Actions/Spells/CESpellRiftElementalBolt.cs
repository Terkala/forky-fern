using System.Numerics;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._CE.Actions.Spells;

/// <summary>
/// Fires the elemental rift horror's bolt; projectile type depends on <see cref="MageRiftElementalVariantComponent.Current"/>.
/// </summary>
public sealed partial class CESpellRiftElementalBolt : CESpellEffect
{
    [DataField]
    public EntProtoId FireProto = "CEProjectileRiftElementalHorrorFire";

    [DataField]
    public EntProtoId AirProto = "CEProjectileRiftElementalHorrorAir";

    [DataField]
    public EntProtoId EarthProto = "CEProjectileRiftElementalHorrorEarth";

    [DataField]
    public EntProtoId WaterProto = "CEProjectileRiftElementalHorrorWater";

    [DataField]
    public float ProjectileSpeed = 12f;

    [DataField]
    public float Spread;

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

        if (!entManager.TryGetComponent<TransformComponent>(args.User.Value, out var xform))
            return;

        EntProtoId proto;
        if (entManager.TryGetComponent<MageRiftElementalVariantComponent>(args.User.Value, out var variant))
        {
            proto = variant.Current switch
            {
                MageRiftElementKind.Fire => FireProto,
                MageRiftElementKind.Air => AirProto,
                MageRiftElementKind.Earth => EarthProto,
                MageRiftElementKind.Water => WaterProto,
                _ => FireProto
            };
        }
        else
        {
            proto = FireProto;
        }

        var transform = entManager.System<SharedTransformSystem>();
        var physics = entManager.System<SharedPhysicsSystem>();
        var gunSystem = entManager.System<SharedGunSystem>();
        var mapManager = IoCManager.Resolve<IMapManager>();
        var random = IoCManager.Resolve<IRobustRandom>();

        var fromCoords = xform.Coordinates;
        var userVelocity = physics.GetMapLinearVelocity(args.User.Value);

        var fromMap = transform.ToMapCoordinates(fromCoords);

        var spawnCoords = mapManager.TryFindGridAt(fromMap, out var gridUid, out _)
            ? transform.WithEntityId(fromCoords, gridUid)
            : new(mapManager.GetMapEntityId(fromMap.MapId), fromMap.Position);

        var offsetedTargetPoint = targetPoint.Value.Offset(new Vector2(
            (float)(random.NextDouble() * 2 - 1) * Spread,
            (float)(random.NextDouble() * 2 - 1) * Spread));

        if (fromCoords == offsetedTargetPoint)
            return;

        var ent = entManager.PredictedSpawnAtPosition(proto, spawnCoords);

        var direction = offsetedTargetPoint.ToMapPos(entManager, transform) -
                        spawnCoords.ToMapPos(entManager, transform);

        gunSystem.ShootProjectile(ent, direction, userVelocity, args.User.Value, args.User, ProjectileSpeed);
    }
}
