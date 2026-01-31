using Content.Shared.Medical.Cybernetics;
using Content.Shared.Popups;
using Content.Shared.Wires;
using Robust.Shared.Containers;

namespace Content.Shared.Medical.Cybernetics;

/// <summary>
/// System that handles cyber-limb maintenance panel state changes and bio-rejection penalties.
/// </summary>
public abstract class CyberLimbPanelSystem : EntitySystem
{
    [Dependency] protected readonly SharedPopupSystem Popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberLimbComponent, PanelChangedEvent>(OnPanelChanged);
    }

    /// <summary>
    /// Handles panel state changes and updates cyber-limb flags.
    /// </summary>
    private void OnPanelChanged(Entity<CyberLimbComponent> ent, ref PanelChangedEvent args)
    {
        var cyberLimb = ent.Comp;
        cyberLimb.PanelOpen = args.Open;
        cyberLimb.PanelExposed = args.Open; // Panel exposed when open

        Dirty(ent, cyberLimb);

        // Show popup messages to all nearby players
        if (args.Open)
        {
            Popup.PopupEntity(Loc.GetString("cyber-limb-panel-exposed"), ent);
        }
        else
        {
            Popup.PopupEntity(Loc.GetString("cyber-limb-panel-sealed"), ent);
        }

        // Raise cyber-limb panel changed event for integrity system
        var ev = new CyberLimbPanelChangedEvent(ent, args.Open);
        RaiseLocalEvent(ent, ref ev);
    }
}
