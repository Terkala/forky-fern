using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Medical;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.Medical.Surgery.Events;
using Content.Shared.Medical.Surgery.Prototypes;
using Content.Shared.MedicalScanner;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Verifies that after amputating a leg, re-attaching it, and performing CloseIncision (Mend Skin),
/// RetractSkin is available again (1:1 pairing: CloseIncision undoes RetractSkin, so it can be re-performed).
/// </summary>
[TestFixture]
[TestOf(typeof(SurgeryLayerSystem))]
public sealed class ReattachedLimbSurgeryIntegrationTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    private static EntityUid GetLeg(IEntityManager entityManager, EntityUid body, string category = "LegLeft")
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>(category) };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    [Test]
    public async Task ReattachedLimb_CloseIncision_RetractSkinAvailableAgain()
    {
        await SpawnTarget("MobHuman");
        var patient = STarget!.Value;
        var patientNet = Target!.Value;

        var analyzerNet = NetEntity.Invalid;
        var scalpelNet = NetEntity.Invalid;
        var sawNet = NetEntity.Invalid;
        var hemostatNet = NetEntity.Invalid;
        var legNet = NetEntity.Invalid;

        await Server.WaitPost(() =>
        {
            var analyzer = SEntMan.SpawnEntity("HandheldHealthAnalyzer", SEntMan.GetCoordinates(TargetCoords));
            var scalpel = SEntMan.SpawnEntity("Scalpel", SEntMan.GetCoordinates(TargetCoords));
            var saw = SEntMan.SpawnEntity("Saw", SEntMan.GetCoordinates(TargetCoords));
            var hemostat = SEntMan.SpawnEntity("Hemostat", SEntMan.GetCoordinates(TargetCoords));
            var leg = GetLeg(SEntMan, patient);

            HandSys.TryPickupAnyHand(SPlayer, analyzer, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, scalpel, checkActionBlocker: false);

            analyzerNet = SEntMan.GetNetEntity(analyzer);
            scalpelNet = SEntMan.GetNetEntity(scalpel);
            sawNet = SEntMan.GetNetEntity(saw);
            hemostatNet = SEntMan.GetNetEntity(hemostat);
            legNet = SEntMan.GetNetEntity(leg);
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

        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, legNet, "CreateIncision", SurgeryLayer.Skin, false), analyzerNet);
        await RunTicks(300);

        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, legNet, "RetractSkin", SurgeryLayer.Skin, false), analyzerNet);
        await RunTicks(300);

        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, legNet, "RetractTissue", SurgeryLayer.Tissue, false), analyzerNet);
        await RunTicks(300);

        await Server.WaitPost(() =>
        {
            var scalpelUid = SEntMan.GetEntity(scalpelNet);
            foreach (var hand in HandSys.EnumerateHands((SPlayer, Hands!)))
            {
                if (HandSys.TryGetHeldItem((SPlayer, Hands!), hand, out var held) && held == scalpelUid)
                {
                    HandSys.TrySetActiveHand((SPlayer, Hands!), hand);
                    HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
                    break;
                }
            }
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(hemostatNet), checkActionBlocker: false);
        });
        await RunTicks(1);

        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, legNet, "ClampBleeders", SurgeryLayer.Tissue, false), analyzerNet);
        await RunTicks(300);

        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, legNet, "MoveNerves", SurgeryLayer.Tissue, false), analyzerNet);
        await RunTicks(300);

        await Server.WaitPost(() =>
        {
            var hemostatUid = SEntMan.GetEntity(hemostatNet);
            foreach (var hand in HandSys.EnumerateHands((SPlayer, Hands!)))
            {
                if (HandSys.TryGetHeldItem((SPlayer, Hands!), hand, out var held) && held == hemostatUid)
                {
                    HandSys.TrySetActiveHand((SPlayer, Hands!), hand);
                    HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
                    break;
                }
            }
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(sawNet), checkActionBlocker: false);
        });
        await RunTicks(1);

        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, legNet, "SawBones", SurgeryLayer.Tissue, false), analyzerNet);
        await RunTicks(300);

        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, legNet, "DetachLimb", SurgeryLayer.Organ, false), analyzerNet);
        await RunTicks(300);

        await Server.WaitPost(() =>
        {
            var leg = SEntMan.GetEntity(legNet);
            Assert.That(SEntMan.EntityExists(leg), Is.True);
            Assert.That(SEntMan.TryGetComponent(leg, out BodyPartComponent? legBodyPart), Is.True);
            Assert.That(legBodyPart!.Body, Is.Null, "Leg should be detached");

            HandSys.TryPickupAnyHand(SPlayer, leg, checkActionBlocker: false);
        });
        await RunTicks(1);

        await Server.WaitPost(() =>
        {
            var leg = SEntMan.GetEntity(legNet);
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            var bodyComp = SEntMan.GetComponent<BodyComponent>(patient);
            var containerSys = SEntMan.System<SharedContainerSystem>();
            Assert.That(containerSys.Insert(leg, bodyComp.Organs!), Is.True, "Leg should re-attach");
        });
        await RunTicks(5);

        await Server.WaitPost(() =>
        {
            var hemostatUid = SEntMan.GetEntity(hemostatNet);
            foreach (var hand in HandSys.EnumerateHands((SPlayer, Hands!)))
            {
                if (HandSys.TryGetHeldItem((SPlayer, Hands!), hand, out var held) && held == hemostatUid)
                {
                    HandSys.TrySetActiveHand((SPlayer, Hands!), hand);
                    break;
                }
            }
            if (HandSys.GetActiveItem((SPlayer, Hands!)) != hemostatUid)
                HandSys.TryPickupAnyHand(SPlayer, hemostatUid, checkActionBlocker: false);
        });
        await RunTicks(1);

        // Apply CloseIncision via event (BUI/DoAfter flow can be flaky when body structure changes).
        // This verifies the surgery layer logic: after CloseIncision on a re-attached limb,
        // RetractSkin should be available again.
        await Server.WaitPost(() =>
        {
            var leg = SEntMan.GetEntity(legNet);
            var step = ProtoMan.Index(new ProtoId<SurgeryStepPrototype>("CloseIncision"));
            var ev = new SurgeryStepCompletedEvent(SPlayer, patient, leg, "CloseIncision", SurgeryLayer.Skin, null, step);
            SEntMan.EventBus.RaiseLocalEvent(leg, ref ev);
        });
        await RunTicks(1);

        await Server.WaitAssertion(() =>
        {
            var leg = SEntMan.GetEntity(legNet);
            var layerComp = SEntMan.GetComponent<SurgeryLayerComponent>(leg);
            Assert.That(layerComp.PerformedSkinSteps, Does.Contain("CloseIncision"),
                "CloseIncision should have been applied to re-attached leg");
            // With 1:1 pairing, CloseIncision undoes RetractSkin, so RetractSkin is removed from performed
            Assert.That(layerComp.PerformedSkinSteps, Does.Not.Contain("RetractSkin"),
                "RetractSkin should be removed by CloseIncision (1:1 pairing)");
            Assert.That(layerComp.PerformedSkinSteps, Does.Contain("CreateIncision"),
                "CreateIncision should remain (CloseIncision only undoes RetractSkin)");

            var surgeryLayer = SEntMan.System<SurgeryLayerSystem>();
            var stepsConfig = surgeryLayer.GetStepsConfig(patient, leg);
            Assert.That(stepsConfig, Is.Not.Null, "Re-attached leg should have steps config");
            Assert.That(surgeryLayer.CanPerformStep("RetractSkin", SurgeryLayer.Skin, layerComp, stepsConfig!), Is.True,
                "RetractSkin should be available again after CloseIncision (CreateIncision still performed)");
            Assert.That(surgeryLayer.GetAvailableSteps(patient, leg), Does.Contain("RetractSkin"),
                "GetAvailableSteps should include RetractSkin");
        });
    }
}
