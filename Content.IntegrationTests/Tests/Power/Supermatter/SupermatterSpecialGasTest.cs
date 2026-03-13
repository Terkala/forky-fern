using System.Linq;
using Content.IntegrationTests.Tests.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Power.Generation.Supermatter;
using Content.Server.Power.Generation.Supermatter.Components;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Power.Generation.Supermatter.Components;
using Content.Shared.Tests;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Power.Supermatter;

[TestFixture]
[TestOf(typeof(SupermatterSystem))]
public sealed class SupermatterSpecialGasTest : AtmosTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamageTypeId = "Blunt";

    protected override ResPath? TestMapPath => new("Maps/Test/Atmospherics/tile_atmosphere_test_room.yml");

    [Test]
    public async Task HealiumHealsIntegrity()
    {
        EntityUid supermatter = default;
        float integrityBeforeHealium = 0f;

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
            centerMix.AdjustMoles(Gas.Nitrogen, 500f);

            var damageSys = SEntMan.System<DamageableSystem>();
            var bluntType = ProtoMan.Index(BluntDamageTypeId);
            damageSys.TryChangeDamage(supermatter, new DamageSpecifier(bluntType, FixedPoint2.New(50000)), true);
        });

        await RunTicks(30);

        await Server.WaitPost(() =>
        {
            integrityBeforeHealium = SEntMan.GetComponent<SupermatterStateComponent>(supermatter).Integrity;
            var floorPos = Transform.GetGridTilePositionOrDefault(supermatter);
            var gridAtmos = SEntMan.GetComponent<GridAtmosphereComponent>(MapData.Grid);
            var centerMix = SAtmos.GetTileMixture((MapData.Grid, gridAtmos), null, floorPos, true);
            Assert.That(centerMix, Is.Not.Null);
            centerMix!.AdjustMoles(Gas.Healium, 50f);
        });

        await RunTicks(20);

        await Server.WaitAssertion(() =>
        {
            var state = SEntMan.GetComponent<SupermatterStateComponent>(supermatter);
            Assert.That(state.Integrity, Is.GreaterThan(integrityBeforeHealium),
                "Healium should heal Integrity when >= 10 mol present");
        });
    }

}
