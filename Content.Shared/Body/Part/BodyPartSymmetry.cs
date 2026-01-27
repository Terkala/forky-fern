namespace Content.Shared.Body.Part;

public enum BodyPartSymmetry : byte
{
    None, // No symmetry (for head and torso).
    Left, // Left side (left arm or left leg).
    Right, // Right side (right arm or right leg).
}
