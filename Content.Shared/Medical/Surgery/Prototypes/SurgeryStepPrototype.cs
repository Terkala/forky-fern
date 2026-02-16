using Content.Shared.Damage;
using Content.Shared.Medical.Surgery;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Prototypes;

[Prototype]
public sealed partial class SurgeryStepPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public SurgeryLayer Layer { get; private set; }

    [DataField]
    public LocId? Name { get; private set; }

    [DataField]
    public int Penalty { get; private set; }

    [DataField]
    public int ProcedureTypeIndex { get; private set; }

    [DataField]
    public string? RequiredToolTag { get; private set; }

    /// <summary>
    /// Alternative tags that can be used as improvised tools (e.g. CuttingTool for SurgeryTool steps).
    /// When used, DoAfterDelay is multiplied by ImprovisedDelayMultiplier.
    /// </summary>
    [DataField]
    public List<string> ImprovisedToolTags { get; private set; } = new();

    /// <summary>
    /// Multiplier for DoAfterDelay when using an improvised tool (e.g. 1.5 = 50% longer).
    /// </summary>
    [DataField]
    public float ImprovisedDelayMultiplier { get; private set; } = 1.5f;

    [DataField]
    public float DoAfterDelay { get; private set; } = 2f;

    /// <summary>
    /// Damage applied when performing opening steps (e.g. CreateIncision applies Slash).
    /// </summary>
    [DataField]
    public DamageSpecifier? Damage { get; private set; }

    /// <summary>
    /// Healing on closing steps - DamageSpecifier with negative values (e.g. Slash: -2).
    /// Applied via TryChangeDamage directly, without UniversalTopicalsHealModifier.
    /// </summary>
    [DataField]
    public DamageSpecifier? HealAmount { get; private set; }

    /// <summary>
    /// Sound played when the step is performed.
    /// </summary>
    [DataField]
    public SoundSpecifier? Sound { get; private set; }
}
