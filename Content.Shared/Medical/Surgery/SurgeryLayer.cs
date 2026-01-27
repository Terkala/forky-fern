using Robust.Shared.Serialization;

namespace Content.Shared.Medical.Surgery;

/// <summary>
/// Surgery layers representing the depth of surgical access.
/// </summary>
[Serializable, NetSerializable]
public enum SurgeryLayer : byte
{
    /// <summary>
    /// Skin layer - retracting skin on body parts.
    /// </summary>
    Skin,

    /// <summary>
    /// Tissue layer - retracting tissues and sawing through bones/skull.
    /// </summary>
    Tissue,

    /// <summary>
    /// Organ layer - clamping vessels, severing, and removing/adding organs.
    /// </summary>
    Organ
}
