using System.Numerics;
using Content.Server.Implants;
using Content.Server.NPC.Companion;
using Content.Server.NPC.Companion.Components;
using Content.Server.NPC.Components;
using Content.Shared.Implants.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.NPC.Companion;

[TestFixture]
public sealed class CompanionImplanterBindingTest
{
    [Test]
    public async Task CompanionImplanter_BindsCompanion_WhenImplantSucceeds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid owner = default;
        EntityUid companion = default;
        EntityUid implanter = default;

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
            implanter = entMan.SpawnEntity("CompanionImplanter", coords);

            var implanterSys = entMan.System<ImplanterSystem>();
            var implanterComp = entMan.GetComponent<ImplanterComponent>(implanter);
            implanterSys.Implant(owner, companion, implanter, implanterComp);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.TryGetComponent(companion, out NPCCompanionComponent? comp), Is.True);
            Assert.That(comp!.Owner, Is.EqualTo(owner));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CompanionImplanter_ConvertsAnyNpc_ToCompanion()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid owner = default;
        EntityUid civilian = default;
        EntityUid implanter = default;

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
            civilian = entMan.SpawnEntity("MobCivilian", coords.Offset(new Vector2(1, 0)));
            implanter = entMan.SpawnEntity("CompanionImplanter", coords);

            var implanterSys = entMan.System<ImplanterSystem>();
            var implanterComp = entMan.GetComponent<ImplanterComponent>(implanter);
            implanterSys.Implant(owner, civilian, implanter, implanterComp);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.TryGetComponent(civilian, out NPCCompanionComponent? comp), Is.True);
            Assert.That(comp!.Owner, Is.EqualTo(owner));
            Assert.That(entMan.HasComponent<NPCDoorBypassStateComponent>(civilian), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
