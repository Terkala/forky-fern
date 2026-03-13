using System.Linq;
using Content.IntegrationTests.Tests.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Power.Generation.Supermatter;
using Content.Shared.Atmos;
using Content.Shared.Tests;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Power.Supermatter;

[TestFixture]
[TestOf(typeof(SupermatterSystem))]
public sealed class SupermatterGasAbsorptionTest : AtmosTest
{
    protected override ResPath? TestMapPath => new("Maps/Test/Atmospherics/tile_atmosphere_test_room.yml");

    [Test]
    public async Task SupermatterAbsorbsGasFromFiveTiles()
    {
        EntityUid supermatter = default;
        float initialMoles = 0f;

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
            centerMix!.AdjustMoles(Gas.Nitrogen, 100f);
            initialMoles = GetGridMoles(RelevantAtmos);
        });

        await RunTicks(100);

        await Server.WaitAssertion(() =>
        {
            var finalMoles = GetGridMoles(RelevantAtmos);
            Assert.That(MathHelper.CloseToPercent(initialMoles, finalMoles, Tolerance),
                $"Grid moles should be conserved. Initial: {initialMoles}, Final: {finalMoles}");

            var centerMix = SAtmos.GetTileMixture(supermatter, true);
            Assert.That(centerMix, Is.Not.Null, "SM tile should have gas mixture");
            Assert.That(centerMix!.TotalMoles, Is.GreaterThan(0f), "SM tile should have gas after absorption");
        });
    }
}
