namespace Content.Shared.Body.Part;

/// <summary>
/// Constants for body part slot IDs.
/// </summary>
public static class BodyPartSlots
{
    // Torso slots
    public const string LeftArm = "left_arm";
    public const string RightArm = "right_arm";
    public const string LeftLeg = "left_leg";
    public const string RightLeg = "right_leg";
    public const string Head = "head";

    // Note: Arms and legs don't have child slots since hands/feet are part of the arm/leg
}
