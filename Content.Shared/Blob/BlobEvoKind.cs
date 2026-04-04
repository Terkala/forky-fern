using Robust.Shared.Serialization;

namespace Content.Shared.Blob;

/// <summary>
/// Evo shop entry id for <see cref="BlobEvoActionComponent"/>.
/// </summary>
[Serializable, NetSerializable]
public enum BlobEvoKind : byte
{
    GenRate,
    QuickSpread,
    SpreadChance,
    Attack,
    FireResist,
    PoisonResist,
    UnlockDevour,
    UnlockBridge,
    UnlockLauncher,
    UnlockPlasmaphyll,
    UnlockEctothermid,
    UnlockReflective,
}
