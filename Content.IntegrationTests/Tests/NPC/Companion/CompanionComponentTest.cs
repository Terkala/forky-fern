using System.Numerics;
using Content.Server.NPC.Companion.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.NPC.Companion;

[TestFixture]
public sealed class CompanionComponentTest
{
    [Test]
    public async Task CompanionAndOwnerComponents_BidirectionalLink()
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
            companion = entMan.SpawnEntity("MobHuman", coords.Offset(new Vector2(1, 0)));

            var ownerComp = entMan.AddComponent<CompanionOwnerComponent>(owner);
            var companionComp = entMan.AddComponent<NPCCompanionComponent>(companion);

            companionComp.Owner = owner;
            ownerComp.Companions.Add(companion);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            Assert.That(entMan.TryGetComponent(owner, out CompanionOwnerComponent? ownerComp), Is.True);
            Assert.That(entMan.TryGetComponent(companion, out NPCCompanionComponent? companionComp), Is.True);

            Assert.That(companionComp!.Owner, Is.EqualTo(owner));
            Assert.That(ownerComp!.Companions, Does.Contain(companion));
            Assert.That(ownerComp.Companions.Count, Is.EqualTo(1));
        });

        await pair.CleanReturnAsync();
    }
}
