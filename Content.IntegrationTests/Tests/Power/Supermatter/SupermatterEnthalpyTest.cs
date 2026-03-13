using System.Linq;
using Content.IntegrationTests.Tests.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Power.Generation.Supermatter;
using Content.Shared.Atmos;
using Content.Shared.Power.Generation.Supermatter.Components;
using Content.Shared.Tests;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Power.Supermatter;

[TestFixture]
[TestOf(typeof(SupermatterSystem))]
public sealed class SupermatterEnthalpyTest : AtmosTest
{
    protected override ResPath? TestMapPath => new("Maps/Test/Atmospherics/tile_atmosphere_test_room.yml");

    [Test]
    public async Task PositiveEnthalpyInHotChamberIncreasesPower()
    {
        EntityUid supermatter = default;
        float powerBefore = 0f;

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
            centerMix.Temperature = 400f;
        });

        await RunTicks(5);
        await Server.WaitPost(() => powerBefore = SEntMan.GetComponent<SupermatterStateComponent>(supermatter).Power);
        await RunTicks(100);

        await Server.WaitAssertion(() =>
        {
            var state = SEntMan.GetComponent<SupermatterStateComponent>(supermatter);
            Assert.That(state.Power, Is.GreaterThan(powerBefore), "Positive Enthalpy (Plasma) in hot chamber should increase Power");
        });
    }

    [Test]
    public async Task NegativeEnthalpyInColdChamberIncreasesPower()
    {
        EntityUid supermatter = default;
        float powerBefore = 0f;

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
            centerMix!.AdjustMoles(Gas.Frezon, 100f);
            centerMix.Temperature = 200f;
        });

        await RunTicks(5);
        await Server.WaitPost(() => powerBefore = SEntMan.GetComponent<SupermatterStateComponent>(supermatter).Power);
        await RunTicks(100);

        await Server.WaitAssertion(() =>
        {
            var state = SEntMan.GetComponent<SupermatterStateComponent>(supermatter);
            Assert.That(state.Power, Is.GreaterThan(powerBefore), "Negative Enthalpy (Frezon) in cold chamber should increase Power");
        });
    }
}
