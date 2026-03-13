using System.Linq;
using Content.IntegrationTests.Tests.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Power.Generation.Supermatter;
using Content.Shared.Atmos.Components;
using Content.Server.Power.Generation.Supermatter.Components;
using Content.Shared.Atmos;
using Content.Shared.Power.Generation.Supermatter.Components;
using Content.Shared.Singularity.Components;
using Content.Shared.Tests;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Power.Supermatter;

/// <summary>
/// Integration tests for every visual state and delamination outcome the supermatter can enter.
/// Each test spawns the SM in a 3x3 room filled with gas that drives it toward that state, then runs until it reaches it.
/// </summary>
[TestFixture]
[TestOf(typeof(SupermatterSystem))]
[TestOf(typeof(SupermatterStateComponent))]
public sealed class SupermatterStateTest : AtmosTest
{
    protected override ResPath? TestMapPath => new("Maps/Test/Atmospherics/tile_atmosphere_test_room.yml");

    [SetUp]
    public override async Task Setup()
    {
        await base.Setup();
        await Server.WaitPost(() =>
        {
            MapSystem.SetPaused(MapData.MapId, false);
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);
        });
        await RunTicks(5);
    }

    /// <summary>
    /// Spawns supermatter and adds gas directly to the center tile (matching SupermatterCharacteristicsTest pattern).
    /// The supermatter absorbs from center + 4 orthogonals; center gets gas immediately.
    /// </summary>
    private async Task<EntityUid> SpawnSupermatterAndFillGas(Gas gas, float moles, float? temperature = null)
    {
        return await SpawnSupermatterAndFillGasMixed([(gas, moles)], temperature);
    }

    /// <summary>
    /// Spawns supermatter and adds gas to center tile. For mixed gases, adds each to center.
    /// </summary>
    private async Task<EntityUid> SpawnSupermatterAndFillGasMixed((Gas gas, float moles)[] gases, float? temperature = null)
    {
        EntityUid supermatter = default;
        await Server.WaitPost(() =>
        {
            var markers = SEntMan.AllEntities<TestMarkerComponent>().ToArray();
            Assert.That(GetMarker(markers, "floor", out var floorUid));
            var floorCoords = SEntMan.GetComponent<TransformComponent>(floorUid).Coordinates;
            supermatter = SEntMan.SpawnEntity("Supermatter", floorCoords);
        });
        await RunTicks(20);
        await Server.WaitPost(() =>
        {
            var centerPos = Transform.GetGridTilePositionOrDefault(supermatter);
            var gridAtmos = SEntMan.GetComponent<GridAtmosphereComponent>(MapData.Grid);
            var centerMix = SAtmos.GetTileMixture((MapData.Grid, gridAtmos), null, centerPos, true);
            Assert.That(centerMix, Is.Not.Null.And.Not.Property("Immutable").True,
                "Center tile must have mutable gas mixture for supermatter absorption");
            foreach (var (gas, moles) in gases)
            {
                centerMix!.AdjustMoles(gas, moles);
            }
            if (temperature.HasValue)
                centerMix!.Temperature = temperature.Value;
        });
        await RunTicks(5);
        return supermatter;
    }

    /// <summary>
    /// Visual state 0: Integrity >= 750 (healthy). N2O maintains high stability and integrity.
    /// </summary>
    [Test]
    public async Task NitrousOxideKeepsSupermatterInHealthyState()
    {
        var supermatter = await SpawnSupermatterAndFillGas(Gas.NitrousOxide, 1000f);

        await RunTicks(200);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(supermatter), "Supermatter should still exist");
            var state = SEntMan.GetComponent<SupermatterStateComponent>(supermatter);
            Assert.That(state.Integrity, Is.GreaterThanOrEqualTo(750f),
                "N2O should keep supermatter in healthy state (Integrity >= 750)");
        });
    }

    /// <summary>
    /// Visual state 1: Integrity >= 250 and < 750 (damaged). BZ has strong Stability -0.8/100 mol.
    /// Need ~13k mol to overcome default Stability 10 (9% absorption from center tile).
    /// </summary>
    [Test]
    public async Task PlasmaInHotRoomDamagesSupermatterToState1()
    {
        var supermatter = await SpawnSupermatterAndFillGas(Gas.Plasma, 50000f, temperature: 1500f);

        await RunTicks(6000);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(supermatter), "Supermatter should still exist");
            var state = SEntMan.GetComponent<SupermatterStateComponent>(supermatter);
            Assert.That(state.Integrity, Is.GreaterThanOrEqualTo(250f).And.LessThan(750f),
                "Plasma in hot room should damage supermatter to state 1 (250 <= Integrity < 750)");
        });
    }

    /// <summary>
    /// Visual state 2: Integrity >= 50 and < 250 (critically damaged). BZ with more moles and heat.
    /// </summary>
    [Test]
    public async Task PlasmaInVeryHotRoomDamagesSupermatterToState2()
    {
        var supermatter = await SpawnSupermatterAndFillGas(Gas.Plasma, 60000f, temperature: 1800f);

        await RunTicks(7500);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(supermatter), "Supermatter should still exist");
            var state = SEntMan.GetComponent<SupermatterStateComponent>(supermatter);
            Assert.That(state.Integrity, Is.GreaterThanOrEqualTo(50f).And.LessThan(250f),
                "Plasma in very hot room should damage supermatter to state 2 (50 <= Integrity < 250)");
        });
    }

    /// <summary>
    /// Visual state 3: Integrity < 50 (about to delaminate). BZ with heavy damage.
    /// </summary>
    [Test]
    public async Task PlasmaInExtremeRoomDamagesSupermatterToState3()
    {
        var supermatter = await SpawnSupermatterAndFillGas(Gas.Plasma, 80000f, temperature: 2000f);

        await RunTicks(8000);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(supermatter), "Supermatter should still exist");
            var state = SEntMan.GetComponent<SupermatterStateComponent>(supermatter);
            Assert.That(state.Integrity, Is.GreaterThanOrEqualTo(0f).And.LessThan(50f),
                "Plasma in extreme room should damage supermatter to state 3 (0 <= Integrity < 50)");
        });
    }

    /// <summary>
    /// Delamination: Singularity. CO2 (high Growth) dominant when integrity hits 0.
    /// CO2: Stability -0.3/100, Growth 1/100.
    /// </summary>
    [Test]
    public async Task CarbonDioxideDominantCausesSingularityDelamination()
    {
        var supermatter = await SpawnSupermatterAndFillGas(Gas.CarbonDioxide, 80000f, temperature: 2000f);

        await RunTicks(8000);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(supermatter), "Supermatter should have delaminated");
            var singularityCount = SEntMan.EntityQuery<SingularityComponent>().Count();
            Assert.That(singularityCount, Is.GreaterThan(0),
                "CO2-dominant delamination should spawn a Singularity");
        });
    }

    /// <summary>
    /// Delamination: Tesla. Water vapor (high Conductivity) dominant when integrity hits 0.
    /// BZ damages; WaterVapor provides Conductivity dominance.
    /// </summary>
    [Test]
    public async Task WaterVaporDominantCausesTeslaDelamination()
    {
        var supermatter = await SpawnSupermatterAndFillGasMixed(
            [(Gas.BZ, 60000f), (Gas.WaterVapor, 60000f)],
            temperature: 2000f);

        await RunTicks(8000);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(supermatter), "Supermatter should have delaminated");
            var teslaCount = SEntMan.EntityQuery<MetaDataComponent>()
                .Count(m => m.EntityPrototype?.ID == "TeslaMiniEnergyBall");
            Assert.That(teslaCount, Is.GreaterThan(0),
                "Water vapor (Conductivity)-dominant delamination should spawn Tesla balls");
        });
    }

    /// <summary>
    /// Delamination: Explosion. Plasma (Enthalpy+) dominant when integrity hits 0.
    /// BZ damages; Plasma provides Enthalpy dominance.
    /// </summary>
    [Test]
    public async Task PlasmaDominantCausesExplosionDelamination()
    {
        var supermatter = await SpawnSupermatterAndFillGasMixed(
            [(Gas.BZ, 60000f), (Gas.Plasma, 40000f)],
            temperature: 2000f);

        await RunTicks(8000);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(supermatter), "Supermatter should have delaminated");
            var singularityCount = SEntMan.EntityQuery<SingularityComponent>().Count();
            var teslaCount = SEntMan.EntityQuery<MetaDataComponent>()
                .Count(m => m.EntityPrototype?.ID == "TeslaMiniEnergyBall");
            Assert.That(singularityCount, Is.EqualTo(0), "Explosion delamination should not spawn Singularity");
            Assert.That(teslaCount, Is.EqualTo(0), "Explosion delamination should not spawn Tesla balls");
        });
    }

    /// <summary>
    /// Delamination: Resonance Cascade. Nitrous oxide (Stability+) with maxAbs near zero causes Resonance.
    /// N2O has high positive Stability; when it dominates and we delaminate, we get Resonance Cascade.
    /// We use BZ to damage (Stability -0.8) then switch to N2O... Actually: Resonance occurs when
    /// Enthalpy- dominant (Frezon) or Stability+ dominant (N2O) or maxAbs<=0. N2O heals heavily.
    /// This test uses a mix: we damage with Plasma first, then before delam we'd need N2O dominant.
    /// Simpler: use Helium (all zeros) - maxAbs<=0 triggers Resonance. But Helium gives Stability=10, heals.
    /// Skip: No gas combination reaches 0 integrity with Enthalpy- or Stability+ dominant without
    /// directly setting integrity (which is access-restricted). Documented for future test infrastructure.
    /// </summary>
    [Test]
    [Ignore("Frezon/N2O heal integrity; no gas combo reaches delam with Enthalpy- or Stability+ dominant")]
    public async Task FrezonDominantCausesResonanceCascadeDelamination()
    {
        var supermatter = await SpawnSupermatterAndFillGas(Gas.Frezon, 2000f, temperature: 200f);

        await RunTicks(15000);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(supermatter), "Supermatter should have delaminated");
            var singularityCount = SEntMan.EntityQuery<SingularityComponent>().Count();
            var teslaCount = SEntMan.EntityQuery<MetaDataComponent>()
                .Count(m => m.EntityPrototype?.ID == "TeslaMiniEnergyBall");
            Assert.That(singularityCount, Is.EqualTo(0), "Resonance cascade should not spawn Singularity");
            Assert.That(teslaCount, Is.EqualTo(0), "Resonance cascade should not spawn Tesla balls");
        });
    }
}
