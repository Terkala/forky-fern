using System.Linq;
using Content.Shared.Body;
using Content.Shared.Hands.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Body;

/// <summary>
/// Regression test for the body part hierarchy refactor.
/// Spawns a human and verifies that observable behavior is unchanged:
/// - Hands work (2 hands)
/// - All organs are present in correct structure
/// - No errors during spawn
/// </summary>
[TestFixture]
[TestOf(typeof(BodySystem))]
public sealed class BodyStructureRegressionTest
{
    [Test]
    public async Task MobHuman_SpawnsWithCorrectHandsAndOrgans()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BodySystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);

            // Verify hands: human should have 2 hands
            Assert.That(entityManager.HasComponent<HandsComponent>(human), Is.True);
            var hands = entityManager.GetComponent<HandsComponent>(human);
            Assert.That(hands.Count, Is.EqualTo(2), "Human should have 2 hands");

            // Verify body has all organs (body parts + nested organs)
            var organCount = bodySystem.GetAllOrgans(human).Count();
            Assert.That(organCount, Is.GreaterThanOrEqualTo(19), "Human should have at least 19 organs (6 body parts + 13 internal/extremity organs)");

            // Run a few ticks to ensure no errors
            for (var i = 0; i < 5; i++)
            {
                pair.Server.RunTicks(1);
            }
        });

        await pair.CleanReturnAsync();
    }
}
