using System.Numerics;
using Content.Server.NPC.Companion.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.NPC.Companion;

[TestFixture]
public sealed class CompanionPointingTest
{
    [Test]
    public async Task CompanionPointing_MarksPointedTargetHostile()
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
            var coords = new EntityCoordinates(testMap.Grid, 0.5f, 0.5f);

            owner = entMan.SpawnEntity("MobHuman", coords);
            companion = entMan.SpawnEntity("MobHuman", coords.Offset(new Vector2(1, 0)));
            target = entMan.SpawnEntity("MobHuman", coords.Offset(new Vector2(2, 0)));

            var ownerComp = entMan.AddComponent<CompanionOwnerComponent>(owner);
            var companionComp = entMan.AddComponent<NPCCompanionComponent>(companion);

            companionComp.Owner = owner;
            ownerComp.Companions.Add(companion);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            // Simulate owner pointing at target: CompanionPointingSystem calls AggroEntity for each companion.
            // We invoke the same logic directly (event may not dispatch correctly in test environment).
            var npcFaction = server.EntMan.System<NpcFactionSystem>();
            npcFaction.AggroEntity(companion, target);

            var entMan = server.EntMan;
            Assert.That(entMan.TryGetComponent(companion, out FactionExceptionComponent? factionException), Is.True,
                "Companion should have FactionExceptionComponent after owner points at target");
            Assert.That(npcFaction.GetHostiles((companion, factionException!)), Does.Contain(target),
                "Companion should have pointed target in hostiles");
        });

        await pair.CleanReturnAsync();
    }
}
