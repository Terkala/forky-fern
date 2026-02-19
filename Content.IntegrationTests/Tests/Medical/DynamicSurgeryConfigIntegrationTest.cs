using System.Linq;
using Content.Server.Medical;
using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.Medical.Surgery.Events;
using Content.Shared.Medical.Surgery.Prototypes;
using Content.Shared.MedicalScanner;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
[TestOf(typeof(BodyPartSurgeryStepsPrototype))]
[TestOf(typeof(SurgeryLayerSystem))]
[TestOf(typeof(HealthAnalyzerSystem))]
public sealed class DynamicSurgeryConfigIntegrationTest
{
    private static EntityUid GetTorso(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("Torso") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    [Test]
    public async Task BodyPartSurgeryStepsPrototype_HasNewSchema_LoadsCorrectly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();

        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var count = 0;
        foreach (var proto in prototypes.EnumeratePrototypes<BodyPartSurgeryStepsPrototype>())
        {
            count++;
            Assert.That(proto.SkinOpenSteps, Is.Not.Empty, $"{proto.ID} should have skinOpenSteps");
            Assert.That(proto.SkinCloseSteps, Is.Not.Empty, $"{proto.ID} should have skinCloseSteps");
            Assert.That(proto.TissueOpenSteps, Is.Not.Empty, $"{proto.ID} should have tissueOpenSteps");
            Assert.That(proto.TissueCloseSteps, Is.Not.Empty, $"{proto.ID} should have tissueCloseSteps");
            Assert.That(proto.OrganSteps, Is.Not.Empty, $"{proto.ID} should have organSteps");
            Assert.That(proto.SkinSteps, Is.Empty, $"{proto.ID} should not use deprecated skinSteps");
            Assert.That(proto.TissueSteps, Is.Empty, $"{proto.ID} should not use deprecated tissueSteps");
        }
        Assert.That(count, Is.GreaterThan(0), "Should have at least one BodyPartSurgeryStepsPrototype");
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CanPerformStep_RespectsConfigDrivenPrerequisites()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = false });
        var server = pair.Server;
        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var surgeryLayer = entityManager.System<SurgeryLayerSystem>();
        var handsSystem = entityManager.System<SharedHandsSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid patient = default;
        EntityUid torso = default;

        await server.WaitPost(() =>
        {
            var surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            var scalpel = entityManager.SpawnEntity("Scalpel", mapData.GridCoords);
            torso = GetTorso(entityManager, patient);
            handsSystem.TryPickupAnyHand(surgeon, analyzer, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false);

            var layerComp = entityManager.EnsureComponent<SurgeryLayerComponent>(torso);
            var stepsConfig = surgeryLayer.GetStepsConfig(patient, torso);
            Assert.That(stepsConfig, Is.Not.Null);

            // RetractTissue rejected when skin not open
            Assert.That(surgeryLayer.CanPerformStep("RetractTissue", SurgeryLayer.Tissue, layerComp, stepsConfig!), Is.False);

            // SawBones rejected when tissue not open
            Assert.That(surgeryLayer.CanPerformStep("SawBones", SurgeryLayer.Tissue, layerComp, stepsConfig!), Is.False);

            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, "RetractSkin", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True);
        });

        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            var layerComp = entityManager.GetComponent<SurgeryLayerComponent>(torso);
            var stepsConfig = surgeryLayer.GetStepsConfig(patient, torso)!;
            // After RetractSkin, SawBones still rejected (RetractTissue not done)
            Assert.That(surgeryLayer.CanPerformStep("SawBones", SurgeryLayer.Tissue, layerComp, stepsConfig), Is.False);
            // CloseIncision requires all skin open steps done
            Assert.That(surgeryLayer.CanPerformStep("CloseIncision", SurgeryLayer.Skin, layerComp, stepsConfig), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LayerState_ComputedFromConfig_NotHardcoded()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = false });
        var server = pair.Server;
        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var surgeryLayer = entityManager.System<SurgeryLayerSystem>();
        var handsSystem = entityManager.System<SharedHandsSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid patient = default;
        EntityUid torso = default;

        await server.WaitAssertion(() =>
        {
            var patient2 = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var torso2 = GetTorso(entityManager, patient2);
            var layerComp = entityManager.EnsureComponent<SurgeryLayerComponent>(torso2);
            var stepsConfig = surgeryLayer.GetStepsConfig(patient2, torso2);
            Assert.That(stepsConfig, Is.Not.Null);

            Assert.That(surgeryLayer.IsSkinOpen(layerComp, stepsConfig!), Is.False);
            Assert.That(surgeryLayer.IsTissueOpen(layerComp, stepsConfig!), Is.False);
            Assert.That(surgeryLayer.IsOrganLayerOpen(layerComp, stepsConfig!), Is.False);
        });

        await server.WaitPost(() =>
        {
            var surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            var scalpel = entityManager.SpawnEntity("Scalpel", mapData.GridCoords);
            var saw = entityManager.SpawnEntity("Saw", mapData.GridCoords);
            torso = GetTorso(entityManager, patient);
            handsSystem.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false);

            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, "RetractSkin", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True);
        });
        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            var layerComp = entityManager.GetComponent<SurgeryLayerComponent>(torso);
            var stepsConfig = surgeryLayer.GetStepsConfig(patient, torso)!;
            Assert.That(surgeryLayer.IsSkinOpen(layerComp, stepsConfig), Is.True);
            Assert.That(surgeryLayer.IsTissueOpen(layerComp, stepsConfig), Is.False);
        });

        await server.WaitPost(() =>
        {
            var surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            var scalpel = entityManager.SpawnEntity("Scalpel", mapData.GridCoords);
            handsSystem.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false);
            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, "RetractTissue", SurgeryLayer.Tissue, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True);
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            var surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            handsSystem.TryPickupAnyHand(surgeon, entityManager.SpawnEntity("Saw", mapData.GridCoords), checkActionBlocker: false);
            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, "SawBones", SurgeryLayer.Tissue, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True);
        });
        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            var layerComp = entityManager.GetComponent<SurgeryLayerComponent>(torso);
            var stepsConfig = surgeryLayer.GetStepsConfig(patient, torso)!;
            Assert.That(surgeryLayer.IsTissueOpen(layerComp, stepsConfig), Is.True);
            Assert.That(surgeryLayer.IsOrganLayerOpen(layerComp, stepsConfig), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpeciesSurgerySteps_MigratedToNewSchema()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();

        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var humanTorsoId = new ProtoId<BodyPartSurgeryStepsPrototype>("HumanTorso");
        Assert.That(prototypes.TryIndex(humanTorsoId, out var humanTorso), Is.True);
        Assert.That(humanTorso!.SkinOpenSteps, Does.Contain(new ProtoId<SurgeryStepPrototype>("RetractSkin")));
        Assert.That(humanTorso.SkinCloseSteps, Does.Contain(new ProtoId<SurgeryStepPrototype>("CloseIncision")));
        Assert.That(humanTorso.TissueOpenSteps, Does.Contain(new ProtoId<SurgeryStepPrototype>("RetractTissue")));
        Assert.That(humanTorso.TissueOpenSteps, Does.Contain(new ProtoId<SurgeryStepPrototype>("SawBones")));
        Assert.That(humanTorso.TissueCloseSteps, Does.Contain(new ProtoId<SurgeryStepPrototype>("CloseTissue")));
        Assert.That(humanTorso.OrganSteps, Does.Contain(new ProtoId<SurgeryStepPrototype>("RemoveOrgan")));
        Assert.That(humanTorso.OrganSteps, Does.Contain(new ProtoId<SurgeryStepPrototype>("InsertOrgan")));

        var humanLegId = new ProtoId<BodyPartSurgeryStepsPrototype>("HumanLegLeft");
        Assert.That(prototypes.TryIndex(humanLegId, out var humanLeg), Is.True);
        Assert.That(humanLeg!.OrganSteps, Does.Contain(new ProtoId<SurgeryStepPrototype>("DetachLimb")));
        Assert.That(humanLeg.OrganSteps, Does.Contain(new ProtoId<SurgeryStepPrototype>("AttachLimb")));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BuildState_SendsSkinOpenTissueOpenOrganOpen_FromConfig()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = false });
        var server = pair.Server;
        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var healthAnalyzer = entityManager.System<HealthAnalyzerSystem>();
        var handsSystem = entityManager.System<SharedHandsSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid patient = default;
        EntityUid torso = default;

        await server.WaitPost(() =>
        {
            var surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            var scalpel = entityManager.SpawnEntity("Scalpel", mapData.GridCoords);
            var saw = entityManager.SpawnEntity("Saw", mapData.GridCoords);
            torso = GetTorso(entityManager, patient);
            handsSystem.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false);

            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, "RetractSkin", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True, "RetractSkin should be valid");
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            var surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            var scalpel = entityManager.SpawnEntity("Scalpel", mapData.GridCoords);
            handsSystem.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false);
            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, "RetractTissue", SurgeryLayer.Tissue, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True, "RetractTissue should be valid");
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            var surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            handsSystem.TryPickupAnyHand(surgeon, entityManager.SpawnEntity("Saw", mapData.GridCoords), checkActionBlocker: false);
            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, "SawBones", SurgeryLayer.Tissue, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True, "SawBones should be valid");
        });
        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            var uiState = healthAnalyzer.GetHealthAnalyzerUiState(patient);
            Assert.That(uiState.BodyPartLayerState, Is.Not.Empty);
            var torsoNet = entityManager.GetNetEntity(torso);
            var torsoLayer = uiState.BodyPartLayerState.FirstOrDefault(l => l.BodyPart == torsoNet);
            Assert.That(torsoLayer.BodyPart, Is.EqualTo(torsoNet));
            Assert.That(torsoLayer.SkinOpen, Is.True);
            Assert.That(torsoLayer.TissueOpen, Is.True);
            Assert.That(torsoLayer.OrganOpen, Is.True);
            Assert.That(torsoLayer.SkinProcedures, Is.Not.Empty);
            Assert.That(torsoLayer.TissueProcedures, Is.Not.Empty);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CanPerformStep_RetractSkin_AvailableAfterCloseIncision()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = false });
        var server = pair.Server;
        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var surgeryLayer = entityManager.System<SurgeryLayerSystem>();
        var handsSystem = entityManager.System<SharedHandsSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid patient = default;
        EntityUid torso = default;

        await server.WaitPost(() =>
        {
            var surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            var scalpel = entityManager.SpawnEntity("Scalpel", mapData.GridCoords);
            torso = GetTorso(entityManager, patient);
            handsSystem.TryPickupAnyHand(surgeon, analyzer, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false);

            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, "RetractSkin", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True);
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            var surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            var hemostat = entityManager.SpawnEntity("Hemostat", mapData.GridCoords);
            handsSystem.TryPickupAnyHand(surgeon, analyzer, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, hemostat, checkActionBlocker: false);
            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, "CloseIncision", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True);
        });
        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            var layerComp = entityManager.GetComponent<SurgeryLayerComponent>(torso);
            var stepsConfig = surgeryLayer.GetStepsConfig(patient, torso)!;
            Assert.That(surgeryLayer.CanPerformStep("RetractSkin", SurgeryLayer.Skin, layerComp, stepsConfig), Is.True,
                "RetractSkin should be available again after closing incision");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GetAvailableSteps_ReturnsCorrectStepsForState()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = false });
        var server = pair.Server;
        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var surgeryLayer = entityManager.System<SurgeryLayerSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid patient = default;
        EntityUid torso = default;

        await server.WaitAssertion(() =>
        {
            patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            torso = GetTorso(entityManager, patient);

            var available = surgeryLayer.GetAvailableSteps(patient, torso);
            Assert.That(available, Does.Contain("RetractSkin"), "RetractSkin should be available when skin closed");
            Assert.That(available, Does.Not.Contain("CloseIncision"), "CloseIncision should not be available when skin closed");
        });
        await pair.CleanReturnAsync();
    }
}
