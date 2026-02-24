using Content.IntegrationTests.Pair;
using Content.Server._Funkystation.LiquidBlob;
using Content.Shared._Funkystation.LiquidBlob.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._Funkystation.LiquidBlob;

[TestFixture]
[TestOf(typeof(LiquidBlobSpreadSystem))]
public sealed class LiquidBlobTest
{

    [Test]
    public async Task BlobSpawnTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            Dirty = true
        });

        var server = pair.Server;
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();
        var mapSys = sEntMan.System<SharedMapSystem>();
        var grid = mapData.Grid;
        var gridUid = grid.Owner;

        EntityUid tileEntity = default;
        EntityUid observerEntity = default;

        await server.WaitPost(() =>
        {
            var tileCoords = mapSys.GridTileToLocal(gridUid, grid.Comp, new Vector2i(0, 0));
            tileEntity = sEntMan.SpawnEntity("LiquidBlobTile", tileCoords);
            observerEntity = sEntMan.SpawnEntity("LiquidBlobObserver", tileCoords);

            var tileComp = sEntMan.GetComponent<LiquidBlobTileComponent>(tileEntity);
            tileComp.RootTile = tileEntity;
            tileComp.LiquidLevel = 0;
            sEntMan.Dirty(tileEntity, tileComp);

            var observerComp = sEntMan.GetComponent<LiquidBlobObserverComponent>(observerEntity);
            observerComp.RootTile = tileEntity;
            sEntMan.Dirty(observerEntity, observerComp);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.EntityExists(tileEntity), "Blob tile entity should exist");
            Assert.That(sEntMan.EntityExists(observerEntity), "Blob observer entity should exist");
            Assert.That(sEntMan.HasComponent<LiquidBlobTileComponent>(tileEntity), "Tile should have LiquidBlobTileComponent");
            Assert.That(sEntMan.HasComponent<LiquidBlobObserverComponent>(observerEntity), "Observer should have LiquidBlobObserverComponent");
        });
    }

    [Test]
    public async Task BlobSpreadTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            Dirty = true
        });

        var server = pair.Server;
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();
        var mapSys = sEntMan.System<SharedMapSystem>();
        var spreadSys = sEntMan.System<LiquidBlobSpreadSystem>();
        var grid = mapData.Grid;
        var gridUid = grid.Owner;

        EntityUid tileEntity = default;
        EntityUid observerEntity = default;

        await server.WaitPost(() =>
        {
            var tileCoords = mapSys.GridTileToLocal(gridUid, grid.Comp, new Vector2i(0, 0));
            var targetCoords = mapSys.GridTileToLocal(gridUid, grid.Comp, new Vector2i(1, 0));

            tileEntity = sEntMan.SpawnEntity("LiquidBlobTile", tileCoords);
            observerEntity = sEntMan.SpawnEntity("LiquidBlobObserver", tileCoords);

            var tileComp = sEntMan.GetComponent<LiquidBlobTileComponent>(tileEntity);
            tileComp.RootTile = tileEntity;
            tileComp.LiquidLevel = 5f;
            sEntMan.Dirty(tileEntity, tileComp);

            var observerComp = sEntMan.GetComponent<LiquidBlobObserverComponent>(observerEntity);
            observerComp.RootTile = tileEntity;
            sEntMan.Dirty(observerEntity, observerComp);

            var result = spreadSys.TrySpreadFromObserver(observerEntity, targetCoords);
            Assert.That(result, "TrySpreadFromObserver should succeed");

            var tileCompAfterSpread = sEntMan.GetComponent<LiquidBlobTileComponent>(tileEntity);
            Assert.That(tileCompAfterSpread.LiquidLevel, Is.LessThan(1f),
                "Source tile should have less than 1 liquid after spread (5 was deducted; small tolerance for production)");

            var blobQuery = sEntMan.EntityQueryEnumerator<LiquidBlobTileComponent>();
            var newTileCount = 0;
            while (blobQuery.MoveNext(out var uid, out var comp))
            {
                if (uid != tileEntity && comp.RootTile == tileEntity)
                    newTileCount++;
            }
            Assert.That(newTileCount, Is.EqualTo(1), "Should have exactly one new blob tile");
        });
    }
}
