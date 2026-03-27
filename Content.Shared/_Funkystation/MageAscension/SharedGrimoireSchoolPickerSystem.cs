using Content.Shared._CE.MageAscension;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared._CE.Skill.Components;
using Content.Shared.Popups;
using Content.Shared.Roles.Components;
using Content.Shared.UserInterface;

namespace Content.Shared._Funkystation.MageAscension;

/// <summary>
/// Gates grimoire skill-tree UIs: mages use <see cref="MageGrimoireComponent"/>; wizards use <see cref="WizardSkillGrimoireComponent"/>.
/// Binds each grimoire to the first valid user who opens the skill tree.
/// </summary>
public sealed class SharedGrimoireSkillTreeOpenValidatorSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MageGrimoireComponent, ActivatableUIOpenAttemptEvent>(OnMageGrimoireOpenAttempt);
        SubscribeLocalEvent<WizardSkillGrimoireComponent, ActivatableUIOpenAttemptEvent>(OnWizardGrimoireOpenAttempt);

        SubscribeLocalEvent<MageGrimoireComponent, AfterActivatableUIOpenEvent>(OnAfterMageGrimoireOpened);
        SubscribeLocalEvent<WizardSkillGrimoireComponent, AfterActivatableUIOpenEvent>(OnAfterWizardGrimoireOpened);
    }

    private void OnMageGrimoireOpenAttempt(Entity<MageGrimoireComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!HasComp<MageOfAscensionComponent>(args.User))
        {
            args.Cancel();
            if (!args.Silent)
                _popup.PopupClient(Loc.GetString("funky-grimoire-skill-tree-not-mage"), ent.Owner, args.User);
            return;
        }

        if (IsWrongBoundUser(ent.Comp.SkillTreeBoundOwner, args.User))
        {
            args.Cancel();
            if (!args.Silent)
                _popup.PopupClient(Loc.GetString("funky-grimoire-skill-tree-wrong-owner"), ent.Owner, args.User);
        }
    }

    private void OnWizardGrimoireOpenAttempt(Entity<WizardSkillGrimoireComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!HasComp<WizardRoleComponent>(args.User))
        {
            args.Cancel();
            if (!args.Silent)
                _popup.PopupClient(Loc.GetString("funky-grimoire-skill-tree-not-wizard"), ent.Owner, args.User);
            return;
        }

        if (IsWrongBoundUser(ent.Comp.SkillTreeBoundOwner, args.User))
        {
            args.Cancel();
            if (!args.Silent)
                _popup.PopupClient(Loc.GetString("funky-grimoire-skill-tree-wrong-owner"), ent.Owner, args.User);
        }
    }

    private bool IsWrongBoundUser(NetEntity? bound, EntityUid user)
    {
        if (bound == null)
            return false;

        if (!TryGetEntity(bound.Value, out var boundUid))
            return false;

        return boundUid != user;
    }

    private void OnAfterMageGrimoireOpened(Entity<MageGrimoireComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        var net = GetNetEntity(args.User);
        if (ent.Comp.SkillTreeBoundOwner == net)
            return;

        ent.Comp.SkillTreeBoundOwner = net;
        Dirty(ent);
    }

    private void OnAfterWizardGrimoireOpened(Entity<WizardSkillGrimoireComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        var net = GetNetEntity(args.User);
        if (ent.Comp.SkillTreeBoundOwner == net)
            return;

        ent.Comp.SkillTreeBoundOwner = net;
        Dirty(ent);
    }
}
