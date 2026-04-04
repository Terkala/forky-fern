using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(BlobRuleSystem))]
public sealed partial class BlobRuleComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? NextRoundEndCheck;

    [DataField]
    public TimeSpan EndCheckDelay = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Blob wins at this tile count (Goon ~500).
    /// </summary>
    [DataField]
    public int VictoryTileCount = 500;
}
