namespace Content.Shared._Funkystation.MageAscension;

/// <summary>
/// Raised on the caster when <see cref="Content.Shared._CE.Actions.Spells.CESpellBeginHonkClownTransform"/> runs (server).
/// </summary>
public sealed class HonkClownTransformSpellCastEvent : EntityEventArgs
{
    public EntityUid User;
}
