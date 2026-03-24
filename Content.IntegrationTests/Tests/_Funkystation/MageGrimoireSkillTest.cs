using Content.IntegrationTests.Pair;
using Content.Server._CE.Skill;
using Content.Shared._CE.MageAscension;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared._CE.Skill.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Funkystation;

[TestFixture]
public sealed class MageGrimoireSkillTest
{
    [Test]
    public async Task ElementalismPackageGrantsLearnedSkill()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();
        var map = await pair.CreateTestMap();

        var ent = server.EntMan;
        var skills = ent.System<CESkillSystem>();

        await server.WaitPost(() =>
        {
            var mage = ent.SpawnEntity("InteractionTestMob", map.MapCoords);
            ent.AddComponent<MageOfAscensionComponent>(mage);
            ent.EnsureComponent<CESkillStorageComponent>(mage);

            Assert.That(skills.TryAddSkill(mage, "MagePathElementalismSpells", null, free: true), Is.True);
            Assert.That(skills.HaveSkill(mage, "MagePathElementalismSpells"), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ConfluenceOpenedEnqueuesTierSpellPick()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();
        var map = await pair.CreateTestMap();

        var ent = server.EntMan;

        await server.WaitPost(() =>
        {
            var mage = ent.SpawnEntity("InteractionTestMob", map.MapCoords);
            var mageComp = ent.AddComponent<MageOfAscensionComponent>(mage);
            mageComp.ConfluencesOpened = 1;

            var ev = new MageConfluenceOpenedEvent();
            ent.EventBus.RaiseLocalEvent(mage, ref ev);

            Assert.That(mageComp.PendingSpellPickTiers, Is.EqualTo(new[] { 2 }).AsCollection);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ElementalismSpellProtosSpawn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();
        var map = await pair.CreateTestMap();
        var ent = server.EntMan;
        var proto = server.ResolveDependency<IPrototypeManager>();

        static void AssertSpawn(IEntityManager ent, IPrototypeManager proto, MapCoordinates coords, string id)
        {
            Assert.That(proto.HasIndex<EntityPrototype>(id), Is.True, $"Missing prototype {id}");
            var uid = ent.SpawnEntity(id, coords);
            Assert.That(ent.EntityExists(uid), Is.True);
            ent.DeleteEntity(uid);
        }

        await server.WaitPost(() =>
        {
            var coords = map.MapCoords;
            AssertSpawn(ent, proto, coords, "CEActionSpellElementalSummonRock");
            AssertSpawn(ent, proto, coords, "CEActionSpellElementalIceShield");
            AssertSpawn(ent, proto, coords, "CEActionSpellElementalEarthenBarricade");
            AssertSpawn(ent, proto, coords, "CEActionSpellElementalFireBarrier");
            AssertSpawn(ent, proto, coords, "CEActionSpellElementalFireball");
            AssertSpawn(ent, proto, coords, "CEActionSpellElementalEarthquake");
        });

        await pair.CleanReturnAsync();
    }
}
