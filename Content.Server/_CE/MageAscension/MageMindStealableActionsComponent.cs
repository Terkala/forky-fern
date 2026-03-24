using System.Collections.Generic;

namespace Content.Server._CE.MageAscension;

/// <summary>
/// Mind-local spell actions that can be stolen via Highlander grimoire rules (excludes utilities like create grimoire).
/// </summary>
[RegisterComponent]
public sealed partial class MageMindStealableActionsComponent : Component
{
    public readonly HashSet<EntityUid> SpellActions = new();
}
