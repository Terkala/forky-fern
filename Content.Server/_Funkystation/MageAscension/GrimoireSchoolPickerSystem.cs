using System;
using System.Linq;
using Content.Server._CE.Skill;
using Content.Shared._CE.MageAscension;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared._CE.Skill;
using Content.Shared._CE.Skill.Prototypes;
using Content.Shared._Funkystation.MageAscension;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Funkystation.MageAscension;

public sealed class GrimoireSchoolPickerSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly CESkillSystem _skill = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MageOfAscensionComponent, MageConfluenceOpenedEvent>(OnMageConfluenceOpened);

        Subs.BuiEvents<MageGrimoireComponent>(GrimoireSchoolPickerUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<GrimoireSchoolPickerSelectMessage>(OnSelectSchool);
            subs.Event<GrimoireSpellPickSelectMessage>(OnSelectSpellPick);
        });
    }

    private void OnMageConfluenceOpened(Entity<MageOfAscensionComponent> ent, ref MageConfluenceOpenedEvent args)
    {
        var c = ent.Comp.ConfluencesOpened;
        if (c is < 1 or > 4)
            return;

        ent.Comp.PendingSpellPickTiers.Add(c + 1);
        Dirty(ent);

        _popup.PopupEntity(Loc.GetString("funky-grimoire-spell-pick-reminder"), ent, ent);
    }

    private void OnUiOpened(Entity<MageGrimoireComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!TryComp<MageOfAscensionComponent>(args.Actor, out var mage))
            return;

        PushPickerState(ent.Owner, args.Actor, mage);
    }

    private void PushPickerState(EntityUid grimoire, EntityUid mageUid, MageOfAscensionComponent mage)
    {
        SanitizePendingSpellPicks(mageUid, mage);
        var (tier, options) = GetSpellPickOffer(mage);
        var optionIds = options.Select(o => (string)o).ToArray();

        _ui.SetUiState(grimoire, GrimoireSchoolPickerUiKey.Key,
            new GrimoireSchoolPickerBuiState(mage.HasChosenSchool, mage.SchoolId, tier, optionIds));
    }

    private (int Tier, IReadOnlyList<ProtoId<CESkillPrototype>> Options) GetSpellPickOffer(
        MageOfAscensionComponent mage)
    {
        if (mage.PendingSpellPickTiers.Count == 0
            || mage.SchoolId is not { } schoolId
            || !_proto.TryIndex(schoolId, out MageSchoolPrototype? school))
        {
            return (0, Array.Empty<ProtoId<CESkillPrototype>>());
        }

        var tier = mage.PendingSpellPickTiers[0];
        if (!school.TryGetSpellPickOptions(tier, out var options))
            return (0, Array.Empty<ProtoId<CESkillPrototype>>());

        return (tier, options);
    }

    private void SanitizePendingSpellPicks(EntityUid mageUid, MageOfAscensionComponent mage)
    {
        var changed = false;

        while (mage.PendingSpellPickTiers.Count > 0)
        {
            if (mage.SchoolId is not { } sid || !_proto.TryIndex(sid, out MageSchoolPrototype? school))
                break;

            var tier = mage.PendingSpellPickTiers[0];
            if (school.TryGetSpellPickOptions(tier, out var opts) && opts.Count > 0)
                break;

            mage.PendingSpellPickTiers.RemoveAt(0);
            changed = true;
        }

        if (changed)
            Dirty(mageUid, mage);
    }

    private void OnSelectSchool(Entity<MageGrimoireComponent> ent, ref GrimoireSchoolPickerSelectMessage args)
    {
        var user = args.Actor;

        if (!TryComp<MageOfAscensionComponent>(user, out var mage))
            return;

        if (mage.HasChosenSchool)
        {
            _popup.PopupEntity(Loc.GetString("funky-grimoire-already-chosen"), user, user);
            return;
        }

        if (!_proto.TryIndex(args.SchoolId, out MageSchoolPrototype? school))
            return;

        if (!_skill.TryGetSkillStorage(user, out var storage))
        {
            _popup.PopupEntity(Loc.GetString("funky-grimoire-no-skill-storage"), user, user);
            return;
        }

        if (!_skill.TryAddSkill(storage.Owner, school.PackageSkill, storage.Comp, free: true))
        {
            _popup.PopupEntity(Loc.GetString("funky-grimoire-grant-failed"), user, user);
            return;
        }

        mage.SchoolId = school.ID;
        mage.HasChosenSchool = true;
        Dirty(user, mage);

        _popup.PopupEntity(Loc.GetString("funky-grimoire-school-chosen", ("school", Loc.GetString(school.Name))), user, user);

        PushPickerState(ent.Owner, user, mage);
    }

    private void OnSelectSpellPick(Entity<MageGrimoireComponent> ent, ref GrimoireSpellPickSelectMessage args)
    {
        var user = args.Actor;

        if (!TryComp<MageOfAscensionComponent>(user, out var mage))
            return;

        if (mage.PendingSpellPickTiers.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("funky-grimoire-spell-pick-none-pending"), user, user);
            return;
        }

        if (mage.SchoolId is not { } schoolId || !_proto.TryIndex(schoolId, out MageSchoolPrototype? school))
            return;

        var tier = mage.PendingSpellPickTiers[0];
        if (!school.TryGetSpellPickOptions(tier, out var options))
        {
            mage.PendingSpellPickTiers.RemoveAt(0);
            Dirty(user, mage);
            PushPickerState(ent.Owner, user, mage);
            return;
        }

        var skillId = new ProtoId<CESkillPrototype>(args.SkillId);
        if (!options.Any(o => o == skillId))
        {
            _popup.PopupEntity(Loc.GetString("funky-grimoire-spell-pick-invalid"), user, user);
            return;
        }

        if (!_skill.TryGetSkillStorage(user, out var storage))
        {
            _popup.PopupEntity(Loc.GetString("funky-grimoire-no-skill-storage"), user, user);
            return;
        }

        if (!_skill.TryAddSkill(storage.Owner, skillId, storage.Comp, free: true))
        {
            _popup.PopupEntity(Loc.GetString("funky-grimoire-grant-failed"), user, user);
            return;
        }

        mage.PendingSpellPickTiers.RemoveAt(0);
        Dirty(user, mage);

        _popup.PopupEntity(Loc.GetString("funky-grimoire-spell-pick-learned", ("spell", _skill.GetSkillName(skillId))), user, user);

        PushPickerState(ent.Owner, user, mage);
    }
}
