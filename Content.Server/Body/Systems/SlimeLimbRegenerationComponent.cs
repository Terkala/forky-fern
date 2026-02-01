// SPDX-FileCopyrightText: 2026 pathetic meowmeow <uhhadd@gmail.com>
// SPDX-License-Identifier: MIT

using Content.Shared.Body;
using Content.Shared.Body.Part;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Server.Body.Systems;

/// <summary>
/// Component that tracks limb regeneration state for slimes.
/// Added to the body entity when a limb is lost.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SlimeLimbRegenerationComponent : Component
{
    /// <summary>
    /// The slime body entity that is regenerating a limb.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid Body;

    /// <summary>
    /// The type of body part to regenerate.
    /// </summary>
    [DataField, AutoNetworkedField]
    public BodyPartType PartType;

    /// <summary>
    /// The symmetry of the body part (Left/Right for arms/legs, None for head/torso).
    /// </summary>
    [DataField, AutoNetworkedField]
    public BodyPartSymmetry Symmetry;

    /// <summary>
    /// When regeneration begins (1 minute after limb loss).
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan RegenerationStartTime;

    /// <summary>
    /// When the healing phase begins (after the limb is spawned at 5% health).
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan HealingStartTime;

    /// <summary>
    /// Reference to the regenerated limb entity, if it has been spawned.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? RegeneratedPart;

    /// <summary>
    /// Whether the system is in the healing phase (spawned limb healing to 100%).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsHealing;
}
