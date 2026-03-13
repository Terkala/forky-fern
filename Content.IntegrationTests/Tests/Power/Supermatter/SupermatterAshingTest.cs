using System.Linq;
using Content.IntegrationTests.Tests.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Power.Generation.Supermatter;
using Content.Server.Power.Generation.Supermatter.Components;
using Content.Shared.Atmos;
using Content.Shared.Item;
using Content.Shared.Power.Generation.Supermatter.Components;
using Content.Shared.Tests;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Power.Supermatter;

[TestFixture]
[TestOf(typeof(SupermatterSystem))]
public sealed class SupermatterAshingTest : AtmosTest
{
    protected override ResPath? TestMapPath => new("Maps/Test/Atmospherics/tile_atmosphere_test_room.yml");

    [Test]
    public async Task ItemAshingIncreasesPowerAndDeletesEntity()
    {
        EntityUid supermatter = default;
        EntityUid item = default;

        await Server.WaitPost(() =>
        {
            var markers = SEntMan.AllEntities<TestMarkerComponent>().ToArray();
            Assert.That(GetMarker(markers, "floor", out var floorUid));
            var floorCoords = SEntMan.GetComponent<TransformComponent>(floorUid).Coordinates;
            supermatter = SEntMan.SpawnEntity("Supermatter", floorCoords);
            item = SEntMan.SpawnEntity("Paper", floorCoords);

            var floorPos = Transform.GetGridTilePositionOrDefault(floorUid);
            var gridAtmos = SEntMan.GetComponent<GridAtmosphereComponent>(MapData.Grid);
            var centerMix = SAtmos.GetTileMixture((MapData.Grid, gridAtmos), null, floorPos, true);
            Assert.That(centerMix, Is.Not.Null);
            centerMix!.AdjustMoles(Gas.NitrousOxide, 100f);
        });

        await RunTicks(20);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(item), "Item should be ashed (deleted) when touching supermatter");
            var state = SEntMan.GetComponent<SupermatterStateComponent>(supermatter);
            Assert.That(state.Power, Is.GreaterThan(0f), "Ashing an item should add power");
        });
    }

    [Test]
    public async Task NonLivingAshingAddsMatterHealing()
    {
        EntityUid supermatter = default;
        EntityUid item = default;

        await Server.WaitPost(() =>
        {
            var markers = SEntMan.AllEntities<TestMarkerComponent>().ToArray();
            Assert.That(GetMarker(markers, "floor", out var floorUid));
            var floorCoords = SEntMan.GetComponent<TransformComponent>(floorUid).Coordinates;
            supermatter = SEntMan.SpawnEntity("Supermatter", floorCoords);
            item = SEntMan.SpawnEntity("Paper", floorCoords);

            var floorPos = Transform.GetGridTilePositionOrDefault(floorUid);
            var gridAtmos = SEntMan.GetComponent<GridAtmosphereComponent>(MapData.Grid);
            var centerMix = SAtmos.GetTileMixture((MapData.Grid, gridAtmos), null, floorPos, true);
            Assert.That(centerMix, Is.Not.Null);
            centerMix!.AdjustMoles(Gas.NitrousOxide, 100f);
        });

        await RunTicks(20);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(item), "Item should be ashed");
            var processing = SEntMan.GetComponent<SupermatterProcessingComponent>(supermatter);
            Assert.That(processing.MatterHealing, Is.GreaterThan(0f),
                "Ashing a non-living item should add MatterHealing");
        });
    }
}
