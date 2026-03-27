using Content.IntegrationTests.Pair;
using Content.Server.Power.EntitySystems;
using Content.Shared._CE.MageAscension;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Power.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._CE;

[TestFixture]
public sealed class MageAscensionTest
{
    [Test]
    public async Task CreateGrimoireCompletesAndCostsMana()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();
        var map = await pair.CreateTestMap();

        var ent = server.EntMan;
        var doAfter = ent.System<SharedDoAfterSystem>();
        var batterySys = ent.System<BatterySystem>();
        var hands = ent.System<SharedHandsSystem>();

        await server.WaitPost(() =>
        {
            var mage = ent.SpawnEntity("InteractionTestMob", map.MapCoords);
            ent.AddComponent<MageOfAscensionComponent>(mage);
            var battery = ent.EnsureComponent<BatteryComponent>(mage);
            batterySys.SetMaxCharge((mage, battery), 100);
            batterySys.SetCharge((mage, battery), 100);

            var book = ent.SpawnEntity("BookHowToSurvive", map.MapCoords);
            Assert.That(hands.TryForcePickupAnyHand(mage, book, checkActionBlocker: false), Is.True);

            var ev = new MageCreateGrimoireDoAfterEvent();
            var args = new DoAfterArgs(ent, mage, TimeSpan.FromSeconds(0.25), ev, mage, used: book)
            {
                Broadcast = true,
                NeedHand = true,
            };
            Assert.That(doAfter.TryStartDoAfter(args), Is.True);
        });

        await pair.RunTicksSync(50);

        await server.WaitAssertion(() =>
        {
            var mageQuery = ent.EntityQueryEnumerator<MageOfAscensionComponent>();
            Assert.That(mageQuery.MoveNext(out var mageUid, out _), Is.True);
            var battery = ent.GetComponent<BatteryComponent>(mageUid);
            // 100 mana spent; MagePassiveManaRegenSystem adds ~1/s so a few ticks can leave a small remainder.
            Assert.That(batterySys.GetCharge((mageUid, battery)), Is.EqualTo(0f).Within(2f));

            var grimQuery = ent.EntityQueryEnumerator<MageGrimoireComponent>();
            Assert.That(grimQuery.MoveNext(out _, out _), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OpenConfluenceRaisesMaxManaAndMarksOpened()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();
        var map = await pair.CreateTestMap();

        var ent = server.EntMan;
        var batterySys = ent.System<BatterySystem>();
        var mapSys = ent.System<SharedMapSystem>();
        var hands = ent.System<SharedHandsSystem>();
        var interact = ent.System<SharedInteractionSystem>();

        await server.WaitPost(() =>
        {
            var mage = ent.SpawnEntity("InteractionTestMob", map.MapCoords);
            ent.AddComponent<MageOfAscensionComponent>(mage);
            var battery = ent.EnsureComponent<BatteryComponent>(mage);
            batterySys.SetMaxCharge((mage, battery), 100);
            batterySys.SetCharge((mage, battery), 100);

            var confCoords = mapSys.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, map.Tile.GridIndices);
            var conf = ent.SpawnEntity("MageLeyConfluence", confCoords);
            var grim = ent.SpawnEntity("MageGrimoire", map.MapCoords);
            Assert.That(hands.TryForcePickupAnyHand(mage, grim, checkActionBlocker: false), Is.True);

            interact.InteractUsing(mage, grim, conf, confCoords, checkCanInteract: false, checkCanUse: false);
        });

        await server.WaitAssertion(() =>
        {
            var mageQuery = ent.EntityQueryEnumerator<MageOfAscensionComponent>();
            Assert.That(mageQuery.MoveNext(out var mageUid, out var mageComp), Is.True);
            Assert.That(mageComp.ConfluencesOpened, Is.EqualTo(1));

            var confQuery = ent.EntityQueryEnumerator<ConfluenceComponent>();
            Assert.That(confQuery.MoveNext(out _, out var confComp), Is.True);
            Assert.That(confComp.Opened, Is.True);

            var bat = ent.GetComponent<BatteryComponent>(mageUid);
            Assert.That(bat.MaxCharge, Is.EqualTo(110f).Within(0.25f));
        });

        await pair.CleanReturnAsync();
    }
}
