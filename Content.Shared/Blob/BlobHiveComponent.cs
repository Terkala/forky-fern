using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Shared.Blob;

/// <summary>
/// Economy and progression state for one blob hive. Lives on the overmind entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class BlobHiveComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Deployed;

    [DataField, AutoNetworkedField]
    public int BioPoints;

    [DataField, AutoNetworkedField]
    public int BioMax = 40;

    [DataField, AutoNetworkedField]
    public float GenerationPerSecond = 1f;

    [DataField, AutoNetworkedField]
    public int EvoPoints;

    [DataField, AutoNetworkedField]
    public int TileCount;

    [DataField, AutoNetworkedField]
    public int LivingNuclei;

    [DataField, AutoNetworkedField]
    public int NextEvoAtTiles = 20;

    [DataField, AutoNetworkedField]
    public Color Tint = Color.FromHex("#8FBA8F");

    // --- Evo unlock flags (phase 3) ---
    [DataField, AutoNetworkedField]
    public bool UnlockDevour;

    [DataField, AutoNetworkedField]
    public bool UnlockBridge;

    [DataField, AutoNetworkedField]
    public bool UnlockLauncher;

    [DataField, AutoNetworkedField]
    public bool UnlockPlasmaphyll;

    [DataField, AutoNetworkedField]
    public bool UnlockEctothermid;

    [DataField, AutoNetworkedField]
    public bool UnlockReflective;

    // --- Passive upgrade tallies ---
    [DataField, AutoNetworkedField]
    public int GenRatePurchases;

    [DataField, AutoNetworkedField]
    public int QuickSpreadPurchases;

    [DataField, AutoNetworkedField]
    public int SpreadChancePurchases;

    [DataField, AutoNetworkedField]
    public int AttackPurchases;

    [DataField, AutoNetworkedField]
    public bool HasFireResist;

    [DataField, AutoNetworkedField]
    public bool HasPoisonResist;

    /// <summary>
    /// Passive generation from ribosomes (see wiki).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RibosomeGenerationBonus;

    [DataField, AutoNetworkedField]
    public float GenerationMultiplier = 1f;

    /// <summary>
    /// Server-side gate for spread cooldown (after starter threshold). Client ignores.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan NextSpreadAllowed = TimeSpan.Zero;

    /// <summary>
    /// Max blob tiles (non-bridge) per living nucleus. Extra nuclei from promotes raise the cap.
    /// 0 = unlimited.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxBlobTilesPerNucleus = 200;
}
