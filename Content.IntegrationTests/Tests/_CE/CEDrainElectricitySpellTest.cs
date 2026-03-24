using Content.IntegrationTests.Pair;
using Content.Shared.Actions.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._CE;

/// <summary>
/// Regression: Drain Electricity CE action loads and spawns (YAML + CESpellEmpPulse wiring).
/// </summary>
[TestFixture]
public sealed class CEDrainElectricitySpellTest
{
    private const string SpellProto = "CEActionSpellElementalDrainElectricity";

    [Test]
    public async Task DrainElectricityPrototypeSpawns()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();
        var map = await pair.CreateTestMap();
        var ent = server.EntMan;

        await server.WaitPost(() =>
        {
            var spell = ent.SpawnEntity(SpellProto, map.MapCoords);
            Assert.That(ent.EntityExists(spell), Is.True);
            Assert.That(ent.HasComponent<ActionComponent>(spell), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
