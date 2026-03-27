namespace Content.Shared._Funkystation.MageAscension;

/// <summary>
/// Server-only pending clown name between dialog confirmation and do-after completion.
/// </summary>
[RegisterComponent]
public sealed partial class HonkClownTransformPendingComponent : Component
{
    [DataField]
    public string ChosenName = string.Empty;
}
