using Content.IntegrationTests;
using Content.IntegrationTests.Pair;
using Content.Server._CE.MageAscension;
using Content.Server.Power.EntitySystems;
using Content.Shared._CE.MageAscension;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Power.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._CE;

[TestFixture]
public sealed class MageAscensionTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

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

    [Test]
    public async Task OpeningEnoughLeylinesSpawnsStationDimensionalRift()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        await server.WaitIdleAsync();
        var map = await pair.CreateTestMap();

        var ent = server.EntMan;
        var mapSys = ent.System<SharedMapSystem>();
        var hands = ent.System<SharedHandsSystem>();
        var interact = ent.System<SharedInteractionSystem>();

        await server.WaitPost(() =>
        {
            var mage = ent.SpawnEntity("InteractionTestMob", map.MapCoords);
            ent.AddComponent<MageOfAscensionComponent>(mage);
            var mageComp = ent.GetComponent<MageOfAscensionComponent>(mage);
            mageComp.LeylinesRequiredForDimensionalRift = 2;

            for (var i = 0; i < 2; i++)
            {
                var confCoords = mapSys.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, map.Tile.GridIndices);
                var conf = ent.SpawnEntity("MageLeyConfluence", confCoords);
                var grim = ent.SpawnEntity("MageGrimoire", map.MapCoords);
                Assert.That(hands.TryForcePickupAnyHand(mage, grim, checkActionBlocker: false), Is.True);
                interact.InteractUsing(mage, grim, conf, confCoords, checkCanInteract: false, checkCanUse: false);
            }
        });

        await server.WaitAssertion(() =>
        {
            var mageQuery = ent.EntityQueryEnumerator<MageOfAscensionComponent>();
            Assert.That(mageQuery.MoveNext(out var mageUid, out var mage), Is.True);
            Assert.That(mage.ConfluencesOpened, Is.EqualTo(2));

            var riftCount = 0;
            EntityUid? riftUid = null;
            var riftEnumerator = ent.EntityQueryEnumerator<MageDimensionalRiftComponent>();
            while (riftEnumerator.MoveNext(out var uid, out _))
            {
                riftCount++;
                riftUid = uid;
            }

            Assert.That(riftCount, Is.EqualTo(1));
            Assert.That(riftUid, Is.Not.Null);
            Assert.That(ent.HasComponent<MageStationDimensionalRiftComponent>(riftUid!.Value), Is.True);

            var mageXform = ent.GetComponent<TransformComponent>(mageUid);
            var riftXform = ent.GetComponent<TransformComponent>(riftUid.Value);
            Assert.That(riftXform.MapUid, Is.EqualTo(mageXform.MapUid));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SecondMageReachingThresholdDoesNotSpawnSecondRift()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        await server.WaitIdleAsync();
        var map = await pair.CreateTestMap();

        var ent = server.EntMan;
        var mapSys = ent.System<SharedMapSystem>();
        var hands = ent.System<SharedHandsSystem>();
        var interact = ent.System<SharedInteractionSystem>();

        await server.WaitPost(() =>
        {
            void OpenTwoForMage(EntityUid mage)
            {
                var mageComp = ent.GetComponent<MageOfAscensionComponent>(mage);
                mageComp.LeylinesRequiredForDimensionalRift = 2;

                for (var i = 0; i < 2; i++)
                {
                    var confCoords = mapSys.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, map.Tile.GridIndices);
                    var conf = ent.SpawnEntity("MageLeyConfluence", confCoords);
                    var grim = ent.SpawnEntity("MageGrimoire", map.MapCoords);
                    Assert.That(hands.TryForcePickupAnyHand(mage, grim, checkActionBlocker: false), Is.True);
                    interact.InteractUsing(mage, grim, conf, confCoords, checkCanInteract: false, checkCanUse: false);
                }
            }

            var mageA = ent.SpawnEntity("InteractionTestMob", map.MapCoords);
            ent.AddComponent<MageOfAscensionComponent>(mageA);
            OpenTwoForMage(mageA);

            var mageB = ent.SpawnEntity("InteractionTestMob", map.MapCoords);
            ent.AddComponent<MageOfAscensionComponent>(mageB);
            OpenTwoForMage(mageB);
        });

        await server.WaitAssertion(() =>
        {
            var riftCount = 0;
            var riftEnumerator = ent.EntityQueryEnumerator<MageDimensionalRiftComponent>();
            while (riftEnumerator.MoveNext(out _, out _))
                riftCount++;

            Assert.That(riftCount, Is.EqualTo(1));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RiftElementalCreatureRotatesElementOverTime()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();
        var map = await pair.CreateTestMap();

        var ent = server.EntMan;

        await server.WaitPost(() =>
        {
            ent.SpawnEntity("MobMageRiftElementalCreature", map.MapCoords);
        });

        MageRiftElementKind start = default;
        await server.WaitAssertion(() =>
        {
            var q = ent.EntityQueryEnumerator<MageRiftElementalVariantComponent>();
            Assert.That(q.MoveNext(out _, out var comp), Is.True);
            start = comp.Current;
        });

        await pair.RunTicksSync(800);

        await server.WaitAssertion(() =>
        {
            var q = ent.EntityQueryEnumerator<MageRiftElementalVariantComponent>();
            Assert.That(q.MoveNext(out _, out var comp), Is.True);
            Assert.That(comp.Current, Is.Not.EqualTo(start));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RiftBigHonkDeathSpawnsBananaPeelsAndDeletesSelf()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();
        var map = await pair.CreateTestMap();

        var ent = server.EntMan;
        var proto = server.ResolveDependency<IPrototypeManager>();
        var blunt = proto.Index<DamageTypePrototype>(BluntDamageType);
        var damage = new DamageSpecifier(blunt, FixedPoint2.New(10_000));

        await server.WaitPost(() =>
        {
            var honk = ent.SpawnEntity("MobMageRiftBigHonk", map.MapCoords);
            ent.System<DamageableSystem>().TryChangeDamage(honk, damage, ignoreResistances: true);
        });

        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            var peelCount = 0;
            var metaEnum = ent.EntityQueryEnumerator<MetaDataComponent>();
            while (metaEnum.MoveNext(out var uid, out var meta))
            {
                if (meta.EntityPrototype?.ID == "TrashBananaPeel")
                    peelCount++;
            }

            Assert.That(peelCount, Is.GreaterThanOrEqualTo(10));

            var honkCount = 0;
            var honkEnum = ent.EntityQueryEnumerator<MageRiftBigHonkComponent>();
            while (honkEnum.MoveNext(out _, out _))
                honkCount++;
            Assert.That(honkCount, Is.EqualTo(0));
        });

        await pair.CleanReturnAsync();
    }
}
