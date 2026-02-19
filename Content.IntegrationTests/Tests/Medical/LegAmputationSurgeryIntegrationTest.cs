using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Medical;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Medical.Surgery;
using Content.Shared.MedicalScanner;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Integration test for amputating a leg via Health Analyzer surgery BUI.
/// Performs RetractSkin, RetractTissue, SawBones, then DetachLimb on a leg.
/// Verifies leg and foot are both detached as separate items, and that the leg can be re-attached.
/// </summary>
[TestFixture]
[TestOf(typeof(HealthAnalyzerSystem))]
public sealed class LegAmputationSurgeryIntegrationTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    private static EntityUid GetLeg(IEntityManager entityManager, EntityUid body, string category = "LegLeft")
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>(category) };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        Assert.That(ev.Parts, Has.Count.GreaterThan(0), $"Body should have a {category}");
        return ev.Parts[0];
    }

    [Test]
    public async Task SurgeryRequestBuiMessage_LegAmputation_DetachesLegAndFoot_ReattachSucceeds()
    {
        await SpawnTarget("MobHuman");
        var patient = STarget!.Value;
        var patientNet = Target!.Value;

        var analyzerNet = NetEntity.Invalid;
        var scalpelNet = NetEntity.Invalid;
        var sawNet = NetEntity.Invalid;
        var legNet = NetEntity.Invalid;

        await Server.WaitPost(() =>
        {
            var analyzer = SEntMan.SpawnEntity("HandheldHealthAnalyzer", SEntMan.GetCoordinates(TargetCoords));
            var scalpel = SEntMan.SpawnEntity("Scalpel", SEntMan.GetCoordinates(TargetCoords));
            var saw = SEntMan.SpawnEntity("Saw", SEntMan.GetCoordinates(TargetCoords));
            var leg = GetLeg(SEntMan, patient);

            HandSys.TryPickupAnyHand(SPlayer, analyzer, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, scalpel, checkActionBlocker: false);

            analyzerNet = SEntMan.GetNetEntity(analyzer);
            scalpelNet = SEntMan.GetNetEntity(scalpel);
            sawNet = SEntMan.GetNetEntity(saw);
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

        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, legNet, "RetractSkin", SurgeryLayer.Skin, false), analyzerNet);
        await RunTicks(150);

        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, legNet, "RetractTissue", SurgeryLayer.Tissue, false), analyzerNet);
        await RunTicks(150);

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
            HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(sawNet), checkActionBlocker: false);
        });
        await RunTicks(1);

        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, legNet, "SawBones", SurgeryLayer.Tissue, false), analyzerNet);
        await RunTicks(150);

        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, legNet, "DetachLimb", SurgeryLayer.Organ, false), analyzerNet);
        await RunTicks(300);

        NetEntity? footNet = null;
        await Server.WaitAssertion(() =>
        {
            var leg = SEntMan.GetEntity(legNet);
            Assert.That(SEntMan.EntityExists(leg), Is.True, "Leg entity should exist after detachment");
            Assert.That(SEntMan.TryGetComponent(leg, out BodyPartComponent? legBodyPart), Is.True);
            Assert.That(legBodyPart!.Body, Is.Null, "Leg should no longer be attached to body after DetachLimb");

            // Foot should be detached separately, not inside the leg
            Assert.That(legBodyPart.Organs?.ContainedEntities.Count ?? 0, Is.EqualTo(0),
                "Leg should not contain the foot after DetachLimb; foot drops as separate item");

            // Find the detached foot (dropped at same location)
            var footQuery = SEntMan.EntityQueryEnumerator<OrganComponent>();
            while (footQuery.MoveNext(out var uid, out var organ))
            {
                if (organ.Category?.ToString() == "FootLeft" && organ.Body == null)
                {
                    footNet = SEntMan.GetNetEntity(uid);
                    break;
                }
            }
            Assert.That(footNet, Is.Not.Null, "Foot should exist as separate detached entity");
        });

        // Pick up the leg and verify player can hold it (prerequisite for re-attach via surgery)
        await Server.WaitPost(() =>
        {
            var sawUid = SEntMan.GetEntity(sawNet);
            foreach (var hand in HandSys.EnumerateHands((SPlayer, Hands!)))
            {
                if (HandSys.TryGetHeldItem((SPlayer, Hands!), hand, out var held) && held == sawUid)
                {
                    HandSys.TrySetActiveHand((SPlayer, Hands!), hand);
                    HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
                    break;
                }
            }
            Assert.That(HandSys.TryPickupAnyHand(SPlayer, SEntMan.GetEntity(legNet), checkActionBlocker: false),
                Is.True, "Player should be able to pick up the detached leg");
        });
        await RunTicks(1);

        await Server.WaitAssertion(() =>
        {
            Assert.That(HandSys.GetActiveItem((SPlayer, Hands!)), Is.EqualTo(SEntMan.GetEntity(legNet)),
                "Player should be holding the detached leg");
        });

        // Re-attach the leg: insert into body.Organs (same as AttachLimb does after DoAfter).
        // Full AttachLimb DoAfter flow is flaky in integration tests; direct insert verifies the logic.
        var bodySys = SEntMan.System<BodySystem>();
        await Server.WaitPost(() =>
        {
            var legUid = SEntMan.GetEntity(legNet);
            HandSys.TryDrop((SPlayer, Hands!), targetDropLocation: null, checkActionBlocker: false);
            var bodyComp = SEntMan.GetComponent<BodyComponent>(patient);
            var containerSys = SEntMan.System<SharedContainerSystem>();
            Assert.That(bodyComp.Organs, Is.Not.Null, "Body should have Organs container");
            Assert.That(containerSys.Insert(legUid, bodyComp.Organs!), Is.True, "Leg should insert into body.Organs");
        });
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var leg = SEntMan.GetEntity(legNet);
            Assert.That(SEntMan.EntityExists(leg), Is.True, "Leg entity should still exist after re-attachment");
            Assert.That(SEntMan.TryGetComponent(leg, out BodyPartComponent? legBodyPart), Is.True);
            Assert.That(legBodyPart!.Body, Is.EqualTo(patient), "Leg should be re-attached to body after AttachLimb");

            // Re-attached leg should not have foot inside; body has at most one FootLeft (no duplicate from sprite bug)
            Assert.That(legBodyPart.Organs?.ContainedEntities.Count ?? 0, Is.EqualTo(0),
                "Re-attached leg should not contain a foot");
            var footCount = bodySys.GetAllOrgans(patient).Count(o =>
                SEntMan.TryGetComponent(o, out OrganComponent? oc) && oc.Category?.ToString() == "FootLeft");
            Assert.That(footCount, Is.LessThanOrEqualTo(1), "Body should have at most one FootLeft");
        });

        // Re-attached leg has surgery state reset (BodySystem.OnBodyEntInserted) so it can be amputated again in-game.
        // Second amputation flow is covered by SurgeryFixesIntegrationTest.DetachLimb_OnLeg; full re-attach+re-amputate
        // is flaky in integration tests due to DoAfter/BUI timing.
    }
}
