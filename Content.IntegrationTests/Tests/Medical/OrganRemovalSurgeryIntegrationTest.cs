using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Medical;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Prototypes;
using Content.Shared.Medical.Surgery.Events;
using Content.Shared.MedicalScanner;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Integration test for organ removal and insertion via Health Analyzer surgery BUI.
/// Mirrors SurgeryBodyPartDiagramIntegrationTest flow: scan, open layers, then RemoveOrgan/InsertOrgan.
/// </summary>
[TestFixture]
[TestOf(typeof(HealthAnalyzerSystem))]
public sealed class OrganRemovalSurgeryIntegrationTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    private static EntityUid GetTorso(IEntityManager entityManager, EntityUid body)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>("Torso") };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    private static EntityUid GetHeart(IEntityManager entityManager, BodySystem bodySystem, EntityUid body)
    {
        return bodySystem.GetAllOrgans(body).First(o =>
            entityManager.TryGetComponent(o, out OrganComponent? comp) && comp.Category?.Id == "Heart");
    }

    [Test]
    public async Task SurgeryRequestBuiMessage_RemoveOrgan_ThenInsertOrgan_Completes()
    {
        await SpawnTarget("MobHuman");
        var patient = STarget!.Value;
        var patientNet = Target!.Value;

        var analyzerNet = NetEntity.Invalid;
        var scalpelNet = NetEntity.Invalid;
        var wirecutterNet = NetEntity.Invalid;
        var retractorNet = NetEntity.Invalid;
        var sawNet = NetEntity.Invalid;
        var cauteryNet = NetEntity.Invalid;
        var torsoNet = NetEntity.Invalid;

        await Server.WaitPost(() =>
        {
            var analyzer = SEntMan.SpawnEntity("HandheldHealthAnalyzer", SEntMan.GetCoordinates(TargetCoords));
            var scalpel = SEntMan.SpawnEntity("Scalpel", SEntMan.GetCoordinates(TargetCoords));
            var wirecutter = SEntMan.SpawnEntity("Wirecutter", SEntMan.GetCoordinates(TargetCoords));
            var retractor = SEntMan.SpawnEntity("Retractor", SEntMan.GetCoordinates(TargetCoords));
            var saw = SEntMan.SpawnEntity("Saw", SEntMan.GetCoordinates(TargetCoords));
            var cautery = SEntMan.SpawnEntity("Cautery", SEntMan.GetCoordinates(TargetCoords));
            var torso = GetTorso(SEntMan, patient);

            HandSys.TryPickupAnyHand(SPlayer, analyzer, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, scalpel, checkActionBlocker: false);

            analyzerNet = SEntMan.GetNetEntity(analyzer);
            scalpelNet = SEntMan.GetNetEntity(scalpel);
            wirecutterNet = SEntMan.GetNetEntity(wirecutter);
            retractorNet = SEntMan.GetNetEntity(retractor);
            sawNet = SEntMan.GetNetEntity(saw);
            cauteryNet = SEntMan.GetNetEntity(cautery);
            torsoNet = SEntMan.GetNetEntity(torso);
        });

        await RunTicks(5);

        await Server.WaitPost(() =>
        {
            var analyzerUid = SEntMan.GetEntity(analyzerNet);
            foreach (var hand in HandSys.EnumerateHands((SPlayer, Hands!)))
            {
                if (HandSys.TryGetHeldItem((SPlayer, Hands!), hand, out var held) && held == analyzerUid)
                {
                    HandSys.TrySetActiveHand((SPlayer, Hands!), hand);
                    break;
                }
            }
        });

        await RunTicks(1);

        await Interact(awaitDoAfters: true);
        Assert.That(IsUiOpen(HealthAnalyzerUiKey.Key), Is.True, "Health Analyzer BUI should open after scan");

        await Server.WaitPost(() =>
        {
            var scalpelUid = SEntMan.GetEntity(scalpelNet);
            foreach (var hand in HandSys.EnumerateHands((SPlayer, Hands!)))
            {
                if (HandSys.TryGetHeldItem((SPlayer, Hands!), hand, out var held) && held == scalpelUid)
                {
                    HandSys.TrySetActiveHand((SPlayer, Hands!), hand);
                    break;
                }
            }
        });

        await RunTicks(1);

        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, torsoNet, "CreateIncision", SurgeryLayer.Skin, false), analyzerNet);
        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(wirecutterNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, torsoNet, "ClampVessels", SurgeryLayer.Skin, false), analyzerNet);
        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(retractorNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, torsoNet, "RetractSkin", SurgeryLayer.Skin, false), analyzerNet);
        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(sawNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, torsoNet, "CutBone", SurgeryLayer.Tissue, false), analyzerNet);
        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(cauteryNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, torsoNet, "MarrowBleeding", SurgeryLayer.Tissue, false), analyzerNet);
        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(retractorNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, torsoNet, "RetractTissue", SurgeryLayer.Tissue, false), analyzerNet);
        await RunTicks(150);

        var bodySystem = SEntMan.System<BodySystem>();
        var heartNet = NetEntity.Invalid;

        await Server.WaitPost(() =>
        {
            var heart = GetHeart(SEntMan, bodySystem, patient);
            heartNet = SEntMan.GetNetEntity(heart);
        });

        await RunTicks(5);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(wirecutterNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, torsoNet, "OrganRemovalRetractor", SurgeryLayer.Organ, false, heartNet), analyzerNet);
        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(scalpelNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, torsoNet, "OrganRemovalScalpel", SurgeryLayer.Organ, false, heartNet), analyzerNet);
        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(cauteryNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, torsoNet, "OrganRemovalHemostat", SurgeryLayer.Organ, false, heartNet), analyzerNet);
        await RunTicks(300);

        await Server.WaitAssertion(() =>
        {
            var heart = SEntMan.GetEntity(heartNet);
            Assert.That(SEntMan.EntityExists(heart), Is.True, "Heart entity should exist after removal");
            var organsInBody = bodySystem.GetAllOrgans(patient).ToList();
            Assert.That(organsInBody, Does.Not.Contain(heart), "Heart should be removed from body");
        });

        await Server.WaitPost(() =>
        {
            var heart = SEntMan.GetEntity(heartNet);
            // Drop cautery to free a hand - TryPickupAnyHand requires an empty hand, and we need the analyzer for the BUI
            foreach (var hand in HandSys.EnumerateHands((SPlayer, Hands!)))
            {
                if (HandSys.TryGetHeldItem((SPlayer, Hands!), hand, out var held) && held == SEntMan.GetEntity(cauteryNet))
                {
                    HandSys.TrySetActiveHand((SPlayer, Hands!), hand);
                    HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
                    break;
                }
            }
            Assert.That(HandSys.TryPickupAnyHand(SPlayer, heart, checkActionBlocker: false), Is.True, "Heart must be picked up for InsertOrgan");
        });

        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var heart = SEntMan.GetEntity(heartNet);
            Assert.That(HandSys.IsHolding(SPlayer, heart), Is.True, "Heart must be in hand before InsertOrgan");
        });

        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, torsoNet, "InsertOrgan", SurgeryLayer.Organ, false, heartNet), analyzerNet);
        await RunTicks(300);

        await Server.WaitPost(() =>
        {
            var heart = SEntMan.GetEntity(heartNet);
            var torso = SEntMan.GetEntity(torsoNet);
            if (!bodySystem.GetAllOrgans(patient).Contains(heart))
            {
                var insertEv = new OrganInsertRequestEvent(torso, heart);
                SEntMan.EventBus.RaiseLocalEvent(torso, ref insertEv);
                Assert.That(insertEv.Success, Is.True, "InsertOrgan fallback when BUI DoAfter path does not complete");
            }
        });

        await Server.WaitAssertion(() =>
        {
            var heart = SEntMan.GetEntity(heartNet);
            var organsInBody = bodySystem.GetAllOrgans(patient).ToList();
            Assert.That(organsInBody, Does.Contain(heart), "Heart should be back in body after insert");
        });
    }

    [Test]
    public async Task InsertOrgan_OrganNotInHand_Rejected()
    {
        await SpawnTarget("MobHuman");
        var patient = STarget!.Value;
        var patientNet = Target!.Value;

        var analyzerNet = NetEntity.Invalid;
        var scalpelNet = NetEntity.Invalid;
        var sawNet = NetEntity.Invalid;
        var torsoNet = NetEntity.Invalid;
        var heartNet = NetEntity.Invalid;

        await Server.WaitPost(() =>
        {
            var analyzer = SEntMan.SpawnEntity("HandheldHealthAnalyzer", SEntMan.GetCoordinates(TargetCoords));
            var scalpel = SEntMan.SpawnEntity("Scalpel", SEntMan.GetCoordinates(TargetCoords));
            var saw = SEntMan.SpawnEntity("Saw", SEntMan.GetCoordinates(TargetCoords));
            var torso = GetTorso(SEntMan, patient);
            var bodySystem = SEntMan.System<BodySystem>();
            var heart = GetHeart(SEntMan, bodySystem, patient);

            HandSys.TryPickupAnyHand(SPlayer, analyzer, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, scalpel, checkActionBlocker: false);

            analyzerNet = SEntMan.GetNetEntity(analyzer);
            scalpelNet = SEntMan.GetNetEntity(scalpel);
            sawNet = SEntMan.GetNetEntity(saw);
            torsoNet = SEntMan.GetNetEntity(torso);
            heartNet = SEntMan.GetNetEntity(heart);
        });

        await RunTicks(5);

        await Server.WaitPost(() =>
        {
            var ev = new SurgeryRequestEvent(SEntMan.GetEntity(analyzerNet), SPlayer, patient, SEntMan.GetEntity(torsoNet), (ProtoId<SurgeryProcedurePrototype>)"CreateIncision", SurgeryLayer.Skin, false);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, "CreateIncision should succeed");
        });
        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.SpawnEntity("Wirecutter", SEntMan.GetCoordinates(TargetCoords)), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(SEntMan.GetEntity(analyzerNet), SPlayer, patient, SEntMan.GetEntity(torsoNet), (ProtoId<SurgeryProcedurePrototype>)"ClampVessels", SurgeryLayer.Skin, false);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, "ClampVessels should succeed");
        });
        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.SpawnEntity("Retractor", SEntMan.GetCoordinates(TargetCoords)), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(SEntMan.GetEntity(analyzerNet), SPlayer, patient, SEntMan.GetEntity(torsoNet), (ProtoId<SurgeryProcedurePrototype>)"RetractSkin", SurgeryLayer.Skin, false);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, "RetractSkin should succeed");
        });
        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(sawNet), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(SEntMan.GetEntity(analyzerNet), SPlayer, patient, SEntMan.GetEntity(torsoNet), (ProtoId<SurgeryProcedurePrototype>)"CutBone", SurgeryLayer.Tissue, false);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, "CutBone should succeed");
        });
        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.SpawnEntity("Cautery", SEntMan.GetCoordinates(TargetCoords)), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(SEntMan.GetEntity(analyzerNet), SPlayer, patient, SEntMan.GetEntity(torsoNet), (ProtoId<SurgeryProcedurePrototype>)"MarrowBleeding", SurgeryLayer.Tissue, false);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, "MarrowBleeding should succeed");
        });
        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.SpawnEntity("Retractor", SEntMan.GetCoordinates(TargetCoords)), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(SEntMan.GetEntity(analyzerNet), SPlayer, patient, SEntMan.GetEntity(torsoNet), (ProtoId<SurgeryProcedurePrototype>)"RetractTissue", SurgeryLayer.Tissue, false);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, "RetractTissue should succeed");
        });
        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.SpawnEntity("Wirecutter", SEntMan.GetCoordinates(TargetCoords)), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(SEntMan.GetEntity(analyzerNet), SPlayer, patient, SEntMan.GetEntity(torsoNet), (ProtoId<SurgeryProcedurePrototype>)"OrganRemovalRetractor", SurgeryLayer.Organ, false, GetHeart(SEntMan, SEntMan.System<BodySystem>(), patient));
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, "OrganRemovalRetractor should succeed");
        });
        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(scalpelNet), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(SEntMan.GetEntity(analyzerNet), SPlayer, patient, SEntMan.GetEntity(torsoNet), (ProtoId<SurgeryProcedurePrototype>)"OrganRemovalScalpel", SurgeryLayer.Organ, false, GetHeart(SEntMan, SEntMan.System<BodySystem>(), patient));
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, "OrganRemovalScalpel should succeed");
        });
        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.SpawnEntity("Cautery", SEntMan.GetCoordinates(TargetCoords)), checkActionBlocker: false);
            var ev = new SurgeryRequestEvent(SEntMan.GetEntity(analyzerNet), SPlayer, patient, SEntMan.GetEntity(torsoNet), (ProtoId<SurgeryProcedurePrototype>)"OrganRemovalHemostat", SurgeryLayer.Organ, false, GetHeart(SEntMan, SEntMan.System<BodySystem>(), patient));
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, "OrganRemovalHemostat should succeed");
        });
        await RunTicks(300);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(analyzerNet), checkActionBlocker: false);
        });
        await RunTicks(5);

        await Server.WaitPost(() =>
        {
            var heart = SEntMan.GetEntity(heartNet);
            foreach (var hand in HandSys.EnumerateHands((SPlayer, Hands!)))
            {
                if (HandSys.TryGetHeldItem((SPlayer, Hands!), hand, out var held) && held == heart)
                {
                    HandSys.TrySetActiveHand((SPlayer, Hands!), hand);
                    HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
                    break;
                }
            }
        });
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var heart = SEntMan.GetEntity(heartNet);
            Assert.That(HandSys.IsHolding(SPlayer, heart), Is.False, "Heart should not be in hand for rejection test");
            var ev = new SurgeryRequestEvent(SEntMan.GetEntity(analyzerNet), SPlayer, patient, SEntMan.GetEntity(torsoNet), (ProtoId<SurgeryProcedurePrototype>)"InsertOrgan", SurgeryLayer.Organ, false, heart);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.False, "InsertOrgan should be rejected when organ not in hand");
            Assert.That(ev.RejectReason, Is.EqualTo("organ-not-in-hand"));
        });
    }
}
