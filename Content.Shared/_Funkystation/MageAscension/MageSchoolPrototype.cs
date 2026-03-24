using System;
using Content.Shared._CE.Skill.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Funkystation.MageAscension;

[DataDefinition]
public sealed partial class MageSpellTierPickEntry
{
    [DataField(required: true)]
    public int Tier { get; private set; }

    [DataField(required: true)]
    public List<ProtoId<CESkillPrototype>> Options { get; private set; } = new();
}

[Prototype]
public sealed partial class MageSchoolPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Player-facing school name (FTL).
    /// </summary>
    [DataField(required: true)]
    public LocId Name { get; private set; }

    /// <summary>
    /// Package skill granted when this school is chosen (<c>TryAddSkill(..., free: true)</c>).
    /// </summary>
    [DataField(required: true)]
    public ProtoId<CESkillPrototype> PackageSkill { get; private set; }

    /// <summary>
    /// For each spell tier (2+), spells the mage may choose one of after opening leylines.
    /// </summary>
    [DataField]
    public List<MageSpellTierPickEntry> TierSpellPicks { get; private set; } = new();

    public bool TryGetSpellPickOptions(int tier, out IReadOnlyList<ProtoId<CESkillPrototype>> options)
    {
        foreach (var entry in TierSpellPicks)
        {
            if (entry.Tier != tier)
                continue;

            options = entry.Options;
            return options.Count > 0;
        }

        options = Array.Empty<ProtoId<CESkillPrototype>>();
        return false;
    }
}
