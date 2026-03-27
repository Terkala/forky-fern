using Content.Shared._Funkystation.MageAscension;
using Robust.Shared.Network;

namespace Content.Shared._CE.Actions.Spells;

public sealed partial class CESpellBeginHonkClownTransform : CESpellEffect
{
    public override void Effect(EntityManager entManager, CESpellEffectBaseArgs args)
    {
        if (args.User is not { } user)
            return;

        var net = IoCManager.Resolve<INetManager>();
        if (!net.IsServer)
            return;

        entManager.EventBus.RaiseLocalEvent(user, new HonkClownTransformSpellCastEvent { User = user });
    }
}
