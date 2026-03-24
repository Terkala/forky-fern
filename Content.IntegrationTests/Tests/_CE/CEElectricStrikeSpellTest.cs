using Content.IntegrationTests.Pair;
using Content.Shared.Actions.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._CE;

/// <summary>
/// Regression: Electric Strike CE action prototype loads and spawns (YAML + Electrocute entity effect wiring).
/// </summary>
[TestFixture]
public sealed class CEElectricStrikeSpellTest
{
    private const string SpellProto = "CEActionSpellElementalElectricStrike";

    [Test]
    public async Task ElectricStrikePrototypeSpawns()
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
