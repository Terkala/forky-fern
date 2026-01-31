using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.Events;
using Content.Shared.Wires;
using Robust.Shared.Containers;

namespace Content.Shared.Medical.Cybernetics;

/// <summary>
/// Shared system that handles cyber-limb storage access control and stack splitting.
/// </summary>
public abstract class CyberLimbStorageSystem : EntitySystem
{
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] protected readonly SharedWiresSystem Wires = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberLimbComponent, StorageInteractAttemptEvent>(OnStorageInteractAttempt);
    }

    /// <summary>
    /// Blocks storage access when the maintenance panel is closed.
    /// </summary>
    private void OnStorageInteractAttempt(Entity<CyberLimbComponent> ent, ref StorageInteractAttemptEvent args)
    {
        if (!TryComp<WiresPanelComponent>(ent, out var panel))
            return;

        if (!panel.Open)
        {
            args.Cancelled = true;
            if (!args.Silent)
            {
                // Show popup to all nearby players since we don't have a specific user
                Popup.PopupEntity(Loc.GetString("cyber-limb-panel-closed"), ent);
            }
        }
    }
}
