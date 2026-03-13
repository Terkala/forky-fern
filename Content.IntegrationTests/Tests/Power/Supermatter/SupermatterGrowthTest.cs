using System.Linq;
using Content.IntegrationTests.Tests.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Power.Generation.Supermatter;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Server.Power.Generation.Supermatter.Components;
using Content.Shared.Power.Generation.Supermatter.Components;
using Content.Shared.Tests;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Power.Supermatter;

[TestFixture]
[TestOf(typeof(SupermatterSystem))]
public sealed class SupermatterGrowthTest : AtmosTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamageTypeId = "Blunt";

    protected override ResPath? TestMapPath => new("Maps/Test/Atmospherics/tile_atmosphere_test_room.yml");

    [Test]
    public async Task NegativeGrowthProducesGasWithHighPower()
    {
        EntityUid supermatter = default;

        await Server.WaitPost(() =>
        {
            var markers = SEntMan.AllEntities<TestMarkerComponent>().ToArray();
            Assert.That(GetMarker(markers, "floor", out var floorUid));
            var floorCoords = SEntMan.GetComponent<TransformComponent>(floorUid).Coordinates;
            supermatter = SEntMan.SpawnEntity("Supermatter", floorCoords);

            var floorPos = Transform.GetGridTilePositionOrDefault(floorUid);
            var gridAtmos = SEntMan.GetComponent<GridAtmosphereComponent>(MapData.Grid);
            var centerMix = SAtmos.GetTileMixture((MapData.Grid, gridAtmos), null, floorPos, true);
            Assert.That(centerMix, Is.Not.Null);
            centerMix!.AdjustMoles(Gas.Plasma, 100f);
            var damageSys = SEntMan.System<DamageableSystem>();
            var bluntType = ProtoMan.Index(BluntDamageTypeId);
            damageSys.TryChangeDamage(supermatter, new DamageSpecifier(bluntType, FixedPoint2.New(5000)), true);
        });

        await RunTicks(100);

        await Server.WaitAssertion(() =>
        {
            var floorPos = Transform.GetGridTilePositionOrDefault(supermatter);
            var gridAtmos = SEntMan.GetComponent<GridAtmosphereComponent>(MapData.Grid);
            var centerMix = SAtmos.GetTileMixture((MapData.Grid, gridAtmos), null, floorPos, true);
            Assert.That(centerMix, Is.Not.Null);
            var ammoniaMols = centerMix!.GetMoles(Gas.Ammonia);
            Assert.That(ammoniaMols, Is.GreaterThan(0f), "Negative Growth with Plasma should produce Ammonia");
        });
    }

    [Test]
    public async Task PositiveGrowthIncreasesReproduction()
    {
        EntityUid supermatter = default;

        await Server.WaitPost(() =>
        {
            var markers = SEntMan.AllEntities<TestMarkerComponent>().ToArray();
            Assert.That(GetMarker(markers, "floor", out var floorUid));
            var floorCoords = SEntMan.GetComponent<TransformComponent>(floorUid).Coordinates;
            supermatter = SEntMan.SpawnEntity("Supermatter", floorCoords);

            var floorPos = Transform.GetGridTilePositionOrDefault(floorUid);
            var gridAtmos = SEntMan.GetComponent<GridAtmosphereComponent>(MapData.Grid);
            var centerMix = SAtmos.GetTileMixture((MapData.Grid, gridAtmos), null, floorPos, true);
            Assert.That(centerMix, Is.Not.Null);
            centerMix!.AdjustMoles(Gas.CarbonDioxide, 500f);
        });

        await RunTicks(100);

        await Server.WaitAssertion(() =>
        {
            var processing = SEntMan.GetComponent<SupermatterProcessingComponent>(supermatter);
            Assert.That(processing.Reproduction + processing.SecondTally, Is.GreaterThan(0f),
                "Positive Growth (CO2) should increase Reproduction or SecondTally");
        });
    }
}
