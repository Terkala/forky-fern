using Content.IntegrationTests.Pair;
using Content.Shared._Funkystation.Projectile;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Funkystation;

[TestFixture]
public sealed class BouncingBananaSpellTest
{
    private const string ActionProto = "CEActionSpellHonkBouncingBanana";
    private const string ProjectileProto = "CEProjectileHonkBouncingBanana";

    [Test]
    public async Task BouncingBananaPrototypesResolve()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitIdleAsync();
        var proto = pair.Server.ResolveDependency<IPrototypeManager>();

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(proto.HasIndex<EntityPrototype>(ActionProto), Is.True);
            Assert.That(proto.HasIndex<EntityPrototype>(ProjectileProto), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BouncingSpellProjectile_CanStillBounce_RespectsCountAndTime()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();
        var map = await pair.CreateTestMap();

        var bouncingSys = server.EntMan.System<SharedBouncingSpellProjectileSystem>();
        var timing = server.ResolveDependency<IGameTiming>();

        await server.WaitPost(() =>
        {
            var proj = server.EntMan.SpawnEntity(ProjectileProto, map.MapCoords);
            var bounce = server.EntMan.AddComponent<BouncingSpellProjectileComponent>(proj);
            bounce.MaxBounces = 3;
            bounce.BounceCount = 0;
            bounce.ExpireAt = timing.CurTime + TimeSpan.FromSeconds(30);
            Assert.That(bouncingSys.CanStillBounce(bounce), Is.True);

            bounce.BounceCount = 3;
            Assert.That(bouncingSys.CanStillBounce(bounce), Is.False);

            bounce.BounceCount = 0;
            bounce.ExpireAt = timing.CurTime - TimeSpan.FromSeconds(1);
            Assert.That(bouncingSys.CanStillBounce(bounce), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
