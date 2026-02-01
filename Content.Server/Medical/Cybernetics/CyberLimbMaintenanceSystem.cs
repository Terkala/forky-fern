// SPDX-FileCopyrightText: 2025
//
// SPDX-License-Identifier: MIT

using Content.Shared.Medical.Cybernetics;
using Content.Shared.Wires;
using Robust.Shared.GameObjects;

namespace Content.Server.Medical.Cybernetics;

/// <summary>
/// System that handles maintenance panel state changes triggered by surgery step component manipulation.
/// Bridges surgery step component additions/removals with the existing WiresPanelComponent system.
/// </summary>
public sealed class CyberLimbMaintenanceSystem : EntitySystem
{
    [Dependency] private readonly SharedWiresSystem _wiresSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MaintenancePanelOpenComponent, ComponentAdd>(OnMaintenancePanelOpenAdded);
        SubscribeLocalEvent<MaintenancePanelOpenComponent, ComponentRemove>(OnMaintenancePanelOpenRemoved);
        SubscribeLocalEvent<MaintenanceScrewsExposedComponent, ComponentAdd>(OnMaintenanceScrewsExposedAdded);
        SubscribeLocalEvent<MaintenanceScrewsExposedComponent, ComponentRemove>(OnMaintenanceScrewsExposedRemoved);
    }

    /// <summary>
    /// Handles MaintenancePanelOpenComponent addition - opens the maintenance panel.
    /// </summary>
    private void OnMaintenancePanelOpenAdded(EntityUid uid, MaintenancePanelOpenComponent component, ComponentAdd args)
    {
        if (!TryComp<WiresPanelComponent>(uid, out var panel))
            return;

        // Open the panel
        _wiresSystem.TogglePanel(uid, panel, true);

        // Update CyberLimbComponent panel state
        if (TryComp<CyberLimbComponent>(uid, out var cyberLimb))
        {
            cyberLimb.PanelOpen = true;
            Dirty(uid, cyberLimb);
        }
    }

    /// <summary>
    /// Handles MaintenancePanelOpenComponent removal - closes the maintenance panel.
    /// </summary>
    private void OnMaintenancePanelOpenRemoved(EntityUid uid, MaintenancePanelOpenComponent component, ComponentRemove args)
    {
        if (!TryComp<WiresPanelComponent>(uid, out var panel))
            return;

        // Close the panel
        _wiresSystem.TogglePanel(uid, panel, false);

        // Update CyberLimbComponent panel state
        if (TryComp<CyberLimbComponent>(uid, out var cyberLimb))
        {
            cyberLimb.PanelOpen = false;
            Dirty(uid, cyberLimb);
        }
    }

    /// <summary>
    /// Handles MaintenanceScrewsExposedComponent addition - exposes the maintenance panel.
    /// </summary>
    private void OnMaintenanceScrewsExposedAdded(EntityUid uid, MaintenanceScrewsExposedComponent component, ComponentAdd args)
    {
        // Update CyberLimbComponent panel exposed state
        if (TryComp<CyberLimbComponent>(uid, out var cyberLimb))
        {
            cyberLimb.PanelExposed = true;
            Dirty(uid, cyberLimb);

            // Raise panel changed event to trigger integrity recalculation
            var ev = new CyberLimbPanelChangedEvent(uid, cyberLimb.PanelOpen);
            RaiseLocalEvent(uid, ref ev);
        }
    }

    /// <summary>
    /// Handles MaintenanceScrewsExposedComponent removal - seals the maintenance panel.
    /// </summary>
    private void OnMaintenanceScrewsExposedRemoved(EntityUid uid, MaintenanceScrewsExposedComponent component, ComponentRemove args)
    {
        // Update CyberLimbComponent panel exposed state
        if (TryComp<CyberLimbComponent>(uid, out var cyberLimb))
        {
            cyberLimb.PanelExposed = false;
            Dirty(uid, cyberLimb);

            // Raise panel changed event to trigger integrity recalculation
            var ev = new CyberLimbPanelChangedEvent(uid, cyberLimb.PanelOpen);
            RaiseLocalEvent(uid, ref ev);
        }
    }
}
