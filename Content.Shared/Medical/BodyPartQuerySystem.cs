using Content.Shared.Body.Events;
using Content.Shared.Body.Part;

namespace Content.Shared.Medical;

/// <summary>
/// System for querying body parts using TargetBodyPart enum.
/// Provides conversion between TargetBodyPart enum and actual body part entities.
/// </summary>
public sealed class BodyPartQuerySystem : EntitySystem
{
    /// <summary>
    /// Gets all body parts for an entity, optionally filtered by part type and symmetry.
    /// </summary>
    /// <param name="entity">The entity to query</param>
    /// <param name="partType">Optional filter by body part type</param>
    /// <param name="symmetry">Optional filter by body part symmetry</param>
    /// <returns>Enumerable of (EntityUid, BodyPartComponent) tuples for matching body parts</returns>
    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyParts(
        EntityUid entity,
        BodyPartType? partType = null,
        BodyPartSymmetry? symmetry = null)
    {
        var ev = new GetBodyPartsEvent();
        RaiseLocalEvent(entity, ref ev);

        var seen = new HashSet<EntityUid>();
        foreach (var part in ev.Parts)
        {
            if (!seen.Add(part.Id))
                continue;

            if (partType.HasValue && part.Component.PartType != partType.Value)
                continue;

            if (symmetry.HasValue && part.Component.Symmetry != symmetry.Value)
                continue;

            yield return part;
        }
    }

    /// <summary>
    /// Gets a specific body part by TargetBodyPart enum.
    /// Returns null if the part doesn't exist (e.g., missing limb).
    /// Note: LeftHand/RightHand map to LeftArm/RightArm, LeftFoot/RightFoot map to LeftLeg/RightLeg.
    /// </summary>
    /// <param name="entity">The entity to query</param>
    /// <param name="targetPart">The target body part to find</param>
    /// <returns>The body part entity and component, or null if not found</returns>
    public (EntityUid Id, BodyPartComponent Component)? GetBodyPart(EntityUid entity, TargetBodyPart targetPart)
    {
        var (partType, symmetry) = ConvertTargetBodyPart(targetPart);

        foreach (var part in GetBodyParts(entity, partType, symmetry))
        {
            return (part.Id, part.Component);
        }

        return null;
    }

    /// <summary>
    /// Converts a BodyPartComponent to TargetBodyPart enum.
    /// </summary>
    /// <param name="part">The body part component</param>
    /// <returns>TargetBodyPart enum value</returns>
    public TargetBodyPart GetTargetBodyPart(BodyPartComponent part)
    {
        return GetTargetBodyPart(part.PartType, part.Symmetry);
    }

    /// <summary>
    /// Converts BodyPartType and BodyPartSymmetry to TargetBodyPart enum.
    /// </summary>
    /// <param name="partType">The body part type</param>
    /// <param name="symmetry">The body part symmetry (null for non-symmetric parts)</param>
    /// <returns>TargetBodyPart enum value</returns>
    public TargetBodyPart GetTargetBodyPart(BodyPartType partType, BodyPartSymmetry? symmetry = null)
    {
        return partType switch
        {
            BodyPartType.Head => TargetBodyPart.Head,
            BodyPartType.Torso => TargetBodyPart.Torso,
            BodyPartType.Arm => symmetry switch
            {
                BodyPartSymmetry.Left => TargetBodyPart.LeftArm,
                BodyPartSymmetry.Right => TargetBodyPart.RightArm,
                _ => TargetBodyPart.LeftArm // Default to left if symmetry not specified
            },
            BodyPartType.Leg => symmetry switch
            {
                BodyPartSymmetry.Left => TargetBodyPart.LeftLeg,
                BodyPartSymmetry.Right => TargetBodyPart.RightLeg,
                _ => TargetBodyPart.LeftLeg // Default to left if symmetry not specified
            },
            _ => TargetBodyPart.Torso // Default fallback
        };
    }

    /// <summary>
    /// Converts TargetBodyPart enum to BodyPartType and BodyPartSymmetry.
    /// Handles Groin → Torso mapping.
    /// </summary>
    /// <param name="targetPart">The target body part enum</param>
    /// <returns>Tuple of (BodyPartType, BodyPartSymmetry?)</returns>
    public (BodyPartType PartType, BodyPartSymmetry? Symmetry) ConvertTargetBodyPart(TargetBodyPart targetPart)
    {
        return targetPart switch
        {
            TargetBodyPart.Head => (BodyPartType.Head, null),
            TargetBodyPart.Torso => (BodyPartType.Torso, null),
            TargetBodyPart.Groin => (BodyPartType.Torso, null), // Map Groin to Torso
            TargetBodyPart.LeftArm => (BodyPartType.Arm, BodyPartSymmetry.Left),
            TargetBodyPart.LeftHand => (BodyPartType.Arm, BodyPartSymmetry.Left), // Hand maps to Arm
            TargetBodyPart.RightArm => (BodyPartType.Arm, BodyPartSymmetry.Right),
            TargetBodyPart.RightHand => (BodyPartType.Arm, BodyPartSymmetry.Right), // Hand maps to Arm
            TargetBodyPart.LeftLeg => (BodyPartType.Leg, BodyPartSymmetry.Left),
            TargetBodyPart.LeftFoot => (BodyPartType.Leg, BodyPartSymmetry.Left), // Foot maps to Leg
            TargetBodyPart.RightLeg => (BodyPartType.Leg, BodyPartSymmetry.Right),
            TargetBodyPart.RightFoot => (BodyPartType.Leg, BodyPartSymmetry.Right), // Foot maps to Leg
            _ => (BodyPartType.Torso, null) // Default fallback
        };
    }
}
