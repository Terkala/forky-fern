namespace Content.Shared.Blob;

/// <summary>
/// Attached to InstantAction entities that purchase an Evo upgrade for the performer's hive.
/// </summary>
[RegisterComponent]
public sealed partial class BlobEvoActionComponent : Component
{
    [DataField(required: true)]
    public BlobEvoKind Kind;
}
