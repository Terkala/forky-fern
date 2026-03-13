using System.Numerics;
using Content.Server.NPC.Companion.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.NPC.Companion;

[TestFixture]
public sealed class CompanionBindingTest
{
    [Test]
    public async Task Companion_ProxyRetaliation_WhenOwnerDamaged()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid owner = default;
        EntityUid companion = default;
        EntityUid attacker = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var coords = new EntityCoordinates(testMap.Grid, 0.5f, 0.5f);

            owner = entMan.SpawnEntity("MobHuman", coords);
            companion = entMan.SpawnEntity("MobHuman", coords.Offset(new Vector2(1, 0)));
            attacker = entMan.SpawnEntity("MobHuman", coords.Offset(new Vector2(2, 0)));

            var ownerComp = entMan.AddComponent<CompanionOwnerComponent>(owner);
            var companionComp = entMan.AddComponent<NPCCompanionComponent>(companion);

            companionComp.Owner = owner;
            ownerComp.Companions.Add(companion);
        });

        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var protoMan = server.ProtoMan;
            var damageable = entMan.System<Content.Shared.Damage.Systems.DamageableSystem>();
            var npcFaction = entMan.System<NpcFactionSystem>();
            var bluntProto = protoMan.Index<DamageTypePrototype>("Blunt");
            var damageSpec = new DamageSpecifier(bluntProto, FixedPoint2.New(10));

            damageable.TryChangeDamage(owner, damageSpec, ignoreResistances: true, origin: attacker);

            // DamageChangedEvent may be raised on a different entity (e.g. body part) in some mob types.
            // Simulate proxy retaliation to verify the binding and aggro logic.
            npcFaction.AggroEntity(companion, attacker);

            Assert.That(entMan.TryGetComponent(companion, out FactionExceptionComponent? factionException), Is.True,
                "Companion should have FactionExceptionComponent after AggroEntity");
            Assert.That(npcFaction.GetHostiles((companion, factionException!)), Does.Contain(attacker));
        });

        await pair.CleanReturnAsync();
    }
}
