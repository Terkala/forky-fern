using System.Linq;
using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Medical.Integrity.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Cybernetics;

[TestFixture]
[TestOf(typeof(CyberneticsMaintenanceSystem))]
public sealed class CyberneticsMaintenanceIntegrationTest
{
    private static EntityUid GetArmLeft(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("ArmLeft") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    /// <summary>
    /// Removes the left arm and inserts OrganCyberArmLeft into the body's container.
    /// </summary>
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
    public async Task Body_GainsCyberneticsMaintenanceComponent_WhenFirstCyberLimbInserted()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = entityManager.GetComponent<TransformComponent>(human).Coordinates;

            Assert.That(entityManager.HasComponent<CyberneticsMaintenanceComponent>(human), Is.False,
                "Body should not have CyberneticsMaintenanceComponent before cyber limb");

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, human, coords);

            Assert.That(entityManager.HasComponent<CyberneticsMaintenanceComponent>(human), Is.True,
                "Body should have CyberneticsMaintenanceComponent after inserting first cyber limb");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Body_LosesCyberneticsMaintenanceComponent_WhenLastCyberLimbRemoved()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var coords = entityManager.GetComponent<TransformComponent>(human).Coordinates;

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, human, coords);
            Assert.That(entityManager.HasComponent<CyberneticsMaintenanceComponent>(human), Is.True);

            var cyberArm = bodySystem.GetAllOrgans(human).First(o =>
                entityManager.HasComponent<CyberLimbComponent>(o));
            var removeEv = new OrganRemoveRequestEvent(cyberArm) { Destination = coords };
            entityManager.EventBus.RaiseLocalEvent(cyberArm, ref removeEv);
            Assert.That(removeEv.Success, Is.True, "Remove cyber arm should succeed");

            Assert.That(entityManager.HasComponent<CyberneticsMaintenanceComponent>(human), Is.False,
                "Body should lose CyberneticsMaintenanceComponent when last cyber limb removed");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FullFiveStepFlow_ScrewdriverWrenchWiresWrenchScrewdriver_Completes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var handsSystem = entityManager.System<SharedHandsSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid technician = default;
        EntityUid patient = default;
        EntityUid screwdriver = default;
        EntityUid wrench = default;
        EntityUid wireStack = default;
        EntityCoordinates coords = default;

        await server.WaitPost(() =>
        {
            coords = mapData.GridCoords;
            technician = entityManager.SpawnEntity("MobHuman", coords);
            patient = entityManager.SpawnEntity("MobHuman", coords);
            screwdriver = entityManager.SpawnEntity("Screwdriver", coords);
            wrench = entityManager.SpawnEntity("Wrench", coords);
            wireStack = entityManager.SpawnEntity("CableApcStack", coords);

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, patient, coords);
            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
        });

        var doAfterTicks = 150;

        await pair.RunTicksSync(5);

        // Step 1: Screwdriver expose
        await server.WaitPost(() =>
        {
            var ev = new InteractUsingEvent(technician, screwdriver, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(doAfterTicks);

        await server.WaitAssertion(() =>
        {
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.PanelExposed, Is.True, "Panel should be exposed after screwdriver");
            Assert.That(comp.PanelSecured, Is.False);
        });

        // Step 2: Wrench open
        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wrench, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wrench, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(doAfterTicks);

        await server.WaitAssertion(() =>
        {
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.PanelOpen, Is.True, "Panel should be open after wrench");
        });

        // Step 3: Wire insert (N=1 for one cyber limb)
        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wireStack, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wireStack, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(doAfterTicks);

        await server.WaitAssertion(() =>
        {
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.WiresInsertedCount, Is.EqualTo(1), "Should have inserted 1 wire");
        });

        // Step 4: Wrench close
        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wrench, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wrench, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(doAfterTicks);

        await server.WaitAssertion(() =>
        {
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.PanelOpen, Is.False, "Panel should be closed");
            Assert.That(comp.WiresInsertedCount, Is.EqualTo(0), "WiresInsertedCount should reset on close");
        });

        // Step 5: Screwdriver secure
        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, screwdriver, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(doAfterTicks);

        await server.WaitAssertion(() =>
        {
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.PanelSecured, Is.True, "Panel should be secured");
            Assert.That(comp.PanelExposed, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WireInsertion_Rejected_WhenWiresInsertedCountReachesN()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var handsSystem = entityManager.System<SharedHandsSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid technician = default;
        EntityUid patient = default;
        EntityUid screwdriver = default;
        EntityUid wrench = default;
        EntityUid wireStack = default;
        EntityCoordinates coords = default;

        await server.WaitPost(() =>
        {
            coords = mapData.GridCoords;
            technician = entityManager.SpawnEntity("MobHuman", coords);
            patient = entityManager.SpawnEntity("MobHuman", coords);
            screwdriver = entityManager.SpawnEntity("Screwdriver", coords);
            wrench = entityManager.SpawnEntity("Wrench", coords);
            wireStack = entityManager.SpawnEntity("CableApcStack", coords);

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, patient, coords);
            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var ev = new InteractUsingEvent(technician, screwdriver, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wrench, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wrench, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wireStack, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wireStack, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            var comp = entityManager.GetComponent<CyberneticsMaintenanceComponent>(patient);
            Assert.That(comp.WiresInsertedCount, Is.EqualTo(1), "Maintenance should be complete after 1 wire");
            Assert.That(comp.PanelOpen, Is.True);

            var wireStack2 = entityManager.SpawnEntity("CableApcStack", coords);
            handsSystem.TryPickupAnyHand(technician, wireStack2, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wireStack2, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
            Assert.That(ev.Handled, Is.False,
                "Second wire attempt when WiresInsertedCount >= N should show popup, not start DoAfter");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IntegrityPenalty_AppliedAndCleared_DuringMaintenanceFlow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var handsSystem = entityManager.System<SharedHandsSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid technician = default;
        EntityUid patient = default;
        EntityUid cyberArm = default;
        EntityUid screwdriver = default;
        EntityUid wrench = default;
        EntityCoordinates coords = default;

        await server.WaitPost(() =>
        {
            coords = mapData.GridCoords;
            technician = entityManager.SpawnEntity("MobHuman", coords);
            patient = entityManager.SpawnEntity("MobHuman", coords);
            screwdriver = entityManager.SpawnEntity("Screwdriver", coords);
            wrench = entityManager.SpawnEntity("Wrench", coords);

            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, patient, coords);
            cyberArm = bodySystem.GetAllOrgans(patient).First(o =>
                entityManager.HasComponent<CyberLimbComponent>(o));
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.HasComponent<IntegrityPenaltyComponent>(cyberArm), Is.False,
                "Cyber limb should have no penalty before maintenance");
        });

        await server.WaitPost(() =>
        {
            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, screwdriver, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.TryGetComponent(cyberArm, out IntegrityPenaltyComponent? penalty), Is.True);
            Assert.That(penalty!.Penalty, Is.EqualTo(1), "Expose should add +1 penalty per cyber limb");
        });

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wrench, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wrench, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.TryGetComponent(cyberArm, out IntegrityPenaltyComponent? penalty), Is.True);
            Assert.That(penalty!.Penalty, Is.EqualTo(2), "Open should add +1 more penalty");
        });

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, wrench, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, wrench, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.TryGetComponent(cyberArm, out IntegrityPenaltyComponent? penalty), Is.True);
            Assert.That(penalty!.Penalty, Is.EqualTo(1), "Close should remove 1 penalty");
        });

        await server.WaitPost(() =>
        {
            handsSystem.TryDrop(technician, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(technician, screwdriver, checkActionBlocker: false);
            var ev = new InteractUsingEvent(technician, screwdriver, patient, coords);
            entityManager.EventBus.RaiseLocalEvent(patient, ev);
        });
        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.HasComponent<IntegrityPenaltyComponent>(cyberArm), Is.False,
                "Secure should clear all penalties from cyber limb");
        });

        await pair.CleanReturnAsync();
    }
}
