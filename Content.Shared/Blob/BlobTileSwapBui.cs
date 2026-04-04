using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Blob;

[Serializable, NetSerializable]
public enum BlobTileSwapUiKey : byte
{
    Key
}

/// <summary>
/// Specialist blob tile types that can be chosen from the overmind radial (normal tile → specialist).
/// </summary>
public static class BlobSpecialistRecipes
{
    public readonly record struct Recipe(EntProtoId EntityPrototype, int BioCost);

    /// <summary>Display / validation order for the radial menu.</summary>
    public static readonly BlobTileKind[] MenuOrder =
    [
        BlobTileKind.Ribosome,
        BlobTileKind.Lipid,
        BlobTileKind.Mitochondria,
        BlobTileKind.ThickMembrane,
        BlobTileKind.Firewall,
        BlobTileKind.SlimeLauncher,
        BlobTileKind.Plasmaphyll,
        BlobTileKind.Ectothermid,
        BlobTileKind.Reflective,
    ];

    private static readonly Dictionary<BlobTileKind, Recipe> Recipes = new()
    {
        [BlobTileKind.Ribosome] = new Recipe("MobBlobTileRibosome", 15),
        [BlobTileKind.Lipid] = new Recipe("MobBlobTileLipid", 5),
        [BlobTileKind.Mitochondria] = new Recipe("MobBlobTileMitochondria", 5),
        [BlobTileKind.ThickMembrane] = new Recipe("MobBlobTileMembrane", 5),
        [BlobTileKind.Firewall] = new Recipe("MobBlobTileFirewall", 10),
        [BlobTileKind.SlimeLauncher] = new Recipe("MobBlobTileLauncher", 15),
        [BlobTileKind.Plasmaphyll] = new Recipe("MobBlobTilePlasmaphyll", 30),
        [BlobTileKind.Ectothermid] = new Recipe("MobBlobTileEctothermid", 30),
        [BlobTileKind.Reflective] = new Recipe("MobBlobTileReflective", 15),
    };

    public static bool TryGetRecipe(BlobTileKind kind, out Recipe recipe)
    {
        if (Recipes.TryGetValue(kind, out var found))
        {
            recipe = found;
            return true;
        }

        recipe = default;
        return false;
    }

    /// <summary>
    /// True when this specialist requires an evo unlock that the hive does not have yet.
    /// </summary>
    public static bool IsMenuOptionLocked(BlobHiveComponent hive, BlobTileKind kind)
    {
        return kind switch
        {
            BlobTileKind.SlimeLauncher => !hive.UnlockLauncher,
            BlobTileKind.Plasmaphyll => !hive.UnlockPlasmaphyll,
            BlobTileKind.Ectothermid => !hive.UnlockEctothermid,
            BlobTileKind.Reflective => !hive.UnlockReflective,
            _ => false,
        };
    }
}

[Serializable, NetSerializable]
public sealed class BlobTileSwapChoiceMessage : BoundUserInterfaceMessage
{
    public BlobTileKind Kind { get; }

    public BlobTileSwapChoiceMessage(BlobTileKind kind)
    {
        Kind = kind;
    }
}
