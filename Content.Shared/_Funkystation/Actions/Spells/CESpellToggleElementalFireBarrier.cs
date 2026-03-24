using Content.Shared._CE.Actions.Spells;
using Content.Shared._Funkystation.Elementalism;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Network;

namespace Content.Shared._Funkystation.Actions.Spells;

/// <summary>
/// Toggles <see cref="ElementalFireBarrierComponent"/> on the spell target (instant self-casts use the caster).
/// </summary>
public sealed partial class CESpellToggleElementalFireBarrier : CESpellEffect
{
    public override void Effect(EntityManager entManager, CESpellEffectBaseArgs args)
    {
        if (IoCManager.Resolve<INetManager>().IsClient)
            return;

        if (args.Target is not { } uid)
            return;

        entManager.EventBus.RaiseEvent(EventSource.Local, new ToggleElementalFireBarrierEvent(uid));
    }
}
