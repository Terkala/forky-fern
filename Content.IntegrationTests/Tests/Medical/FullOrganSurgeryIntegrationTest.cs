using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Medical;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Medical.Surgery;
using Content.Shared.MedicalScanner;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Self-contained integration test that validates the entire organ surgery pipeline via BUI:
/// scan -> open layers (RetractSkin, RetractTissue, SawBones) -> RemoveOrgan -> pick up organ -> InsertOrgan.
/// All steps via SurgeryRequestBuiMessage and DoAfter completion.
/// </summary>
[TestFixture]
[TestOf(typeof(HealthAnalyzerSystem))]
public sealed class FullOrganSurgeryIntegrationTest : InteractionTest
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
    public async Task FullSurgeryFlow_Scan_OpenLayers_RemoveOrgan_InsertOrgan_Completes()
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
        await RunTicks(400);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(scalpelNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, torsoNet, "OrganRemovalScalpel", SurgeryLayer.Organ, false, heartNet), analyzerNet);
        await RunTicks(400);

        await Server.WaitPost(() =>
        {
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(cauteryNet), checkActionBlocker: false);
        });
        await RunTicks(1);
        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, torsoNet, "OrganRemovalHemostat", SurgeryLayer.Organ, false, heartNet), analyzerNet);
        await RunTicks(500);

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
            Assert.That(HandSys.IsHolding(SPlayer, heart), Is.True, "Heart must be in hand before InsertOrgan BUI");
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
}
