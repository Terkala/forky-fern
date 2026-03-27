using Content.Server.Administration;
using Content.Server.Clothing.Systems;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared._CE.MagicEnergy.Systems;
using Content.Shared._Funkystation.MageAscension;
using Content.Shared.Access.Systems;
using Robust.Shared.Player;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Server._Funkystation.MageAscension;

public sealed class HonkClownTransformSystem : EntitySystem
{
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly OutfitSystem _outfit = default!;
    [Dependency] private readonly MagicBatterySystem _magicBattery = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MobThresholdSystem _mobThresholds = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private static readonly ProtoId<JobPrototype> ClownJob = "Clown";
    private static readonly EntProtoId HonkOutfit = "MageHonkBlessedOutfit";
    private static readonly TimeSpan TransformDelay = TimeSpan.FromSeconds(30);
    private const float ManaCost = 100f;
    private const int NameMax = 48;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransformComponent, HonkClownTransformSpellCastEvent>(OnSpellCast);
        SubscribeLocalEvent<HonkClownTransformDoAfterEvent>(OnDoAfterFinished);
    }

    private void OnSpellCast(EntityUid uid, TransformComponent transform, HonkClownTransformSpellCastEvent ev)
    {
        if (ev.User != uid)
            return;

        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        if (!HasComp<MageOfAscensionComponent>(uid))
        {
            _popup.PopupEntity(Loc.GetString("funky-mage-honk-transform-not-mage"), uid, uid);
            return;
        }

        if (HasComp<HonkClownTransformPendingComponent>(uid))
        {
            _popup.PopupEntity(Loc.GetString("funky-mage-honk-transform-busy"), uid, uid);
            return;
        }

        var session = actor.PlayerSession;
        _quickDialog.OpenDialog<string>(session,
            Loc.GetString("funky-mage-honk-transform-dialog-title"),
            Loc.GetString("funky-mage-honk-transform-dialog-prompt"),
            name =>
            {
                var trimmed = name.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    _popup.PopupEntity(Loc.GetString("funky-mage-honk-transform-name-empty"), uid, uid);
                    return;
                }

                if (trimmed.Length > NameMax)
                    trimmed = trimmed[..NameMax];

                if (!Exists(uid) || !HasComp<MageOfAscensionComponent>(uid))
                    return;

                if (!TryComp<ActorComponent>(uid, out _))
                    return;

                var pending = EnsureComp<HonkClownTransformPendingComponent>(uid);
                pending.ChosenName = trimmed;

                var da = new DoAfterArgs(EntityManager, uid, TransformDelay, new HonkClownTransformDoAfterEvent(), uid)
                {
                    Broadcast = true,
                    BreakOnMove = true,
                    NeedHand = false,
                };

                if (!_doAfter.TryStartDoAfter(da))
                {
                    RemComp<HonkClownTransformPendingComponent>(uid);
                    _popup.PopupEntity(Loc.GetString("funky-mage-honk-transform-busy"), uid, uid);
                }
            });
    }

    private void OnDoAfterFinished(HonkClownTransformDoAfterEvent ev)
    {
        var user = ev.User;

        if (!TryComp<HonkClownTransformPendingComponent>(user, out var pending))
            return;

        var chosenName = pending.ChosenName;
        RemCompDeferred<HonkClownTransformPendingComponent>(user);

        if (ev.Cancelled)
            return;

        if (!HasComp<MageOfAscensionComponent>(user))
            return;

        if (!TryComp<BatteryComponent>(user, out var battery) || battery.LastCharge < ManaCost)
        {
            _popup.PopupEntity(Loc.GetString("funky-mage-honk-transform-no-mana"), user, user);
            return;
        }

        _magicBattery.ChangeMagicCharge((user, battery), -ManaCost);

        if (!_outfit.SetOutfit(user, HonkOutfit, unremovable: true, unremovableDeleteOnDrop: true))
        {
            _popup.PopupEntity(Loc.GetString("funky-mage-honk-transform-outfit-fail"), user, user);
            return;
        }

        _metaData.SetEntityName(user, chosenName, raiseEvents: false);

        if (_inventory.TryGetSlotEntity(user, "id", out var idEnt))
        {
            if (_idCard.TryGetIdCard(idEnt.Value, out var card))
            {
                _idCard.TryChangeFullName(card.Owner, chosenName, card);
                if (_proto.TryIndex(ClownJob, out var job))
                {
                    _idCard.TryChangeJobDepartment(card.Owner, job, card);
                    _idCard.TryChangeJobTitle(card.Owner, job.LocalizedName, card);

                    var icon = _proto.Index<JobIconPrototype>(job.Icon);
                    _idCard.TryChangeJobIcon(card.Owner, icon, card);
                }
            }
        }

        if (TryComp<MobThresholdsComponent>(user, out _))
        {
            if (_mobThresholds.TryGetThresholdForState(user, MobState.Critical, out var critTh) && critTh != null)
                _mobThresholds.SetMobStateThreshold(user, critTh.Value + 50, MobState.Critical);

            if (_mobThresholds.TryGetThresholdForState(user, MobState.Dead, out var deadTh) && deadTh != null)
                _mobThresholds.SetMobStateThreshold(user, deadTh.Value + 50, MobState.Dead);

            _mobThresholds.VerifyThresholds(user);
        }

        _popup.PopupEntity(Loc.GetString("funky-mage-honk-transform-success"), user, user);
    }
}
