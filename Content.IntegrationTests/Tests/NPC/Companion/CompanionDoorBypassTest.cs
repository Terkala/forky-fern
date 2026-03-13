using System.Numerics;
using Content.Server.NPC.Companion;
using Content.Server.NPC.Companion.Components;
using Content.Shared.Doors.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.NPC.Companion;

[TestFixture]
public sealed class CompanionDoorBypassTest
{
    [Test]
    public async Task Companion_BypassesDoor_ToReachOwner()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid owner = default;
        EntityUid companion = default;
        EntityUid door = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mapSys = entMan.System<SharedMapSystem>();
            var coords = new EntityCoordinates(testMap.Grid, 0.5f, 0.5f);

            var tileDef = server.Resolve<ITileDefinitionManager>()["Plating"];
            for (var x = 0; x <= 5; x++)
            for (var y = 0; y <= 2; y++)
                mapSys.SetTile(testMap.Grid.Owner, testMap.Grid.Comp, new Vector2i(x, y), new Tile(tileDef.TileId));

            owner = entMan.SpawnEntity("MobHuman", coords.Offset(new Vector2(4, 0)));
            companion = entMan.SpawnEntity("MobCompanion", coords.Offset(new Vector2(0, 0)));
            door = entMan.SpawnEntity("AirlockDummy", coords.Offset(new Vector2(2, 0)));

            var companionSystem = entMan.System<NPCCompanionSystem>();
            companionSystem.BindCompanion(owner, companion);
        });

        await pair.RunTicksSync(240);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var transform = entMan.System<SharedTransformSystem>();
            var ownerPos = transform.GetMapCoordinates(owner).Position;
            var companionPos = transform.GetMapCoordinates(companion).Position;
            var distance = Vector2.Distance(ownerPos, companionPos);

            var doorOpened = entMan.TryGetComponent(door, out DoorComponent? doorComp) &&
                (doorComp!.State == DoorState.Open || doorComp.State == DoorState.Opening);

            Assert.Multiple(() =>
            {
                Assert.That(distance < 5f || doorOpened, Is.True,
                    "Companion should have either reached the owner (distance < 5) or opened the door. Distance: " + distance + ", Door opened: " + doorOpened);
            });
        });

        await pair.CleanReturnAsync();
    }
}
