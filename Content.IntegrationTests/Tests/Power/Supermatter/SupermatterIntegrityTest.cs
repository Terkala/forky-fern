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
public sealed class SupermatterIntegrityTest : AtmosTest
{
    protected override ResPath? TestMapPath => new("Maps/Test/Atmospherics/tile_atmosphere_test_room.yml");

    [Test]
    public async Task SupermatterMaintainsIntegrityWithPositiveStability()
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
            centerMix!.AdjustMoles(Gas.NitrousOxide, 1000f);
        });

        await RunTicks(100);

        await Server.WaitAssertion(() =>
        {
            var state = SEntMan.GetComponent<SupermatterStateComponent>(supermatter);
            Assert.That(state.Integrity, Is.GreaterThanOrEqualTo(900f),
                "N2O (high Stability) should maintain or heal Integrity");
        });
    }
}
