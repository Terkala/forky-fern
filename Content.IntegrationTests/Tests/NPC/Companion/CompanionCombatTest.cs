using System.Numerics;
using Content.Server.NPC.Companion;
using Content.Server.NPC.Companion.Components;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.NPC.Companion;

[TestFixture]
public sealed class CompanionCombatTest
{
    [Test]
    public async Task Companion_DoesNotFire_WhenOwnerInLineOfFire()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid owner = default;
        EntityUid companion = default;
        EntityUid target = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mapSys = entMan.System<SharedMapSystem>();
            var xform = entMan.System<SharedTransformSystem>();
            var coords = new EntityCoordinates(testMap.Grid, 0.5f, 0.5f);

            var tileDef = server.Resolve<ITileDefinitionManager>()["Plating"];
            for (var x = 0; x <= 3; x++)
            for (var y = 0; y <= 3; y++)
                mapSys.SetTile(testMap.Grid.Owner, testMap.Grid.Comp, new Vector2i(x, y), new Tile(tileDef.TileId));

            owner = entMan.SpawnEntity("MobHuman", coords.Offset(new Vector2(1, 0)));
            companion = entMan.SpawnEntity("MobGlockroach", coords.Offset(new Vector2(0, 0)));
            target = entMan.SpawnEntity("MobHuman", coords.Offset(new Vector2(2, 0)));

            var companionSystem = entMan.System<NPCCompanionSystem>();
            companionSystem.BindCompanion(owner, companion);

            var npcFaction = entMan.System<NpcFactionSystem>();
            npcFaction.AggroEntity(companion, target);
        });

        await pair.RunTicksSync(90);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            if (entMan.TryGetComponent(companion, out NPCRangedCombatComponent? ranged))
            {
                Assert.That(ranged.Status, Is.Not.EqualTo(CombatStatus.Normal),
                    "Companion should not be in Normal (firing) status when owner blocks line of fire - owner is between companion and target");
            }
        });

        await pair.CleanReturnAsync();
    }
}
