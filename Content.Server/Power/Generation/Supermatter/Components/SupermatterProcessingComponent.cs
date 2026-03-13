using Content.Shared.Atmos;
using Content.Shared.Audio;
using Content.Shared.Power.Generation.Supermatter.Components;
using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Power.Generation.Supermatter.Components;

/// <summary>
/// Server-only processing state for the Supermatter. Not networked.
/// Holds internal tallies, timers, and caches.
/// </summary>
[RegisterComponent]
[Access(typeof(SupermatterSystem))]
public sealed partial class SupermatterProcessingComponent : Component
{
    /// <summary>
    /// Reproduction tally from positive Growth. Decays 10% per tick; 10% goes to SecondTally.
    /// </summary>
    [ViewVariables]
    public float Reproduction;

    /// <summary>
    /// Second tally for shard spawning. When >= 1000, spawn shard and reset.
    /// </summary>
    [ViewVariables]
    public float SecondTally;

    /// <summary>
    /// Matter healing tally from absorbing non-living matter. Every 10 points = 1 Integrity heal.
    /// </summary>
    [ViewVariables]
    public float MatterHealing;

    /// <summary>
    /// Power gained from matter absorption, drained gradually into main Power.
    /// </summary>
    [ViewVariables]
    public float MatterPower;

    /// <summary>
    /// Timer for lightning. When <= 0, fire lightning and reset.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [ViewVariables]
    public TimeSpan LightningTimer = TimeSpan.Zero;

    /// <summary>
    /// Count of lightning bolts fired this round. Used for testing.
    /// </summary>
    [ViewVariables]
    public int LightningFiredCount;

    /// <summary>
    /// Cached absorbed gas mixture for processing. Cleared and refilled each tick.
    /// </summary>
    [ViewVariables]
    public GasMixture? AbsorbedMixture;

    /// <summary>
    /// Integrity damage from Enthalpy thermal effects this tick. Applied in ApplyIntegrityChanges.
    /// </summary>
    [ViewVariables]
    public float IntegrityDamageThisTick;

    #region Config (YAML defaults)

    [DataField("ratioPerTile")]
    public float RatioPerTile = 0.09f;

    [DataField("stabilityDefault")]
    public float StabilityDefault = 10f;

    [DataField("powerMultiplier")]
    public float PowerMultiplier = 1000f;

    [DataField("decayStabilityMultiplier")]
    public float DecayStabilityMultiplier = 8f;

    [DataField("integrityMax")]
    public float IntegrityMax = 1000f;

    [DataField("powerDamageDivisor")]
    public float PowerDamageDivisor = 500f;

    [DataField("vacuumDamage")]
    public float VacuumDamage = 0.5f;

    [DataField("vacuumThresholdKpa")]
    public float VacuumThresholdKpa = 10f;

    [DataField("matterHealingThreshold")]
    public float MatterHealingThreshold = 10f;

    [DataField("damageCap")]
    public float DamageCap = 2f;

    [DataField("growthAbsorptionDivisor")]
    public float GrowthAbsorptionDivisor = 45f;

    [DataField("reproductionDecay")]
    public float ReproductionDecay = 0.9f;

    [DataField("reproductionShardThreshold")]
    public float ReproductionShardThreshold = 1000f;

    [DataField("growthPowerThreshold")]
    public float GrowthPowerThreshold = 3000f;

    [DataField("shardPrototype")]
    public string ShardPrototype = "SupermatterShard";

    [DataField("shardsPerCrystal")]
    public int ShardsPerCrystal = 100;

    /// <summary>
    /// Reference interval (0.5s) for scaling per-cycle values to maintain tickrate invariance when running on atmos ticks.
    /// </summary>
    [DataField("processingInterval")]
    public TimeSpan ProcessingInterval = TimeSpan.FromSeconds(0.5);

    [DataField("lightningIntervalBase")]
    public float LightningIntervalBase = 10f;

    [DataField("lightningIntervalConductivityFactor")]
    public float LightningIntervalConductivityFactor = 0.1f;

    [DataField("lightningIntervalMin")]
    public float LightningIntervalMin = 0.5f;

    [DataField("lightningPowerThresholds")]
    public float[] LightningPowerThresholds = [3000f, 6000f, 9000f];

    [DataField("ashingRange")]
    public float AshingRange = 0.6f;

    [DataField("ashingPowerSmall")]
    public float AshingPowerSmall = 200f;

    [DataField("ashingPowerMedium")]
    public float AshingPowerMedium = 1000f;

    [DataField("ashingPowerLarge")]
    public float AshingPowerLarge = 2000f;

    [DataField("ashingPowerBase")]
    public float AshingPowerBase = 5f;

    [DataField("ashingLivingIntegrityDivisor")]
    public float AshingLivingIntegrityDivisor = 10f;

    [DataField("ashingMassSmall")]
    public float AshingMassSmall = 15f;

    [DataField("ashingMassLarge")]
    public float AshingMassLarge = 80f;

    [DataField("ashingSound")]
    public SoundSpecifier? AshingSound = new SoundPathSpecifier("/Audio/_Funkystation/Supermatter/supermatter.ogg");

    [DataField("ashingAshPrototype")]
    public string AshingAshPrototype = "Ash";

    [DataField("nibblesMinMols")]
    public float NibblesMinMols = 10f;

    [DataField("nibblesConsumeRatio")]
    public float NibblesConsumeRatio = 0.1f;

    [DataField("pluoxiumMinMols")]
    public float PluoxiumMinMols = 10f;

    [DataField("pluoxiumConsumeRatio")]
    public float PluoxiumConsumeRatio = 0.1f;

    [DataField("healiumHealAmount")]
    public float HealiumHealAmount = 10f;

    [DataField("healiumIntegrityPerHeal")]
    public float HealiumIntegrityPerHeal = 1f;

    [DataField("ambientCalmSound")]
    public SoundSpecifier? AmbientCalmSound = new SoundPathSpecifier("/Audio/_Funkystation/Supermatter/calm.ogg");

    [DataField("ambientDelammingSound")]
    public SoundSpecifier? AmbientDelammingSound = new SoundPathSpecifier("/Audio/_Funkystation/Supermatter/delamming.ogg");

    [DataField("lightningSound")]
    public SoundSpecifier? LightningSound = new SoundPathSpecifier("/Audio/_Funkystation/Supermatter/lightning.ogg");

    [DataField("antiLightningSound")]
    public SoundSpecifier? AntiLightningSound = new SoundPathSpecifier("/Audio/_Funkystation/Supermatter/marauder.ogg");

    [ViewVariables]
    public EntityUid? AmbientLoopEntity;

    [ViewVariables]
    public bool AmbientLoopIsDelamming;

    #endregion
}
