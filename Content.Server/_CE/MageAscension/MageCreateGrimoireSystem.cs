using Content.Shared._CE.MageAscension;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared._CE.MagicEnergy.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.MageAscension;

public sealed class MageCreateGrimoireSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MagicBatterySystem _magicBattery = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly EntProtoId GrimoireProto = "MageGrimoire";

    private const float ManaCost = 100f;
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(20);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MageCreateGrimoireActionEvent>(OnCreateAction);
        SubscribeLocalEvent<MageCreateGrimoireDoAfterEvent>(OnDoAfter);
    }

    private void OnCreateAction(MageCreateGrimoireActionEvent ev)
    {
        if (ev.Handled)
            return;

        var user = ev.Performer;

        if (!HasComp<MageOfAscensionComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("mage-grimoire-fail-not-mage"), user, user);
            ev.Handled = true;
            return;
        }

        if (UserHasGrimoireInTree(user))
        {
            _popup.PopupEntity(Loc.GetString("mage-grimoire-fail-has-grimoire"), user, user);
            ev.Handled = true;
            return;
        }

        if (!_hands.TryGetActiveItem(user, out var book))
        {
            _popup.PopupEntity(Loc.GetString("mage-grimoire-fail-no-book"), user, user);
            ev.Handled = true;
            return;
        }

        if (!_tags.HasTag(book.Value, "Book"))
        {
            _popup.PopupEntity(Loc.GetString("mage-grimoire-fail-not-book"), user, user);
            ev.Handled = true;
            return;
        }

        if (!TryComp<BatteryComponent>(user, out var battery) || battery.LastCharge < ManaCost)
        {
            _popup.PopupEntity(Loc.GetString("mage-grimoire-fail-mana"), user, user);
            ev.Handled = true;
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, Delay, new MageCreateGrimoireDoAfterEvent(), user, used: book)
        {
            NeedHand = true,
            BreakOnMove = true,
            Broadcast = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("mage-grimoire-fail-busy"), user, user);

        ev.Handled = true;
    }

    private void OnDoAfter(MageCreateGrimoireDoAfterEvent ev)
    {
        if (ev.Cancelled || ev.Handled)
            return;

        var user = ev.User;
        if (!TryComp<MageOfAscensionComponent>(user, out _))
            return;

        if (!_hands.TryGetActiveItem(user, out var book) || !_tags.HasTag(book.Value, "Book"))
            return;

        if (UserHasGrimoireInTree(user))
            return;

        if (!TryComp<BatteryComponent>(user, out var battery) || battery.LastCharge < ManaCost)
            return;

        _magicBattery.ChangeMagicCharge((user, battery), -ManaCost);

        var coords = Transform(book.Value).Coordinates;
        QueueDel(book.Value);

        var grim = Spawn(GrimoireProto, coords);
        _hands.PickupOrDrop(user, grim);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/blink.ogg"), user);
        _popup.PopupEntity(Loc.GetString("mage-grimoire-created"), user, user);
    }

    private bool UserHasGrimoireInTree(EntityUid user)
    {
        var xformQuery = GetEntityQuery<TransformComponent>();
        var grimoireQuery = EntityQueryEnumerator<MageGrimoireComponent, TransformComponent>();
        while (grimoireQuery.MoveNext(out var grimUid, out _, out _))
        {
            var parent = grimUid;
            var safety = 0;
            while (parent.IsValid() && safety++ < 32)
            {
                if (parent == user)
                    return true;
                if (!xformQuery.TryGetComponent(parent, out var xf))
                    break;
                parent = xf.ParentUid;
            }
        }

        return false;
    }
}
