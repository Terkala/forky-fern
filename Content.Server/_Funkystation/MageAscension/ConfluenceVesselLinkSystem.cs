using Content.Server.Anomaly;
using Content.Server.Anomaly.Components;
using Content.Shared.Anomaly;
using Content.Shared.Anomaly.Components;

namespace Content.Server._Funkystation.MageAscension;

/// <summary>
/// Clears vessel and scanner state when a linked ley confluence is removed.
/// </summary>
public sealed class ConfluenceVesselLinkSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly AnomalySystem _anomaly = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConfluenceVesselLinkComponent, ComponentShutdown>(OnLinkShutdown);
    }

    private void OnLinkShutdown(EntityUid uid, ConfluenceVesselLinkComponent component, ComponentShutdown args)
    {
        ClearScannersTargeting(uid);

        if (component.ConnectedVessel is not { } vesselUid)
            return;

        if (TryComp<AnomalyVesselComponent>(vesselUid, out var vessel) &&
            vessel.Confluence == uid)
        {
            vessel.Confluence = null;
            vessel.ConfluenceResearchAccumulator = 0f;

            _anomaly.UpdateVesselAppearance(vesselUid, vessel);
        }
    }

    /// <summary>
    /// Reset scanners that had this confluence targeted; mirrors anomaly scanner cleanup.
    /// </summary>
    public void ClearScannersTargeting(EntityUid confluence)
    {
        var query = EntityQueryEnumerator<AnomalyScannerComponent>();
        while (query.MoveNext(out var scannerUid, out var scanner))
        {
            if (scanner.ScannedConfluence != confluence)
                continue;

            scanner.ScannedConfluence = null;
            _ui.CloseUi(scannerUid, AnomalyScannerUiKey.Key);
            _appearance.SetData(scannerUid, AnomalyScannerVisuals.HasAnomaly, false);
            _appearance.SetData(scannerUid, AnomalyScannerVisuals.AnomalyIsSupercritical, false);
            _appearance.SetData(scannerUid, AnomalyScannerVisuals.AnomalyNextPulse, 0);
            _appearance.SetData(scannerUid, AnomalyScannerVisuals.AnomalySeverity, 0);
            _appearance.SetData(scannerUid, AnomalyScannerVisuals.AnomalyStability, AnomalyStabilityVisuals.Stable);
        }
    }
}
