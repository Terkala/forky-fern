using System.Numerics;
using Content.IntegrationTests.Tests.Movement;
using Content.Shared.Inventory;
using Robust.Shared.Maths;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Misc;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Weapons;

/// <summary>
/// Tests that the grappling hook rope bends around obstacles instead of phasing through them.
/// Simulates player in space with grappling gun and hardsuit: fire at distant wall, obstacle between,
/// move laterally and verify rope wraps around obstacle or player is constrained.
/// </summary>
[TestOf(typeof(GrapplingGunComponent))]
public sealed class GrapplingRopeWrapTest : MovementTest
{
    private readonly EntProtoId _grapplingGunProto = "WeaponGrapplingGun";
    private readonly EntProtoId _hardsuitProto = "ClothingOuterHardsuitSalvage";

    protected override int Tiles => 5;

    /// <summary>
    /// Tests that when an obstacle is between the player and the hooked wall, the rope path bends
    /// around it (RopePath has more than 2 waypoints) and the player can move laterally without
    /// phasing through the obstacle.
    /// </summary>
    [Test]
    public async Task GrapplingRopeWrapsAroundObstacleTest()
    {
        var pCoords = SEntMan.GetCoordinates(PlayerCoords);

        // Spawn obstacle north of the line of fire so the initial shot reaches the wall.
        // After moving north, the rope may pass by the obstacle; path computation should detect it.
        var obstacle = await Spawn("WallSolid", SEntMan.GetNetCoordinates(pCoords.Offset(new Vector2(2, 1))));
        await RunTicks(5);

        // Equip hardsuit so player survives in space (we add atmosphere for test reliability, but this matches user scenario).
        await EquipHardsuit();

        // Give grappling gun and fire at wall.
        var grapplingGun = await PlaceInHands(_grapplingGunProto);
        await Pair.RunSeconds(2f); // guns have a cooldown when picking them up

        Assert.That(WallRight, Is.Not.Null, "No wall to shoot at!");
        await AttemptShoot(WallRight);
        await Pair.RunSeconds(0.25f); // allow path computation (throttled at 0.1s)

        // Verify hook embedded.
        Assert.That(TryComp<GrapplingGunComponent>(grapplingGun, out var grapplingComp), "Grappling gun did not have GrapplingGunComponent.");
        Assert.That(grapplingComp.Projectile, Is.Not.Null, "Grappling gun projectile does not exist.");
        Assert.That(SEntMan.TryGetComponent<EmbeddableProjectileComponent>(grapplingComp.Projectile, out var embeddable), "Grappling hook was not embeddable.");
        Assert.That(embeddable.EmbeddedIntoUid, Is.EqualTo(ToServer(WallRight)), "Grappling hook was not embedded into the wall.");

        var grapplingSystem = SEntMan.System<SharedGrapplingGunSystem>();
        Assert.That(grapplingSystem.IsEntityHooked(SPlayer), "Player is not hooked to the wall.");

        // Move laterally (north). The obstacle is between gun and hook; rope should bend around it and anchor.
        var posBefore = Transform.GetWorldPosition(SPlayer);
        await Move(DirectionFlag.North, 0.5f);
        await Pair.RunSeconds(0.25f); // allow path recomputation (throttled at 0.1s) and anchor logic

        var posAfter = Transform.GetWorldPosition(SPlayer);

        // Player should still be hooked (rope did not snap from phasing through obstacle).
        Assert.That(grapplingSystem.IsEntityHooked(SPlayer), "Player became unhooked after moving laterally.");
        Assert.That(TryComp<GrapplingGunComponent>(grapplingGun, out grapplingComp), "Grappling gun component lost.");
        Assert.That(grapplingComp.Projectile, Is.Not.Null, "Grappling gun lost projectile.");

        // Rope should have bent around the obstacle (path has corner waypoints).
        Assert.That(grapplingComp.RopePath.Count, Is.GreaterThan(2),
            "Rope path should have more than 2 waypoints when wrapping around obstacle (gun, corner, hook).");

        // Anchor angle should be set when rope is bent, so un-anchor uses angle-based logic.
        Assert.That(grapplingComp.AnchorAngle, Is.Not.Null,
            "Anchor angle should be set when rope bends around obstacle.");

        // Player should have moved (we pressed north).
        Assert.That((posAfter - posBefore).LengthSquared(), Is.GreaterThan(0.01f), "Player did not move when pressing north.");
    }

    private async Task EquipHardsuit()
    {
        var hardsuit = await Spawn(_hardsuitProto, SEntMan.GetNetCoordinates(SEntMan.GetCoordinates(PlayerCoords)));
        var invSystem = SEntMan.System<InventorySystem>();

        await Server.WaitPost(() =>
        {
            Assert.That(invSystem.TryEquip(SEntMan.GetEntity(Player), SEntMan.GetEntity(hardsuit), "outerClothing", force: true),
                "Failed to equip hardsuit.");
        });
        await RunTicks(1);
    }
}
