using System.Linq;
using Content.IntegrationTests;
using Content.Server.Cybernetics.Systems;
using Content.Shared.Atmos.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Events;
using Content.Shared.Cybernetics.UI;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IgnitionSource;
using Content.Shared.Interaction;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mind;
using Content.Shared.Players;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Cybernetics;

/// <summary>
/// Integration test verifying that a lighter stored in a cyber arm can be brought out
/// and used to ignite a flammable object (paper). Tests the alt-use BUI change: normal use
/// on virtual item uses the item instead of opening the BUI.
/// </summary>
[TestFixture]
[TestOf(typeof(CyberArmSelectSystem))]
public sealed class CyberArmLighterIgnitionIntegrationTest
{
    private static EntityUid GetArmLeft(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("ArmLeft") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    private static void ReplaceArmWithCyberArm(IEntityManager entityManager, BodySystem bodySystem,
        SharedContainerSystem containerSystem, EntityUid body, EntityCoordinates coords)
    {
        var arm = GetArmLeft(entityManager, body);
        var removeEv = new OrganRemoveRequestEvent(arm) { Destination = coords };
        entityManager.EventBus.RaiseLocalEvent(arm, ref removeEv);
        Assert.That(removeEv.Success, Is.True, "Remove arm should succeed");

        var cyberArm = entityManager.SpawnEntity("OrganCyberArmLeft", coords);
        var bodyComp = entityManager.GetComponent<BodyComponent>(body);
        Assert.That(bodyComp.Organs, Is.Not.Null, "Body should have Organs container");
        Assert.That(containerSystem.Insert(cyberArm, bodyComp.Organs!), Is.True, "Insert cyber arm should succeed");
    }

    [Test]
    public async Task CyberArmLighter_CanBeBroughtOutAndIgniteObject()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true, DummyTicker = false });
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitIdleAsync();
        await client.WaitIdleAsync();

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = sEntMan.System<BodySystem>();
        var containerSystem = sEntMan.System<SharedContainerSystem>();
        var storageSystem = sEntMan.System<SharedStorageSystem>();
        var userInterface = sEntMan.System<UserInterfaceSystem>();
        var handsSystem = sEntMan.System<SharedHandsSystem>();
        var interactionSystem = sEntMan.System<SharedInteractionSystem>();
        var playerMan = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
        var mapData = await pair.CreateTestMap();

        await pair.RunTicksSync(5);
        await PoolManager.WaitUntil(server, () => playerMan.Sessions.First().AttachedEntity != null);

        EntityUid cyberArm = default;
        EntityUid user = default;
        EntityUid lighter = default;
        EntityUid paper = default;

        await server.WaitAssertion(() =>
        {
            var session = playerMan.Sessions.First();
            var mindSystem = sEntMan.System<SharedMindSystem>();
            mindSystem.WipeMind(session.ContentData()?.Mind);
            user = sEntMan.SpawnEntity("MobHuman", mapData.GridCoords);
            playerMan.SetAttachedEntity(session, user);
            ReplaceArmWithCyberArm(sEntMan, bodySystem, containerSystem, user, mapData.GridCoords);
            cyberArm = bodySystem.GetAllOrgans(user).First(o =>
                sEntMan.HasComponent<CyberLimbComponent>(o));

            lighter = sEntMan.SpawnEntity("Lighter", mapData.GridCoords);
            storageSystem.Insert(cyberArm, lighter, out _, user: null, playSound: false);
        });

        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
        {
            handsSystem.TrySetActiveHand((user, sEntMan.GetComponent<HandsComponent>(user)), "left");
            Assert.That(handsSystem.TryUseItemInHand(user, altInteract: true, handName: "left"), Is.True,
                "TryUseItemInHand (alt) should open BUI");
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(userInterface.IsUiOpen(cyberArm, CyberArmSelectUiKey.Key, user), Is.True,
                "Cyber arm select UI should be open");

            var lighterNet = sEntMan.GetNetEntity(lighter);
            var msg = new CyberArmSelectRequestMessage(lighterNet);
            msg.Actor = user;
            userInterface.RaiseUiMessage(cyberArm, CyberArmSelectUiKey.Key, msg);
        });

        await pair.RunTicksSync(15);

        await server.WaitAssertion(() =>
        {
            Assert.That(userInterface.IsUiOpen(cyberArm, CyberArmSelectUiKey.Key, user), Is.False,
                "Cyber arm select UI should close after selection");

            Assert.That(handsSystem.TryGetActiveItem(user, out var held), Is.True,
                "User should have an item in hand");
            Assert.That(sEntMan.HasComponent<VirtualItemComponent>(held), Is.True,
                "Held item should be a virtual item");

            var virt = sEntMan.GetComponent<VirtualItemComponent>(held!.Value);
            Assert.That(virt.BlockingEntity, Is.EqualTo(lighter),
                "Virtual item should point to the lighter in storage");

            // Normal use in hand - should toggle lighter, NOT open BUI
            var useResult = handsSystem.TryUseItemInHand(user, altInteract: false);
            Assert.That(useResult, Is.True, "Use in hand should succeed");
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var lighterComp = sEntMan.GetComponent<ItemToggleComponent>(lighter);
            Assert.That(lighterComp.Activated, Is.True,
                "Lighter should be activated after use in hand");

            Assert.That(sEntMan.TryGetComponent(lighter, out IgnitionSourceComponent? ignSource), Is.True);
            Assert.That(ignSource!.Ignited, Is.True,
                "Lighter ignition source should be ignited");

            paper = sEntMan.SpawnEntity("Paper", mapData.GridCoords.Offset(new(1, 0)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var paperCoords = sEntMan.GetComponent<TransformComponent>(paper).Coordinates;
            var interactResult = interactionSystem.InteractUsing(user, lighter, paper, paperCoords);
            Assert.That(interactResult, Is.True, "InteractUsing lighter on paper should succeed");
        });

        await pair.RunTicksSync(15);

        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.TryGetComponent(paper, out FlammableComponent? flammable), Is.True);
            Assert.That(flammable!.OnFire, Is.True,
                "Paper should be on fire after being ignited by lighter");
        });

        await pair.CleanReturnAsync();
    }
}
