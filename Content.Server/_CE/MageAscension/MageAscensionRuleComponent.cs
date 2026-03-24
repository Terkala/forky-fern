using System;

namespace Content.Server._CE.MageAscension;

/// <summary>Game rule marker for Mage of Ascension antag selection and round flow.</summary>
[RegisterComponent, Access(typeof(MageAscensionRuleSystem), typeof(MageConfluenceSystem))]
public sealed partial class MageAscensionRuleComponent : Component
{
    /// <summary>When to allow spawning the next unopened confluence (after the previous was opened).</summary>
    public TimeSpan NextConfluenceSpawn;

    /// <summary>The current hidden confluence entity, if any.</summary>
    public EntityUid? ActiveUnopenedConfluence;
}
