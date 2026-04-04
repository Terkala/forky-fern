using Robust.Shared.Serialization;

namespace Content.Shared.Blob;

[Serializable, NetSerializable]
public enum BlobTileKind : byte
{
    Normal = 0,
    Nucleus = 1,
    Ribosome = 2,
    Lipid = 3,
    Mitochondria = 4,
    ThickMembrane = 5,
    Firewall = 6,
    Reflective = 7,
    SlimeLauncher = 8,
    Plasmaphyll = 9,
    Ectothermid = 10,
    /// <summary>Floor in space; not counted in blob size.</summary>
    Bridge = 11,
}
