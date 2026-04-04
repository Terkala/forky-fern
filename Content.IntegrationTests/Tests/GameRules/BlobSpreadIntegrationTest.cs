using Content.IntegrationTests.Pair;
using Content.Shared.Blob;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture]
public sealed class BlobSpreadIntegrationTest
{
    [Test]
    public async Task DeployAndSpreadIncreasesTileCount()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap(true, "FloorSteel");
        await pair.RunTicksSync(5);

        var tileMan = server.ResolveDependency<ITileDefinitionManager>();
        var tile = new Tile(tileMan["FloorSteel"].TileId);

        EntityUid overmind = default;
        BlobHiveComponent? hive = null;

        await server.WaitPost(() =>
        {
            var sys = server.System<SharedMapSystem>();
            var grid = map.Grid;
            for (var x = -2; x <= 2; x++)
            {
                for (var y = -2; y <= 2; y++)
                    sys.SetTile(grid.Owner, grid.Comp, new EntityCoordinates(grid.Owner, x, y), tile);
            }

            var center = new EntityCoordinates(grid.Owner, 0, 0);
            overmind = server.EntMan.SpawnEntity("MobBlobOvermind", center);
            hive = server.EntMan.GetComponent<BlobHiveComponent>(overmind);

            var deploy = new BlobDeployActionEvent { Performer = overmind, Target = center };
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, deploy);
        });

        await pair.RunTicksSync(5);

        Assert.That(hive!.Deployed, Is.True);
        Assert.That(hive.TileCount, Is.EqualTo(5));

        await server.WaitPost(() =>
        {
            hive!.BioPoints = 100;
            var spreadTarget = new EntityCoordinates(map.Grid.Owner, 2, 0);
            var spread = new BlobSpreadActionEvent { Performer = overmind, Target = spreadTarget };
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, spread);
        });

        await pair.RunTicksSync(5);
        Assert.That(hive!.TileCount, Is.EqualTo(6));

        await pair.CleanReturnAsync();
    }
}
