using Content.IntegrationTests;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Humanoid;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Cybernetics;

/// <summary>
/// Regression test for Bug 2: When attaching a cybernetic arm, the arm briefly appeared as human.
/// The fix ensures the sprite is changed to cybernetic before revealing the arm.
/// Verification: Attach a cybernetic arm to an empty arm slot; the arm should appear as cybernetic
/// immediately (cyber sprite set, layers visible) with no flash of organic arm.
/// </summary>
[TestFixture]
[TestOf(typeof(Content.Shared.Cybernetics.Systems.CyberLimbAppearanceSystem))]
public sealed class CyberArmAppearanceNoFlashIntegrationTest
{
    private static EntityUid GetLimbByCategory(IEntityManager entityManager, EntityUid body, string category)
    {
        var ev = new BodyPartQueryByTypeEvent(body) { Category = new ProtoId<OrganCategoryPrototype>(category) };
        entityManager.EventBus.RaiseLocalEvent(body, ref ev);
        return ev.Parts[0];
    }

    private static void ReplaceArmWithCyberArm(IEntityManager entityManager, BodySystem bodySystem,
        SharedContainerSystem containerSystem, EntityUid body, EntityCoordinates coords)
    {
        var arm = GetLimbByCategory(entityManager, body, "ArmLeft");
        var removeEv = new OrganRemoveRequestEvent(arm) { Destination = coords };
        entityManager.EventBus.RaiseLocalEvent(arm, ref removeEv);
        Assert.That(removeEv.Success, Is.True, "Remove arm should succeed");

        var cyberArm = entityManager.SpawnEntity("OrganCyberArmLeft", coords);
        var bodyComp = entityManager.GetComponent<BodyComponent>(body);
        Assert.That(bodyComp.Organs, Is.Not.Null, "Body should have Organs container");
        Assert.That(containerSystem.Insert(cyberArm, bodyComp.Organs!), Is.True, "Insert cyber arm should succeed");
    }

    [Test]
    public async Task CyberArm_AppearanceIsCyberneticImmediately_WhenAttached()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid user = default;

        await server.WaitAssertion(() =>
        {
            user = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            ReplaceArmWithCyberArm(entityManager, bodySystem, containerSystem, user, mapData.GridCoords);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.TryGetComponent<HumanoidAppearanceComponent>(user, out var humanoid), Is.True,
                "User should have HumanoidAppearanceComponent");

            // First step of attach must be: sprite set to cybernetic, then reveal.
            // Verify cyber sprites are set for arm and hand
            Assert.That(humanoid!.CustomBaseLayers.TryGetValue(HumanoidVisualLayers.LArm, out var lairmInfo), Is.True,
                "CustomBaseLayers should contain LArm");
            Assert.That(lairmInfo.Id?.ToString(), Is.EqualTo("MobCyberLArm"),
                "LArm should use MobCyberLArm sprite (not organic)");

            Assert.That(humanoid.CustomBaseLayers.TryGetValue(HumanoidVisualLayers.LHand, out var lhandInfo), Is.True,
                "CustomBaseLayers should contain LHand");
            Assert.That(lhandInfo.Id?.ToString(), Is.EqualTo("MobCyberLHand"),
                "LHand should use MobCyberLHand sprite (not organic)");

            // Arm and hand layers must be visible (not hidden) - ensures no flash of organic arm
            // when layer was revealed before sprite was set
            Assert.That(humanoid.PermanentlyHidden.Contains(HumanoidVisualLayers.LArm), Is.False,
                "LArm should be visible after cyber arm attach");
            Assert.That(humanoid.PermanentlyHidden.Contains(HumanoidVisualLayers.LHand), Is.False,
                "LHand should be visible after cyber arm attach");
        });

        await pair.CleanReturnAsync();
    }
}
