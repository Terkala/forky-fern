using System.Numerics;
using Content.Server.NPC.Companion;
using Content.Server.NPC.Companion.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.NPC.Companion;

[TestFixture]
public sealed class CompanionOffGridTest
{
    [Test]
    public async Task Companion_LastKnownPositionSet_WhenOwnerGoesOffGrid()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid owner = default;
        EntityUid companion = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mapSys = entMan.System<SharedMapSystem>();
            var coords = new EntityCoordinates(testMap.Grid, 0.5f, 0.5f);

            var tileDef = server.Resolve<ITileDefinitionManager>()["Plating"];
            for (var x = 0; x <= 4; x++)
            for (var y = 0; y <= 2; y++)
                mapSys.SetTile(testMap.Grid.Owner, testMap.Grid.Comp, new Vector2i(x, y), new Tile(tileDef.TileId));

            owner = entMan.SpawnEntity("MobHuman", coords);
            companion = entMan.SpawnEntity("MobCompanion", coords.Offset(new Vector2(1, 0)));

            var companionSystem = entMan.System<NPCCompanionSystem>();
            companionSystem.BindCompanion(owner, companion);
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var xformSys = entMan.System<SharedTransformSystem>();

            var ownerPos = xformSys.GetMapCoordinates(owner).Position;
            var mapUid = testMap.MapUid;

            var ownerXform = entMan.GetComponent<TransformComponent>(owner);
            xformSys.SetParent(owner, ownerXform, mapUid);
            xformSys.SetWorldPosition(owner, ownerPos + new Vector2(100, 0));
        });

        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var xformSys = entMan.System<SharedTransformSystem>();

            Assert.That(entMan.TryGetComponent(companion, out NPCCompanionComponent? comp), Is.True);
            Assert.That(comp!.LastKnownOwnerPosition, Is.Not.EqualTo(default(MapCoordinates)),
                "LastKnownOwnerPosition should be set when owner was on grid before going off-grid");
        });

        await pair.CleanReturnAsync();
    }
}
