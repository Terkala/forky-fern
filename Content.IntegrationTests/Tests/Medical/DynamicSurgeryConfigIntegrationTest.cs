using System.Linq;
using Content.Server.Medical;
using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Hands.Components;
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
            var isCyberLimb = proto.SkinOpenSteps.Count == 0;
            if (isCyberLimb)
            {
                // Cyber limbs: attach/detach only, no skin/tissue steps
                Assert.That(proto.OrganSteps, Is.Not.Empty, $"{proto.ID} (cyber limb) should have organSteps");
            }
            else
            {
                Assert.That(proto.SkinOpenSteps, Is.Not.Empty, $"{proto.ID} should have skinOpenSteps");
                Assert.That(proto.SkinCloseSteps, Is.Not.Empty, $"{proto.ID} should have skinCloseSteps");
                Assert.That(proto.TissueOpenSteps, Is.Not.Empty, $"{proto.ID} should have tissueOpenSteps");
                Assert.That(proto.TissueCloseSteps, Is.Not.Empty, $"{proto.ID} should have tissueCloseSteps");
                Assert.That(proto.OrganSteps, Is.Not.Empty, $"{proto.ID} should have organSteps");
            }
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
        EntityUid surgeon = default;
        EntityUid analyzer = default;

        await server.WaitPost(() =>
        {
            surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            var scalpel = entityManager.SpawnEntity("Scalpel", mapData.GridCoords);
            torso = GetTorso(entityManager, patient);
            handsSystem.TryPickupAnyHand(surgeon, analyzer, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false);

            // CreateIncision requires CuttingTool in active hand for DoAfter
            var hands = entityManager.GetComponent<HandsComponent>(surgeon);
            foreach (var hand in handsSystem.EnumerateHands((surgeon, hands)))
            {
                if (handsSystem.TryGetHeldItem((surgeon, hands), hand, out var held) && held == scalpel)
                {
                    handsSystem.TrySetActiveHand((surgeon, hands), hand);
                    break;
                }
            }

            var layerComp = entityManager.EnsureComponent<SurgeryLayerComponent>(torso);
            var stepsConfig = surgeryLayer.GetStepsConfig(patient, torso);
            Assert.That(stepsConfig, Is.Not.Null);

            // RetractTissue rejected when skin not open
            Assert.That(surgeryLayer.CanPerformStep("RetractTissue", SurgeryLayer.Tissue, layerComp, stepsConfig!), Is.False);

            // CutBone rejected when tissue not open
            Assert.That(surgeryLayer.CanPerformStep("CutBone", SurgeryLayer.Tissue, layerComp, stepsConfig!), Is.False);

            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"CreateIncision", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True, $"CreateIncision: {reqEv.RejectReason}");
        });

        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            // ClampVessels requires Wirecutter; RetractSkin requires PryingTool (retractor)
            var wirecutter = entityManager.SpawnEntity("Wirecutter", mapData.GridCoords);
            var hands = entityManager.GetComponent<HandsComponent>(surgeon);
            handsSystem.TryDrop((surgeon, hands), targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, wirecutter, checkActionBlocker: false);
            foreach (var hand in handsSystem.EnumerateHands((surgeon, hands)))
            {
                if (handsSystem.TryGetHeldItem((surgeon, hands), hand, out var held) && held == wirecutter)
                {
                    handsSystem.TrySetActiveHand((surgeon, hands), hand);
                    break;
                }
            }
            var clampEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"ClampVessels", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref clampEv);
            Assert.That(clampEv.Valid, Is.True, $"ClampVessels: {clampEv.RejectReason}");
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            var retractor = entityManager.SpawnEntity("Retractor", mapData.GridCoords);
            var hands = entityManager.GetComponent<HandsComponent>(surgeon);
            handsSystem.TryDrop((surgeon, hands), targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, retractor, checkActionBlocker: false);
            foreach (var hand in handsSystem.EnumerateHands((surgeon, hands)))
            {
                if (handsSystem.TryGetHeldItem((surgeon, hands), hand, out var held) && held == retractor)
                {
                    handsSystem.TrySetActiveHand((surgeon, hands), hand);
                    break;
                }
            }
            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"RetractSkin", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True, $"RetractSkin: {reqEv.RejectReason}");
        });
        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            var layerComp = entityManager.GetComponent<SurgeryLayerComponent>(torso);
            var stepsConfig = surgeryLayer.GetStepsConfig(patient, torso)!;
            // After CreateIncision, ClampVessels, RetractSkin - skin is open. CutBone (first tissue step) is now available
            Assert.That(surgeryLayer.CanPerformStep("CutBone", SurgeryLayer.Tissue, layerComp, stepsConfig), Is.True);
            // ReleaseRetractor (skin close) requires RetractSkin done
            Assert.That(surgeryLayer.CanPerformStep("ReleaseRetractor", SurgeryLayer.Skin, layerComp, stepsConfig), Is.True);
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
        EntityUid scalpel = default;
        EntityUid hemostat = default;
        EntityUid surgeon = default;

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

        EntityUid analyzer = default;

        await server.WaitPost(() =>
        {
            surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            scalpel = entityManager.SpawnEntity("Scalpel", mapData.GridCoords);
            var saw = entityManager.SpawnEntity("Saw", mapData.GridCoords);
            hemostat = entityManager.SpawnEntity("Hemostat", mapData.GridCoords);
            torso = GetTorso(entityManager, patient);
            handsSystem.TryPickupAnyHand(surgeon, analyzer, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false);

            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"CreateIncision", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True, $"CreateIncision: {reqEv.RejectReason}");
        });
        await pair.RunTicksSync(350);

        await server.WaitPost(() =>
        {
            var wirecutter = entityManager.SpawnEntity("Wirecutter", mapData.GridCoords);
            handsSystem.TryDrop(surgeon, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, wirecutter, checkActionBlocker: false);
            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"ClampVessels", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True, $"ClampVessels: {reqEv.RejectReason}");
        });
        await pair.RunTicksSync(250);

        await server.WaitPost(() =>
        {
            var retractor = entityManager.SpawnEntity("Retractor", mapData.GridCoords);
            handsSystem.TryDrop(surgeon, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, retractor, checkActionBlocker: false);
            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"RetractSkin", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True, $"RetractSkin: {reqEv.RejectReason}");
        });
        await pair.RunTicksSync(250);

        await server.WaitAssertion(() =>
        {
            var layerComp = entityManager.GetComponent<SurgeryLayerComponent>(torso);
            var stepsConfig = surgeryLayer.GetStepsConfig(patient, torso)!;
            Assert.That(surgeryLayer.IsSkinOpen(layerComp, stepsConfig), Is.True);
            Assert.That(surgeryLayer.IsTissueOpen(layerComp, stepsConfig), Is.False);
        });

        await server.WaitPost(() =>
        {
            var saw = entityManager.SpawnEntity("Saw", mapData.GridCoords);
            handsSystem.TryDrop(surgeon, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, saw, checkActionBlocker: false);
            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"CutBone", SurgeryLayer.Tissue, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True, $"CutBone: {reqEv.RejectReason}");
        });
        await pair.RunTicksSync(250);

        await server.WaitPost(() =>
        {
            var cautery = entityManager.SpawnEntity("Cautery", mapData.GridCoords);
            handsSystem.TryDrop(surgeon, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, cautery, checkActionBlocker: false);
            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"MarrowBleeding", SurgeryLayer.Tissue, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True);
        });
        await pair.RunTicksSync(250);

        await server.WaitPost(() =>
        {
            var retractor = entityManager.SpawnEntity("Retractor", mapData.GridCoords);
            handsSystem.TryDrop(surgeon, targetDropLocation: null, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, retractor, checkActionBlocker: false);
            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"RetractTissue", SurgeryLayer.Tissue, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True);
        });
        await pair.RunTicksSync(250);

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
        Assert.That(humanTorso!.SkinOpenSteps, Does.Contain(new ProtoId<SurgeryProcedurePrototype>("CreateIncision")));
        Assert.That(humanTorso.SkinOpenSteps, Does.Contain(new ProtoId<SurgeryProcedurePrototype>("ClampVessels")));
        Assert.That(humanTorso.SkinOpenSteps, Does.Contain(new ProtoId<SurgeryProcedurePrototype>("RetractSkin")));
        Assert.That(humanTorso.SkinCloseSteps, Does.Contain(new ProtoId<SurgeryProcedurePrototype>("ReleaseRetractor")));
        Assert.That(humanTorso.SkinCloseSteps, Does.Contain(new ProtoId<SurgeryProcedurePrototype>("ReconnectVessels")));
        Assert.That(humanTorso.SkinCloseSteps, Does.Contain(new ProtoId<SurgeryProcedurePrototype>("SealSkin")));
        Assert.That(humanTorso.TissueOpenSteps, Does.Contain(new ProtoId<SurgeryProcedurePrototype>("CutBone")));
        Assert.That(humanTorso.TissueOpenSteps, Does.Contain(new ProtoId<SurgeryProcedurePrototype>("MarrowBleeding")));
        Assert.That(humanTorso.TissueOpenSteps, Does.Contain(new ProtoId<SurgeryProcedurePrototype>("RetractTissue")));
        Assert.That(humanTorso.TissueCloseSteps, Does.Contain(new ProtoId<SurgeryProcedurePrototype>("MaintainAlignment")));
        Assert.That(humanTorso.TissueCloseSteps, Does.Contain(new ProtoId<SurgeryProcedurePrototype>("SealBleedPoints")));
        Assert.That(humanTorso.TissueCloseSteps, Does.Contain(new ProtoId<SurgeryProcedurePrototype>("RepairBoneSection")));
        Assert.That(humanTorso.OrganSteps, Does.Contain(new ProtoId<SurgeryProcedurePrototype>("InsertOrgan")));

        var humanLegId = new ProtoId<BodyPartSurgeryStepsPrototype>("HumanLegLeft");
        Assert.That(prototypes.TryIndex(humanLegId, out var humanLeg), Is.True);
        Assert.That(humanLeg!.OrganSteps, Does.Contain(new ProtoId<SurgeryProcedurePrototype>("DetachLimb")));
        Assert.That(humanLeg.OrganSteps, Does.Contain(new ProtoId<SurgeryProcedurePrototype>("AttachLimb")));

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

        EntityUid scalpel2 = default;
        EntityUid hemostat2 = default;

        await server.WaitPost(() =>
        {
            var surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            scalpel2 = entityManager.SpawnEntity("Scalpel", mapData.GridCoords);
            var saw = entityManager.SpawnEntity("Saw", mapData.GridCoords);
            hemostat2 = entityManager.SpawnEntity("Hemostat", mapData.GridCoords);
            torso = GetTorso(entityManager, patient);
            handsSystem.TryPickupAnyHand(surgeon, scalpel2, checkActionBlocker: false);

            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"CreateIncision", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True, "CreateIncision should be valid");
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            var surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            var wirecutter = entityManager.SpawnEntity("Wirecutter", mapData.GridCoords);
            handsSystem.TryPickupAnyHand(surgeon, wirecutter, checkActionBlocker: false);
            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"ClampVessels", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True, "ClampVessels should be valid");
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            var surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            var retractor = entityManager.SpawnEntity("Retractor", mapData.GridCoords);
            handsSystem.TryPickupAnyHand(surgeon, retractor, checkActionBlocker: false);
            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"RetractSkin", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True, "RetractSkin should be valid");
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            var surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            handsSystem.TryPickupAnyHand(surgeon, entityManager.SpawnEntity("Saw", mapData.GridCoords), checkActionBlocker: false);
            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"CutBone", SurgeryLayer.Tissue, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True, "CutBone should be valid");
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            var surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            handsSystem.TryPickupAnyHand(surgeon, entityManager.SpawnEntity("Cautery", mapData.GridCoords), checkActionBlocker: false);
            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"MarrowBleeding", SurgeryLayer.Tissue, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True, "MarrowBleeding should be valid");
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            var surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            handsSystem.TryPickupAnyHand(surgeon, entityManager.SpawnEntity("Retractor", mapData.GridCoords), checkActionBlocker: false);
            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"RetractTissue", SurgeryLayer.Tissue, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True, "RetractTissue should be valid");
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

            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"CreateIncision", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True);
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            var surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            var wirecutter = entityManager.SpawnEntity("Wirecutter", mapData.GridCoords);
            handsSystem.TryPickupAnyHand(surgeon, analyzer, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, wirecutter, checkActionBlocker: false);
            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"ClampVessels", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True);
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            var surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            var retractor = entityManager.SpawnEntity("Retractor", mapData.GridCoords);
            handsSystem.TryPickupAnyHand(surgeon, analyzer, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, retractor, checkActionBlocker: false);
            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"RetractSkin", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True);
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            var surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            var retractor = entityManager.SpawnEntity("Retractor", mapData.GridCoords);
            handsSystem.TryPickupAnyHand(surgeon, analyzer, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, retractor, checkActionBlocker: false);
            var reqEv = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, (ProtoId<SurgeryProcedurePrototype>)"ReleaseRetractor", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref reqEv);
            Assert.That(reqEv.Valid, Is.True);
        });
        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            var layerComp = entityManager.GetComponent<SurgeryLayerComponent>(torso);
            var stepsConfig = surgeryLayer.GetStepsConfig(patient, torso)!;
            Assert.That(surgeryLayer.CanPerformStep("RetractSkin", SurgeryLayer.Skin, layerComp, stepsConfig), Is.True,
                "RetractSkin should be available again after ReleaseRetractor");
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
            Assert.That(available, Does.Contain("CreateIncision"), "CreateIncision should be available when skin closed");
            Assert.That(available, Does.Not.Contain("ReleaseRetractor"), "ReleaseRetractor should not be available when skin closed");
        });
        await pair.CleanReturnAsync();
    }
}
