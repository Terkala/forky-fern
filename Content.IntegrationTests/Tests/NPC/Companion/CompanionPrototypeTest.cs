using System.Numerics;
using Content.Server.NPC;
using Content.Server.NPC.Companion;
using Content.Server.NPC.Companion.Components;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.NPC.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.NPC.Companion;

[TestFixture]
public sealed class CompanionPrototypeTest
{
    [Test]
    public async Task CompanionPrototype_HasRequiredComponents()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid companion = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var coords = new EntityCoordinates(testMap.Grid, 0.5f, 0.5f);
            companion = entMan.SpawnEntity("MobCompanion", coords);

            Assert.That(entMan.HasComponent<HTNComponent>(companion), "Companion should have HTNComponent");
            Assert.That(entMan.HasComponent<NPCCompanionComponent>(companion), "Companion should have NPCCompanionComponent");
            Assert.That(entMan.HasComponent<NpcFactionMemberComponent>(companion), "Companion should have NpcFactionMemberComponent");
            Assert.That(entMan.HasComponent<NPCDoorBypassStateComponent>(companion), "Companion should have NPCDoorBypassStateComponent");

            var protoMan = server.Resolve<IPrototypeManager>();
            Assert.That(entMan.TryGetComponent(companion, out HTNComponent? htn), Is.True);
            Assert.That(htn!.RootTask, Is.Not.Null, "Companion should have HTN root task");
            Assert.That(htn.RootTask is HTNCompoundTask compound && compound.Task == "CompanionRootCompound", Is.True,
                "Companion HTN root should be CompanionRootCompound");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CompanionBinding_EstablishesCorrectLink()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid owner = default;
        EntityUid companion = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var coords = new EntityCoordinates(testMap.Grid, 0.5f, 0.5f);

            owner = entMan.SpawnEntity("MobHuman", coords);
            companion = entMan.SpawnEntity("MobCompanion", coords.Offset(new Vector2(1, 0)));

            var companionSystem = entMan.System<NPCCompanionSystem>();
            companionSystem.BindCompanion(owner, companion);
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var npcSystem = entMan.System<NPCSystem>();

            Assert.That(entMan.TryGetComponent(owner, out CompanionOwnerComponent? ownerComp), Is.True);
            Assert.That(ownerComp!.Companions, Does.Contain(companion));

            Assert.That(entMan.TryGetComponent(companion, out NPCCompanionComponent? companionComp), Is.True);
            Assert.That(companionComp!.Owner, Is.EqualTo(owner));

            Assert.That(entMan.TryGetComponent(companion, out HTNComponent? htn), Is.True);
            Assert.That(htn!.Blackboard.ContainsKey(NPCBlackboard.FollowTarget), Is.True,
                "Companion's FollowTarget should be set after binding");
        });

        await pair.CleanReturnAsync();
    }
}
