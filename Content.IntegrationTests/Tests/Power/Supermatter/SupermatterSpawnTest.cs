using Content.IntegrationTests.Tests.Atmos;
using Content.Server.Power.Generation.Supermatter.Components;
using Content.Shared.Power.Generation.Supermatter.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Power.Supermatter;

[TestFixture]
[TestOf(typeof(SupermatterStateComponent))]
[TestOf(typeof(SupermatterProcessingComponent))]
public sealed class SupermatterSpawnTest : AtmosTest
{
    protected override ResPath? TestMapPath => new("Maps/Test/Atmospherics/tile_atmosphere_test_room.yml");

    [Test]
    public async Task SupermatterSpawnsWithCorrectInitialState()
    {
        EntityUid supermatter = default;

        await Server.WaitPost(() =>
        {
            supermatter = SEntMan.SpawnEntity("Supermatter", MapData.GridCoords);
        });

        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(supermatter), "Supermatter entity should exist");

            Assert.That(SEntMan.HasComponent<SupermatterStateComponent>(supermatter),
                "Supermatter should have SupermatterStateComponent");
            Assert.That(SEntMan.HasComponent<SupermatterProcessingComponent>(supermatter),
                "Supermatter should have SupermatterProcessingComponent");

            var state = SEntMan.GetComponent<SupermatterStateComponent>(supermatter);
            Assert.That(state.Integrity, Is.EqualTo(1000f), "Initial Integrity should be 1000");
            Assert.That(state.Power, Is.EqualTo(0f), "Initial Power should be 0");
            Assert.That(state.Stability, Is.EqualTo(10f), "Initial Stability should be 10");

            Assert.That(SEntMan.HasComponent<TransformComponent>(supermatter), "Supermatter should be in the world");
        });
    }
}
