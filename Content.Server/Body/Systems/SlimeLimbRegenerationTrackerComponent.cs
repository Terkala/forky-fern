// SPDX-FileCopyrightText: 2026 pathetic meowmeow <uhhadd@gmail.com>
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Systems;

namespace Content.Server.Body.Systems;

/// <summary>
/// Component that tracks limb regeneration state for multiple limbs on a slime body.
/// Keyed by SlimeLimbKey (BodyPartType, BodyPartSymmetry) to allow independent regeneration per limb.
/// Server-only (not networked) to avoid LastComponentRemoved triggering client crashes when removed from body.
/// </summary>
[RegisterComponent]
public sealed partial class SlimeLimbRegenerationTrackerComponent : Component
{
    /// <summary>
    /// Dictionary tracking regeneration state for each missing limb.
    /// Key is SlimeLimbKey (BodyPartType, BodyPartSymmetry).
    /// </summary>
    [DataField]
    public Dictionary<SlimeLimbKey, SlimeLimbRegenerationData> RegeneratingLimbs = new();
}
