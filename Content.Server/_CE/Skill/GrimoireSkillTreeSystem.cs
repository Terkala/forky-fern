using Content.Shared._CE.MageAscension;
using Content.Shared._CE.Skill;
using Content.Shared._CE.Skill.Components;
using Robust.Server.GameObjects;

namespace Content.Server._CE.Skill;

/// <summary>
/// Pushes an empty BUI state when a grimoire opens the CE skill tree interface.
/// </summary>
public sealed class GrimoireSkillTreeSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<MageGrimoireComponent>(GrimoireSkillTreeUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnMageGrimoireOpened);
        });

        Subs.BuiEvents<WizardSkillGrimoireComponent>(GrimoireSkillTreeUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnWizardGrimoireOpened);
        });
    }

    private void OnMageGrimoireOpened(EntityUid uid, MageGrimoireComponent component, BoundUIOpenedEvent args)
    {
        _ui.SetUiState(uid, GrimoireSkillTreeUiKey.Key, new GrimoireSkillTreeBuiState());
    }

    private void OnWizardGrimoireOpened(EntityUid uid, WizardSkillGrimoireComponent component, BoundUIOpenedEvent args)
    {
        _ui.SetUiState(uid, GrimoireSkillTreeUiKey.Key, new GrimoireSkillTreeBuiState());
    }
}
