using System.Collections.Generic;
using System.Threading;
using Content.Shared._CE.MageAscension;
using Content.Shared._CE.Skill;
using Content.Shared._CE.Skill.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Server._CE.Skill;

/// <summary>
/// Pushes BUI state for the CE skill tree on grimoires, syncs mage grimoire open visuals with the UI session.
/// </summary>
public sealed class GrimoireSkillTreeSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _sharedUi = default!;

    /// <summary>
    /// Matches <c>icon_opening</c> / <c>icon_closing</c> delay totals in book.rsi (4 × 0.1s).
    /// </summary>
    private static readonly TimeSpan MageBookAnimDuration = TimeSpan.FromMilliseconds(400);

    private readonly Dictionary<EntityUid, CancellationTokenSource> _mageBookAnimCancel = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<MageGrimoireComponent>(GrimoireSkillTreeUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnMageGrimoireBuiOpened);
            subs.Event<BoundUIClosedEvent>(OnMageGrimoireBuiClosed);
        });

        Subs.BuiEvents<WizardSkillGrimoireComponent>(GrimoireSkillTreeUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnWizardGrimoireBuiOpened);
        });

        SubscribeLocalEvent<MageGrimoireComponent, EntityTerminatingEvent>(OnMageGrimoireTerminating);
    }

    private void OnMageGrimoireTerminating(Entity<MageGrimoireComponent> ent, ref EntityTerminatingEvent args)
    {
        CancelMageBookAnim(ent.Owner);
    }

    private void CancelMageBookAnim(EntityUid uid)
    {
        if (!_mageBookAnimCancel.Remove(uid, out var cts))
            return;

        cts.Cancel();
        cts.Dispose();
    }

    private void OnMageGrimoireBuiOpened(EntityUid uid, MageGrimoireComponent comp, BoundUIOpenedEvent args)
    {
        _ui.SetUiState(uid, GrimoireSkillTreeUiKey.Key, new GrimoireSkillTreeBuiState());

        comp.IsOpen = true;
        Dirty(uid, comp);

        CancelMageBookAnim(uid);
        _appearance.SetData(uid, GrimoireBookVisuals.State, GrimoireBookVisualState.Opening);

        var cts = new CancellationTokenSource();
        _mageBookAnimCancel[uid] = cts;
        Robust.Shared.Timing.Timer.Spawn(MageBookAnimDuration, () =>
        {
            try
            {
                if (TerminatingOrDeleted(uid) || cts.IsCancellationRequested)
                    return;

                _appearance.SetData(uid, GrimoireBookVisuals.State, GrimoireBookVisualState.Open);
            }
            finally
            {
                _mageBookAnimCancel.Remove(uid);
            }
        }, cts.Token);
    }

    private void OnMageGrimoireBuiClosed(EntityUid uid, MageGrimoireComponent comp, BoundUIClosedEvent args)
    {
        if (_sharedUi.IsUiOpen(uid, GrimoireSkillTreeUiKey.Key))
            return;

        comp.IsOpen = false;
        Dirty(uid, comp);

        CancelMageBookAnim(uid);
        _appearance.SetData(uid, GrimoireBookVisuals.State, GrimoireBookVisualState.Closing);

        var cts = new CancellationTokenSource();
        _mageBookAnimCancel[uid] = cts;
        Robust.Shared.Timing.Timer.Spawn(MageBookAnimDuration, () =>
        {
            try
            {
                if (TerminatingOrDeleted(uid) || cts.IsCancellationRequested)
                    return;

                _appearance.SetData(uid, GrimoireBookVisuals.State, GrimoireBookVisualState.Closed);
            }
            finally
            {
                _mageBookAnimCancel.Remove(uid);
            }
        }, cts.Token);
    }

    private void OnWizardGrimoireBuiOpened(EntityUid uid, WizardSkillGrimoireComponent component, BoundUIOpenedEvent args)
    {
        _ui.SetUiState(uid, GrimoireSkillTreeUiKey.Key, new GrimoireSkillTreeBuiState());
    }
}
