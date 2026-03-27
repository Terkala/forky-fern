using Content.IntegrationTests.Pair;
using Content.Server._CE.Skill;
using Content.Shared._CE.MageAscension;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared._CE.Skill.Components;
using Content.Shared._CE.Skill.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared._Funkystation.MageAscension;
using Content.Shared.Roles.Components;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Funkystation;

[TestFixture]
public sealed class MageGrimoireSkillTest
{
    private const string MageSkillPoint = "MageGrimoire";
    private const string WizardFireballSkill = "ForkyWizardSpellFireball";

    [Test]
    public async Task ElementalCommitAndSpellRespectSkillPoints()
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
            var storage = ent.GetComponent<CESkillStorageComponent>(mage);

            skills.AddSkillTree(mage, "MageElementalism");
            Assert.That(skills.TryAddSkillPoints((mage, storage), MageSkillPoint, 3), Is.True);

            Assert.That(skills.TryLearnSkill(mage, "MagePathElementalCommit"), Is.True);
            Assert.That(skills.TryLearnSkill(mage, "MageSpellElementalSummonRock"), Is.True);
            Assert.That(skills.TryLearnSkill(mage, "MageSpellElementalIceShield"), Is.True);
            Assert.That(storage.SkillPoints[MageSkillPoint].Sum, Is.EqualTo(FixedPoint2.New(3)));
            Assert.That(skills.CanLearnSkill(mage, "MageSpellElementalEarthenBarricade"), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ElementalismSpentPointsUnlockHigherTiers()
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
            var storage = ent.GetComponent<CESkillStorageComponent>(mage);

            skills.AddSkillTree(mage, "MageElementalism");
            Assert.That(skills.TryAddSkillPoints((mage, storage), MageSkillPoint, 10), Is.True);

            Assert.That(skills.TryLearnSkill(mage, "MagePathElementalCommit"), Is.True);
            Assert.That(skills.TryLearnSkill(mage, "MageSpellElementalSummonRock"), Is.True);
            Assert.That(skills.TryLearnSkill(mage, "MageSpellElementalIceShield"), Is.True);
            Assert.That(storage.SkillPoints[MageSkillPoint].Sum, Is.EqualTo(FixedPoint2.New(3)));

            Assert.That(skills.CanLearnSkill(mage, "MageSpellElementalDrainElectricity"), Is.False);
            Assert.That(skills.TryLearnSkill(mage, "MageSpellElementalEarthenBarricade"), Is.True);
            Assert.That(storage.SkillPoints[MageSkillPoint].Sum, Is.EqualTo(FixedPoint2.New(4)));

            Assert.That(skills.CanLearnSkill(mage, "MageSpellElementalDrainElectricity"), Is.True);
            Assert.That(skills.CanLearnSkill(mage, "MageSpellElementalFireBarrier"), Is.False);

            Assert.That(skills.TryLearnSkill(mage, "MageSpellElementalDrainElectricity"), Is.True);
            Assert.That(storage.SkillPoints[MageSkillPoint].Sum, Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(skills.CanLearnSkill(mage, "MageSpellElementalFireBarrier"), Is.True);
            Assert.That(skills.CanLearnSkill(mage, "MageSpellElementalFireball"), Is.False);

            Assert.That(skills.TryLearnSkill(mage, "MageSpellElementalFireBarrier"), Is.True);
            Assert.That(storage.SkillPoints[MageSkillPoint].Sum, Is.EqualTo(FixedPoint2.New(6)));
            Assert.That(skills.CanLearnSkill(mage, "MageSpellElementalFireball"), Is.True);

            Assert.That(ent.GetComponent<MageElementalismProgressComponent>(mage).ElementalismDepth, Is.EqualTo(3));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ElementalismSkillCapShrinkRespectsFrontier()
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
            var storage = ent.GetComponent<CESkillStorageComponent>(mage);

            skills.AddSkillTree(mage, "MageElementalism");
            // Budget exactly 4 points: foundation + one tier-2; lowering max forces Sum > Max.
            Assert.That(skills.TryAddSkillPoints((mage, storage), MageSkillPoint, 4), Is.True);

            Assert.That(skills.TryLearnSkill(mage, "MagePathElementalCommit"), Is.True);
            Assert.That(skills.TryLearnSkill(mage, "MageSpellElementalSummonRock"), Is.True);
            Assert.That(skills.TryLearnSkill(mage, "MageSpellElementalIceShield"), Is.True);
            Assert.That(skills.TryLearnSkill(mage, "MageSpellElementalEarthenBarricade"), Is.True);

            var frontierBefore = skills.GetFrontierSkills((mage, storage));
            Assert.That(frontierBefore.Contains("MageSpellElementalEarthenBarricade"), Is.True);

            skills.TryRemoveSkillPoints((mage, storage), MageSkillPoint, 1, silent: true);
            Assert.That(storage.SkillPoints[MageSkillPoint].Sum, Is.EqualTo(FixedPoint2.New(3)));
            Assert.That(storage.SkillPoints[MageSkillPoint].Max, Is.EqualTo(FixedPoint2.New(3)));
            var lostIce = !skills.HaveSkill(mage, "MageSpellElementalIceShield");
            var lostEarthen = !skills.HaveSkill(mage, "MageSpellElementalEarthenBarricade");
            // Spent-only tier gates make both Ice and Earthen frontier-removable; cap shrink removes one at random.
            Assert.That(lostIce ^ lostEarthen, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ConfluenceOpenedIncreasesMagePointCap()
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
            var storage = ent.GetComponent<CESkillStorageComponent>(mage);

            Assert.That(skills.TryAddSkillPoints((mage, storage), MageSkillPoint, 3), Is.True);

            var ev = new MageConfluenceOpenedEvent();
            ent.EventBus.RaiseLocalEvent(mage, ref ev);

            Assert.That(storage.SkillPoints[MageSkillPoint].Max, Is.EqualTo(FixedPoint2.New(4)));
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

    [Test]
    public async Task ElfPathCommitsAreMutuallyExclusive()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();
        var map = await pair.CreateTestMap();
        var ent = server.EntMan;
        var skills = ent.System<CESkillSystem>();

        await server.WaitPost(() =>
        {
            var elf = ent.SpawnEntity("InteractionTestMob", map.MapCoords);
            ent.EnsureComponent<CESkillStorageComponent>(elf);
            var storage = ent.GetComponent<CESkillStorageComponent>(elf);

            skills.AddSkillTree(elf, "ForkyElfTreeCantrip");
            skills.AddSkillTree(elf, "ForkyElfTreeVitality");
            skills.AddSkillTree(elf, "ForkyElfTreeTide");
            Assert.That(skills.TryAddSkillPoints((elf, storage), "ElfMagic", 3), Is.True);

            Assert.That(skills.TryLearnSkill(elf, "ForkyElfCommitCantrip"), Is.True);
            Assert.That(skills.CanLearnSkill(elf, "ForkyElfCommitVitality"), Is.False);
            Assert.That(skills.TryLearnSkill(elf, "SphereOfLight"), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WizardSpellSkillPrototypeExists()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();
        var proto = server.ResolveDependency<IPrototypeManager>();

        await server.WaitPost(() =>
        {
            Assert.That(proto.TryIndex(WizardFireballSkill, out CESkillPrototype _), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MageGrimoireSkillTreeLocksToFirstUser()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();
        var map = await pair.CreateTestMap();
        var ent = server.EntMan;

        await server.WaitPost(() =>
        {
            var grim = ent.SpawnEntity("MageGrimoire", map.MapCoords);
            var mageA = ent.SpawnEntity("InteractionTestMob", map.MapCoords);
            var mageB = ent.SpawnEntity("InteractionTestMob", map.MapCoords);
            ent.AddComponent<MageOfAscensionComponent>(mageA);
            ent.AddComponent<MageOfAscensionComponent>(mageB);

            var openA = new ActivatableUIOpenAttemptEvent(mageA, silent: false);
            ent.EventBus.RaiseLocalEvent(grim, openA);
            Assert.That(openA.Cancelled, Is.False);

            ent.EventBus.RaiseLocalEvent(grim, new AfterActivatableUIOpenEvent(mageA));
            Assert.That(ent.GetComponent<MageGrimoireComponent>(grim).SkillTreeBoundOwner,
                Is.EqualTo(ent.GetNetEntity(mageA)));

            var openB = new ActivatableUIOpenAttemptEvent(mageB, silent: false);
            ent.EventBus.RaiseLocalEvent(grim, openB);
            Assert.That(openB.Cancelled, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonMageCannotOpenMageGrimoireSkillTree()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();
        var map = await pair.CreateTestMap();
        var ent = server.EntMan;

        await server.WaitPost(() =>
        {
            var grim = ent.SpawnEntity("MageGrimoire", map.MapCoords);
            var mundane = ent.SpawnEntity("InteractionTestMob", map.MapCoords);

            var attempt = new ActivatableUIOpenAttemptEvent(mundane, silent: false);
            ent.EventBus.RaiseLocalEvent(grim, attempt);
            Assert.That(attempt.Cancelled, Is.True);
            Assert.That(ent.GetComponent<MageGrimoireComponent>(grim).SkillTreeBoundOwner, Is.Null);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WizardGrimoireSkillTreeLocksToFirstUser()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();
        var map = await pair.CreateTestMap();
        var ent = server.EntMan;

        await server.WaitPost(() =>
        {
            var grim = ent.SpawnEntity("WizardsGrimoire", map.MapCoords);
            var wizA = ent.SpawnEntity("InteractionTestMob", map.MapCoords);
            var wizB = ent.SpawnEntity("InteractionTestMob", map.MapCoords);
            ent.AddComponent<WizardRoleComponent>(wizA);
            ent.AddComponent<WizardRoleComponent>(wizB);

            var openA = new ActivatableUIOpenAttemptEvent(wizA, silent: false);
            ent.EventBus.RaiseLocalEvent(grim, openA);
            Assert.That(openA.Cancelled, Is.False);

            ent.EventBus.RaiseLocalEvent(grim, new AfterActivatableUIOpenEvent(wizA));
            Assert.That(ent.GetComponent<WizardSkillGrimoireComponent>(grim).SkillTreeBoundOwner,
                Is.EqualTo(ent.GetNetEntity(wizA)));

            var openB = new ActivatableUIOpenAttemptEvent(wizB, silent: false);
            ent.EventBus.RaiseLocalEvent(grim, openB);
            Assert.That(openB.Cancelled, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonWizardCannotOpenWizardGrimoireSkillTree()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();
        var map = await pair.CreateTestMap();
        var ent = server.EntMan;

        await server.WaitPost(() =>
        {
            var grim = ent.SpawnEntity("WizardsGrimoire", map.MapCoords);
            var mundane = ent.SpawnEntity("InteractionTestMob", map.MapCoords);

            var attempt = new ActivatableUIOpenAttemptEvent(mundane, silent: false);
            ent.EventBus.RaiseLocalEvent(grim, attempt);
            Assert.That(attempt.Cancelled, Is.True);
            Assert.That(ent.GetComponent<WizardSkillGrimoireComponent>(grim).SkillTreeBoundOwner, Is.Null);
        });

        await pair.CleanReturnAsync();
    }
}
