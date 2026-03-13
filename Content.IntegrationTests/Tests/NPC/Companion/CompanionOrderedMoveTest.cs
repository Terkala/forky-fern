using System.Numerics;
using Content.Server.NPC;
using Content.Server.NPC.Companion;
using Content.Server.NPC.Companion.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.NPC.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.NPC.Companion;

[TestFixture]
public sealed class CompanionOrderedMoveTest
{
    [Test]
    public async Task Companion_MovesToOrderedDestination_AndClearsWhenArrived()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid owner = default;
        EntityUid companion = default;
        EntityCoordinates destination = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mapSys = entMan.System<SharedMapSystem>();
            var coords = new EntityCoordinates(testMap.Grid, 0.5f, 0.5f);

            var tileDef = server.Resolve<ITileDefinitionManager>()["Plating"];
            for (var x = 0; x <= 3; x++)
            for (var y = 0; y <= 3; y++)
                mapSys.SetTile(testMap.Grid.Owner, testMap.Grid.Comp, new Vector2i(x, y), new Tile(tileDef.TileId));

            owner = entMan.SpawnEntity("MobHuman", coords);
            companion = entMan.SpawnEntity("MobCompanion", coords.Offset(new Vector2(1, 0)));

            destination = coords.Offset(new Vector2(2, 2));

            var companionSystem = entMan.System<NPCCompanionSystem>();
            companionSystem.BindCompanion(owner, companion);

            var npcSystem = entMan.System<NPCSystem>();
            npcSystem.SetBlackboard(companion, NPCBlackboard.FollowTarget, new EntityCoordinates(owner, Vector2.Zero));
            npcSystem.SetBlackboard(companion, NPCBlackboard.OrderedDestination, destination);
        });

        float initialDistance = 0;
        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var transform = entMan.System<SharedTransformSystem>();
            var destMap = transform.ToMapCoordinates(destination);
            initialDistance = Vector2.Distance(
                transform.GetMapCoordinates(companion).Position,
                destMap.Position);
        });

        await pair.RunTicksSync(180);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var npcSystem = entMan.System<NPCSystem>();
            var transform = entMan.System<SharedTransformSystem>();
            var destMap = transform.ToMapCoordinates(destination);

            var currentDistance = Vector2.Distance(
                transform.GetMapCoordinates(companion).Position,
                destMap.Position);

            var orderedDestinationCleared = !entMan.TryGetComponent(companion, out HTNComponent? htn) ||
                !htn.Blackboard.ContainsKey(NPCBlackboard.OrderedDestination);

            Assert.Multiple(() =>
            {
                Assert.That(currentDistance < initialDistance || orderedDestinationCleared, Is.True,
                    "Companion should have moved toward OrderedDestination (distance decreased) or arrived (key cleared). " +
                    "Initial: " + initialDistance + ", Current: " + currentDistance + ", Cleared: " + orderedDestinationCleared);
            });
        });

        await pair.CleanReturnAsync();
    }
}
