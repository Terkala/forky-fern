// SPDX-FileCopyrightText: 2026 Fruitsalad <949631+Fruitsalad@users.noreply.github.com>
// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Client.HealthAnalyzer.UI;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Medical;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Events;
using Content.Shared.MedicalScanner;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Integration tests for surgery bug fixes:
/// - SawBones rejects scalpel (requires SawingTool, no improvised CuttingTool)
/// - DetachLimb detaches whole limb (arm with hand, leg with foot) not just hand/foot
/// </summary>
[TestFixture]
[TestOf(typeof(HealthAnalyzerSystem))]
public sealed class SurgeryFixesIntegrationTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    private static EntityUid GetBodyPart(IEntityManager entityManager, EntityUid body, string category)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>(category) };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        Assert.That(ev.Parts, Has.Count.GreaterThan(0), $"Body should have a {category}");
        return ev.Parts[0];
    }

    /// <summary>
    /// SawBones requires SawingTool. Scalpel has CuttingTool which was incorrectly allowed as improvised.
    /// With the fix, SawBones with only scalpel should be rejected with missing-tool.
    /// </summary>
    [Test]
    public async Task SawBones_WithScalpelOnly_Rejected()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false
        });
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var handsSystem = entityManager.System<SharedHandsSystem>();
        var mapData = await pair.CreateTestMap();

        await pair.RunTicksSync(5);

        EntityUid surgeon = default;
        EntityUid patient = default;
        EntityUid analyzer = default;
        EntityUid scalpel = default;
        EntityUid torso = default;

        await server.WaitPost(() =>
        {
            surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            analyzer = entityManager.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            scalpel = entityManager.SpawnEntity("Scalpel", mapData.GridCoords);
            torso = GetBodyPart(entityManager, patient, "Torso");

            handsSystem.TryPickupAnyHand(surgeon, analyzer, checkActionBlocker: false);
            handsSystem.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false);
        });

        await pair.RunTicksSync(5);

        // Open skin and tissue layers first (SawBones requires tissue retracted)
        await server.WaitPost(() =>
        {
            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, "RetractSkin", SurgeryLayer.Skin, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, "RetractSkin should succeed");
        });
        await pair.RunTicksSync(150);

        await server.WaitPost(() =>
        {
            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, "RetractTissue", SurgeryLayer.Tissue, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);
            Assert.That(ev.Valid, Is.True, "RetractTissue should succeed");
        });
        await pair.RunTicksSync(150);

        // Try SawBones with only scalpel - should be rejected (scalpel has CuttingTool, SawBones requires SawingTool)
        await server.WaitAssertion(() =>
        {
            var ev = new SurgeryRequestEvent(analyzer, surgeon, patient, torso, "SawBones", SurgeryLayer.Tissue, false);
            entityManager.EventBus.RaiseLocalEvent(patient, ref ev);

            Assert.That(ev.Valid, Is.False, "SawBones should be rejected when only holding scalpel");
            Assert.That(ev.RejectReason, Is.EqualTo("missing-tool"), "Reject reason should be missing-tool");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// When DetachLimb is performed on an arm, the whole arm (including hand) should be detached,
    /// not just the hand. Verifies the arm entity is detached and still contains its hand.
    /// </summary>
    [Test]
    public async Task DetachLimb_OnArm_DetachesWholeLimbWithHand()
    {
        await SpawnTarget("MobHuman");
        var patient = STarget!.Value;
        var patientNet = Target!.Value;

        var analyzerNet = NetEntity.Invalid;
        var scalpelNet = NetEntity.Invalid;
        var sawNet = NetEntity.Invalid;
        var armNet = NetEntity.Invalid;

        await Server.WaitPost(() =>
        {
            var analyzer = SEntMan.SpawnEntity("HandheldHealthAnalyzer", SEntMan.GetCoordinates(TargetCoords));
            var scalpel = SEntMan.SpawnEntity("Scalpel", SEntMan.GetCoordinates(TargetCoords));
            var saw = SEntMan.SpawnEntity("Saw", SEntMan.GetCoordinates(TargetCoords));
            var arm = GetBodyPart(SEntMan, patient, "ArmLeft");

            HandSys.TryPickupAnyHand(SPlayer, analyzer, checkActionBlocker: false);
            HandSys.TryPickupAnyHand(SPlayer, scalpel, checkActionBlocker: false);

            analyzerNet = SEntMan.GetNetEntity(analyzer);
            scalpelNet = SEntMan.GetNetEntity(scalpel);
            sawNet = SEntMan.GetNetEntity(saw);
            armNet = SEntMan.GetNetEntity(arm);
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

        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, armNet, "RetractSkin", SurgeryLayer.Skin, false), analyzerNet);
        await RunTicks(150);

        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, armNet, "RetractTissue", SurgeryLayer.Tissue, false), analyzerNet);
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

        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, armNet, "SawBones", SurgeryLayer.Tissue, false), analyzerNet);
        await RunTicks(150);

        await SendBui(HealthAnalyzerUiKey.Key, new SurgeryRequestBuiMessage(patientNet, armNet, "DetachLimb", SurgeryLayer.Organ, false), analyzerNet);
        await RunTicks(300);

        await Server.WaitAssertion(() =>
        {
            var arm = SEntMan.GetEntity(armNet);
            Assert.That(SEntMan.EntityExists(arm), Is.True, "Arm entity should exist after detachment");
            Assert.That(SEntMan.TryGetComponent(arm, out BodyPartComponent? bodyPart), Is.True);
            Assert.That(bodyPart!.Body, Is.Null, "Arm should no longer be attached to body after DetachLimb");

            // The whole arm (including hand) should have been detached - verify the arm still contains the hand
            Assert.That(bodyPart.Organs, Is.Not.Null, "Arm should have Organs container");
            Assert.That(bodyPart.Organs!.ContainedEntities.Count, Is.GreaterThan(0),
                "Arm should still contain its hand after detachment (whole limb detached, not just hand)");
        });
    }

    /// <summary>
    /// When DetachLimb is performed on a leg, the whole leg (including foot) should be detached,
    /// not just the foot. Verifies the leg entity is detached and still contains its foot.
    /// </summary>
    [Test]
    public async Task DetachLimb_OnLeg_DetachesWholeLimbWithFoot()
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
            var leg = GetBodyPart(SEntMan, patient, "LegLeft");

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

        await Server.WaitAssertion(() =>
        {
            var leg = SEntMan.GetEntity(legNet);
            Assert.That(SEntMan.EntityExists(leg), Is.True, "Leg entity should exist after detachment");
            Assert.That(SEntMan.TryGetComponent(leg, out BodyPartComponent? bodyPart), Is.True);
            Assert.That(bodyPart!.Body, Is.Null, "Leg should no longer be attached to body after DetachLimb");

            // The whole leg (including foot) should have been detached - verify the leg still contains the foot
            Assert.That(bodyPart.Organs, Is.Not.Null, "Leg should have Organs container");
            Assert.That(bodyPart.Organs!.ContainedEntities.Count, Is.GreaterThan(0),
                "Leg should still contain its foot after detachment (whole limb detached, not just foot)");
        });
    }

    /// <summary>
    /// SurgeryBodyPartDiagram region order: arms must come before hands, legs before feet,
    /// so clicking on an arm/leg selects the whole limb for DetachLimb, not just the hand/foot.
    /// </summary>
    [Test]
    public void SurgeryBodyPartDiagram_RegionOrder_ArmsBeforeHandsLegsBeforeFeet()
    {
        var order = SurgeryBodyPartDiagramControl.RegionCategoryOrder.ToList();

        var armRightIdx = order.IndexOf("ArmRight");
        var handRightIdx = order.IndexOf("HandRight");
        Assert.That(armRightIdx, Is.GreaterThanOrEqualTo(0), "ArmRight must exist in region order");
        Assert.That(armRightIdx, Is.LessThan(handRightIdx), "ArmRight must come before HandRight in region order");

        var armLeftIdx = order.IndexOf("ArmLeft");
        var handLeftIdx = order.IndexOf("HandLeft");
        Assert.That(armLeftIdx, Is.LessThan(handLeftIdx), "ArmLeft must come before HandLeft in region order");

        var legRightIdx = order.IndexOf("LegRight");
        var footRightIdx = order.IndexOf("FootRight");
        Assert.That(legRightIdx, Is.LessThan(footRightIdx), "LegRight must come before FootRight in region order");

        var legLeftIdx = order.IndexOf("LegLeft");
        var footLeftIdx = order.IndexOf("FootLeft");
        Assert.That(legLeftIdx, Is.LessThan(footLeftIdx), "LegLeft must come before FootLeft in region order");
    }
}
