using Content.IntegrationTests.Pair;
using Content.Server.Station.Systems;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Station;

[TestFixture]
public sealed class StationSafeSpotSystemTest
{
    [Test]
    public async Task TryLocateSafeSpotOnStation_ReturnsTileWhenStationLinkedToGrid()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();
        var map = await pair.CreateTestMap();

        var ent = server.EntMan;
        var safeSpot = ent.System<StationSafeSpotSystem>();
        var stationSys = ent.System<StationSystem>();

        await server.WaitPost(() =>
        {
            var station = ent.SpawnEntity("TestStation", map.MapCoords);
            var data = ent.GetComponent<StationDataComponent>(station);
            stationSys.AddGridToStation(station, map.Grid.Owner, map.Grid.Comp, data);

            var spec = new StationSafeSpotLocateSpec
            {
                Station = (station, data),
                FootprintWidth = 1,
                FootprintHeight = 1,
                LocalAnchor = map.GridCoords,
                LocalHalfExtent = 3,
                StationWideStrictAttempts = 10,
            };

            Assert.That(safeSpot.TryLocateSafeSpotOnStation(spec, out var gridUid, out _, out var coords, out var quality), Is.True);
            Assert.That(gridUid.IsValid(), Is.True);
            Assert.That(coords.IsValid(ent), Is.True);
            Assert.That(quality, Is.AnyOf(StationLocateQuality.Strict, StationLocateQuality.RandomTile, StationLocateQuality.Arbitrary));
        });

        await pair.CleanReturnAsync();
    }
}
