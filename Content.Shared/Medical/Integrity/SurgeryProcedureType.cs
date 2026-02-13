namespace Content.Shared.Medical.Integrity;

/// <summary>
/// Procedure type indices for contextual integrity penalties. Used to clear penalties by procedure without string comparison.
/// </summary>
public static class SurgeryProcedureType
{
    public const int SkinRetraction = 0;
    public const int TissueRetraction = 1;
    public const int BoneSawing = 2;
    public const int BoneSmashing = 3;
    public const int DirtyRoom = 4;
    public const int ImproperTools = 5;
}
