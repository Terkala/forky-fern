using Content.Shared._CE.MageAscension;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using System.Linq;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CE.MageAscension;

public sealed class MageSpellStealSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;

    private static readonly EntProtoId Ash = "Ash";

    private static readonly TimeSpan StealDelay = TimeSpan.FromSeconds(12);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<MageSpellStealDoAfterEvent>(OnStealDoAfter);
    }

    private void OnInteractUsing(InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<MobStateComponent>(args.Target, out var mob) || mob.CurrentState != MobState.Dead)
            return;

        if (!HasComp<MageOfAscensionComponent>(args.User))
            return;

        if (!TryComp<MageGrimoireComponent>(args.Used, out var grim) || !grim.IsOpen)
            return;

        if (!TryComp<MindContainerComponent>(args.Target, out var mindContainer) || mindContainer.Mind is not { } victimMind)
        {
            _popup.PopupEntity(Loc.GetString("mage-steal-fail-no-mind"), args.User, args.User);
            args.Handled = true;
            return;
        }

        if (!TryComp<MageMindStealableActionsComponent>(victimMind, out var stealable) || stealable.SpellActions.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("mage-steal-fail-nothing"), args.User, args.User);
            args.Handled = true;
            return;
        }

        var da = new DoAfterArgs(EntityManager, args.User, StealDelay, new MageSpellStealDoAfterEvent(), args.User,
            target: args.Target, used: args.Used)
        {
            NeedHand = true,
            BreakOnMove = true,
            Broadcast = true,
        };

        if (!_doAfter.TryStartDoAfter(da))
            _popup.PopupEntity(Loc.GetString("mage-steal-fail-busy"), args.User, args.User);

        args.Handled = true;
    }

    private void OnStealDoAfter(MageSpellStealDoAfterEvent ev)
    {
        if (ev.Cancelled)
            return;

        var user = ev.User;
        if (ev.Target is not { } corpse || !TryComp<MobStateComponent>(corpse, out var mob) || mob.CurrentState != MobState.Dead)
            return;

        if (!HasComp<MageOfAscensionComponent>(user))
            return;

        if (ev.Used is not { } grimoire || !TryComp<MageGrimoireComponent>(grimoire, out var g) || !g.IsOpen)
            return;

        if (!TryComp<MindContainerComponent>(corpse, out var mindContainer) || mindContainer.Mind is not { } victimMind)
            return;

        if (!TryComp<MageMindStealableActionsComponent>(victimMind, out var stealable) || stealable.SpellActions.Count == 0)
            return;

        if (!_mind.TryGetMind(user, out var attackerMind, out _))
            return;

        var pick = _random.Pick(stealable.SpellActions.ToList());
        if (!Exists(pick))
            return;

        if (MetaData(pick).EntityPrototype is not { } protoDef)
            return;

        stealable.SpellActions.Remove(pick);
        _actionContainer.RemoveAction(pick);

        var newAction = _actionContainer.AddAction(attackerMind, protoDef.ID);
        if (newAction != null)
        {
            var attackerSteal = EnsureComp<MageMindStealableActionsComponent>(attackerMind);
            attackerSteal.SpellActions.Add(newAction.Value);
        }

        var coords = Transform(corpse).Coordinates;
        Spawn(Ash, coords);
        QueueDel(corpse);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/blink.ogg"), user);
        _popup.PopupEntity(Loc.GetString("mage-steal-success"), user, user);
    }
}
