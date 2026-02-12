// SPDX-FileCopyrightText: 2026 pathetic meowmeow <uhhadd@gmail.com>
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Part;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Body.Systems;

/// <summary>
/// Data structure for tracking regeneration state of a single limb.
/// </summary>
[Serializable, NetSerializable]
public sealed class SlimeLimbRegenerationData
{
    /// <summary>
    /// When regeneration begins (1 minute after limb loss).
    /// </summary>
    [DataField]
    public TimeSpan RegenerationStartTime;

    /// <summary>
    /// When the healing phase begins (after the limb is spawned at 5% health).
    /// </summary>
    [DataField]
    public TimeSpan HealingStartTime;

    /// <summary>
    /// Reference to the regenerated limb entity, if it has been spawned.
    /// </summary>
    [DataField]
    public NetEntity? RegeneratedPart;

    /// <summary>
    /// Whether the system is in the healing phase (spawned limb healing to 100%).
    /// </summary>
    [DataField]
    public bool IsHealing;
}

/// <summary>
/// Key for tracking limb regeneration by part type and symmetry.
/// </summary>
[Serializable, NetSerializable]
public sealed class SlimeLimbKey
{
    [DataField]
    public BodyPartType PartType;

    [DataField]
    public BodyPartSymmetry Symmetry;

    public SlimeLimbKey()
    {
    }

    public SlimeLimbKey(BodyPartType partType, BodyPartSymmetry symmetry)
    {
        PartType = partType;
        Symmetry = symmetry;
    }

    public override bool Equals(object? obj)
    {
        return obj is SlimeLimbKey key &&
               PartType == key.PartType &&
               Symmetry == key.Symmetry;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(PartType, Symmetry);
    }
}
