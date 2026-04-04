using Content.IntegrationTests.Pair;
using Content.Server.Blob;
using Content.Shared.Blob;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture]
public sealed class BlobTileSpecialistIntegrationTest
{
    [Test]
    public async Task SpecialistConversion_ReplacesNormalTile_AndSpendsBio()
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

        EntityUid normalTileCaptured = default;
        var bioAfterConvert = 0;

        await server.WaitPost(() =>
        {
            hive!.BioPoints = 100;
            var hiveNet = server.EntMan.GetNetEntity(overmind);
            var q = server.EntMan.EntityQueryEnumerator<BlobTileComponent>();
            while (q.MoveNext(out var uid, out var bt))
            {
                if (bt.Hive == hiveNet && bt.Kind == BlobTileKind.Normal)
                {
                    normalTileCaptured = uid;
                    break;
                }
            }

            Assert.That(normalTileCaptured, Is.Not.EqualTo(EntityUid.Invalid));

            var blobSys = server.System<BlobSystem>();
            var ok = blobSys.TryApplySpecialistConversion(overmind, normalTileCaptured, BlobTileKind.Ribosome);
            Assert.That(ok, Is.True);
            bioAfterConvert = hive!.BioPoints;
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var hiveNet = server.EntMan.GetNetEntity(overmind);
            Assert.That(server.EntMan.Deleted(normalTileCaptured), Is.True);
            Assert.That(bioAfterConvert, Is.EqualTo(85));

            var foundRibosome = false;
            var rq = server.EntMan.EntityQueryEnumerator<BlobTileComponent>();
            while (rq.MoveNext(out var uid, out var bt))
            {
                if (bt.Hive == hiveNet && bt.Kind == BlobTileKind.Ribosome)
                {
                    foundRibosome = true;
                    break;
                }
            }

            Assert.That(foundRibosome, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpecialistConversion_LockedReflective_DoesNothing()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap(true, "FloorSteel");
        await pair.RunTicksSync(5);

        var tileMan = server.ResolveDependency<ITileDefinitionManager>();
        var tile = new Tile(tileMan["FloorSteel"].TileId);

        EntityUid overmind = default;
        BlobHiveComponent? hive = null;
        EntityUid normalTile = default;

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

        await server.WaitPost(() =>
        {
            hive!.BioPoints = 100;
            Assert.That(hive.UnlockReflective, Is.False);

            var hiveNet = server.EntMan.GetNetEntity(overmind);
            var q = server.EntMan.EntityQueryEnumerator<BlobTileComponent>();
            while (q.MoveNext(out var uid, out var bt))
            {
                if (bt.Hive == hiveNet && bt.Kind == BlobTileKind.Normal)
                {
                    normalTile = uid;
                    break;
                }
            }

            Assert.That(normalTile, Is.Not.EqualTo(EntityUid.Invalid));

            var blobSys = server.System<BlobSystem>();
            var ok = blobSys.TryApplySpecialistConversion(overmind, normalTile, BlobTileKind.Reflective);
            Assert.That(ok, Is.False);
            Assert.That(server.EntMan.GetComponent<BlobTileComponent>(normalTile).Kind, Is.EqualTo(BlobTileKind.Normal));
            Assert.That(hive!.BioPoints, Is.EqualTo(100));
        });

        await pair.CleanReturnAsync();
    }
}
