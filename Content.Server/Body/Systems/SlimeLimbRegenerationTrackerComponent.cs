// SPDX-FileCopyrightText: 2026 pathetic meowmeow <uhhadd@gmail.com>
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Systems;
using Robust.Shared.GameStates;

namespace Content.Server.Body.Systems;

/// <summary>
/// Component that tracks limb regeneration state for multiple limbs on a slime body.
/// Keyed by SlimeLimbKey (BodyPartType, BodyPartSymmetry) to allow independent regeneration per limb.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SlimeLimbRegenerationTrackerComponent : Component
{
    /// <summary>
    /// Dictionary tracking regeneration state for each missing limb.
    /// Key is SlimeLimbKey (BodyPartType, BodyPartSymmetry).
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<SlimeLimbKey, SlimeLimbRegenerationData> RegeneratingLimbs = new();
}
