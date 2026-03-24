using Content.Shared._CE.MageAscension;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared._CE.MagicEnergy.Systems;
using Content.Shared.Actions;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Robust.Shared.Map;

namespace Content.Server._CE.MageAscension;

public sealed class MageUniversalBlinkSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly MagicBatterySystem _magicBattery = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private const float ManaCost = 25f;
    private const float MaxRange = 16f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MageUniversalBlinkEvent>(OnBlink);
    }

    private void OnBlink(MageUniversalBlinkEvent ev)
    {
        if (ev.Handled)
            return;

        var user = ev.Performer;

        if (!HasComp<MageOfAscensionComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("mage-blink-fail-not-mage"), user, user);
            return;
        }

        if (!TryComp<BatteryComponent>(user, out var battery))
        {
            _popup.PopupEntity(Loc.GetString("mage-blink-fail-no-mana"), user, user);
            return;
        }

        if (battery.LastCharge < ManaCost)
        {
            _popup.PopupEntity(Loc.GetString("mage-blink-fail-not-enough-mana"), user, user);
            return;
        }

        var origin = _transform.GetMapCoordinates(user);
        var target = _transform.ToMapCoordinates(ev.Target);

        if (!_examine.InRangeUnOccluded(origin, target, MaxRange, null))
        {
            _popup.PopupEntity(Loc.GetString("mage-blink-fail-los"), user, user);
            return;
        }

        _magicBattery.ChangeMagicCharge((user, battery), -ManaCost);

        if (TryComp<PullableComponent>(user, out var pull) && _pulling.IsPulled(user, pull))
            _pulling.TryStopPull(user, pull);

        if (TryComp<PullerComponent>(user, out var puller) && TryComp<PullableComponent>(puller.Pulling, out var pullable))
            _pulling.TryStopPull(puller.Pulling.Value, pullable);

        var xform = Transform(user);
        _transform.SetCoordinates(user, xform, ev.Target);
        _transform.AttachToGridOrMap(user, xform);
        ev.Handled = true;
    }
}
