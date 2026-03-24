using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Power.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.Power.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._CE;

/// <summary>
/// Verifies CE spell actions drain the performer's magic battery after a cast completes.
/// </summary>
[TestFixture]
public sealed class CEMagicManaSpellTest : InteractionTest
{
    private static readonly EntProtoId WaterSpell = "CEActionSpellWaterCreation";

    [Test]
    public async Task WaterCreationSpellConsumesMana()
    {
        var actionsSystem = SEntMan.System<SharedActionsSystem>();
        var batterySys = SEntMan.System<BatterySystem>();
        var playerUid = SEntMan.GetEntity(Player);

        var chargeBefore = 0f;

        await Server.WaitPost(() =>
        {
            var battery = SEntMan.AddComponent<BatteryComponent>(playerUid);
            batterySys.SetMaxCharge((playerUid, battery), 100);
            batterySys.SetCharge((playerUid, battery), 100);
            chargeBefore = batterySys.GetCharge((playerUid, battery));

            var actionId = actionsSystem.AddAction(playerUid, WaterSpell);
            Assert.That(actionId, Is.Not.Null, "AddAction should spawn CEActionSpellWaterCreation.");

            var actionEnt = actionsSystem.GetAction(actionId);
            Assert.That(actionEnt, Is.Not.Null);

            actionsSystem.PerformAction(playerUid, actionEnt!.Value);
        });

        await Pair.RunTicksSync(60);

        await Server.WaitAssertion(() =>
        {
            var battery = SEntMan.GetComponent<BatteryComponent>(playerUid);
            var after = batterySys.GetCharge((playerUid, battery));
            Assert.That(after, Is.EqualTo(chargeBefore - 10f).Within(0.05f));
        });
    }
}
