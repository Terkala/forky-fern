using System;
using Content.Shared._CE.MageAscension;
using Robust.Shared.Network;

namespace Content.Shared._CE.Actions.Spells;

/// <summary>
/// Requests short-duration incorporeal-style movement for the elemental rift horror (handled on server).
/// </summary>
public sealed partial class CESpellRequestRiftElementalPhase : CESpellEffect
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);

    public override void Effect(EntityManager entManager, CESpellEffectBaseArgs args)
    {
        if (args.User is not { } user)
            return;

        if (IoCManager.Resolve<INetManager>().IsClient)
            return;

        entManager.EventBus.RaiseLocalEvent(user, new MageRiftElementalPhaseRequestEvent { Duration = Duration });
    }
}
