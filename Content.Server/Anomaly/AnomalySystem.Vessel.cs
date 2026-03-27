// SPDX-FileCopyrightText: 2023-2024 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2025 Quantum-cross <7065792+Quantum-cross@users.noreply.github.com>
// SPDX-License-Identifier: MIT

// Funky
using Content.Server._Funkystation.MageAscension;
using Content.Server.Anomaly.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared.Anomaly;
using Content.Shared.Anomaly.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Research.Components;

namespace Content.Server.Anomaly;

/// <summary>
/// This handles anomalous vessel as well as
/// the calculations for how many points they
/// should produce.
/// </summary>
public sealed partial class AnomalySystem
{
    private void InitializeVessel()
    {
        SubscribeLocalEvent<AnomalyVesselComponent, ComponentShutdown>(OnVesselShutdown);
        SubscribeLocalEvent<AnomalyVesselComponent, MapInitEvent>(OnVesselMapInit);
        SubscribeLocalEvent<AnomalyVesselComponent, InteractUsingEvent>(OnVesselInteractUsing);
        SubscribeLocalEvent<AnomalyVesselComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<AnomalyVesselComponent, ResearchServerGetPointsPerSecondEvent>(OnVesselGetPointsPerSecond);
        SubscribeLocalEvent<AnomalyShutdownEvent>(OnVesselAnomalyShutdown);
    }

    private void OnExamined(EntityUid uid, AnomalyVesselComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        // Funky
        if (component.Confluence != null)
        {
            args.PushText(Loc.GetString("mage-confluence-vessel-assigned"));
            return;
        }

        args.PushText(component.Anomaly == null
            ? Loc.GetString("anomaly-vessel-component-not-assigned")
            : Loc.GetString("anomaly-vessel-component-assigned"));
    }

    private void OnVesselShutdown(EntityUid uid, AnomalyVesselComponent component, ComponentShutdown args)
    {
        if (component.Anomaly is { } anomaly && TryComp<AnomalyComponent>(anomaly, out var anomalyComp))
            anomalyComp.ConnectedVessel = null;

        // Funky
        if (component.Confluence is { } confluence && TryComp<ConfluenceVesselLinkComponent>(confluence, out var link))
            link.ConnectedVessel = null;
    }

    private void OnVesselMapInit(EntityUid uid, AnomalyVesselComponent component, MapInitEvent args)
    {
        UpdateVesselAppearance(uid,  component);
    }

    private void OnVesselInteractUsing(EntityUid uid, AnomalyVesselComponent component, InteractUsingEvent args)
    {
        // Funky: mutual exclusion with ley confluence link
        if (component.Anomaly != null || component.Confluence != null)
            return;

        if (!TryComp<AnomalyScannerComponent>(args.Used, out var scanner))
            return;

        if (scanner.ScannedAnomaly is { } anomaly
            && TryComp<AnomalyComponent>(anomaly, out var anomalyComponent)
            && anomalyComponent.ConnectedVessel == null)
        {
            component.Anomaly = scanner.ScannedAnomaly;
            anomalyComponent.ConnectedVessel = uid;
            _radiation.SetSourceEnabled(uid, true);
            UpdateVesselAppearance(uid, component);
            Popup.PopupEntity(Loc.GetString("anomaly-vessel-component-anomaly-assigned"), uid);
            return;
        }

        // Funky
        if (scanner.ScannedConfluence is not { } confluence
            || !TryComp<ConfluenceComponent>(confluence, out var confluenceComp)
            || !confluenceComp.Opened
            || !TryComp<ConfluenceVesselLinkComponent>(confluence, out var link)
            || link.ConnectedVessel != null)
        {
            return;
        }

        LinkConfluenceToVessel(uid, component, confluence, link);
        scanner.ScannedConfluence = null;
        Popup.PopupEntity(Loc.GetString("mage-confluence-vessel-linked"), uid);
    }

    // Funky
    /// <summary>
    /// Links an opened confluence to an empty vessel (integration tests; gameplay uses scanner interaction).
    /// </summary>
    public void SetVesselConfluenceForTesting(EntityUid vessel, EntityUid confluence)
    {
        if (!TryComp(vessel, out AnomalyVesselComponent? vesselComp)
            || vesselComp.Anomaly != null
            || vesselComp.Confluence != null)
        {
            return;
        }

        if (!TryComp<ConfluenceComponent>(confluence, out var conf) || !conf.Opened
            || !TryComp<ConfluenceVesselLinkComponent>(confluence, out var link)
            || link.ConnectedVessel != null)
        {
            return;
        }

        LinkConfluenceToVessel(vessel, vesselComp, confluence, link);
    }

    // Funky
    private void LinkConfluenceToVessel(EntityUid vessel, AnomalyVesselComponent vesselComp, EntityUid confluence,
        ConfluenceVesselLinkComponent link)
    {
        vesselComp.Confluence = confluence;
        link.ConnectedVessel = vessel;
        UpdateVesselAppearance(vessel, vesselComp);
    }

    private void OnVesselGetPointsPerSecond(EntityUid uid, AnomalyVesselComponent component, ref ResearchServerGetPointsPerSecondEvent args)
    {
        // Funky: ley confluence grants via ConfluenceVesselResearchSystem instead
        if (!this.IsPowered(uid, EntityManager) || component.Confluence != null)
            return;

        if (component.Anomaly is not { } anomaly)
            return;

        args.Points += (int) (GetAnomalyPointValue(anomaly) * component.PointMultiplier);
    }

    private void OnVesselAnomalyShutdown(ref AnomalyShutdownEvent args)
    {
        var query = EntityQueryEnumerator<AnomalyVesselComponent>();
        while (query.MoveNext(out var ent, out var component))
        {
            if (args.Anomaly != component.Anomaly)
                continue;

            component.Anomaly = null;
            UpdateVesselAppearance(ent,  component);
            _radiation.SetSourceEnabled(ent, false);

            if (!args.Supercritical)
                continue;
            _explosion.TriggerExplosive(ent);
        }
    }

    private void OnVesselAnomalyStabilityChanged(ref AnomalyStabilityChangedEvent args)
    {
        var query = EntityQueryEnumerator<AnomalyVesselComponent>();
        while (query.MoveNext(out var ent, out var component))
        {
            if (args.Anomaly != component.Anomaly)
                continue;

            UpdateVesselAppearance(ent,  component);
        }
    }

    /// <summary>
    /// Updates the appearance of an anomaly vessel
    /// based on whether or not it has an anomaly
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    public void UpdateVesselAppearance(EntityUid uid, AnomalyVesselComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        // Funky
        var on = component.Anomaly != null || component.Confluence != null;

        if (!TryComp<AppearanceComponent>(uid, out var appearanceComponent))
            return;

        Appearance.SetData(uid, AnomalyVesselVisuals.HasAnomaly, on, appearanceComponent);
        if (_pointLight.TryGetLight(uid, out var pointLightComponent))
            _pointLight.SetEnabled(uid, on, pointLightComponent);

        if (component.Anomaly == null || !TryGetStabilityVisual(component.Anomaly.Value, out var visual))
            visual = AnomalyStabilityVisuals.Stable;

        Appearance.SetData(uid, AnomalyVesselVisuals.AnomalySeverity, visual, appearanceComponent);

        _ambient.SetAmbience(uid, on);
    }

    private void UpdateVessels()
    {
        var query = EntityQueryEnumerator<AnomalyVesselComponent>();
        while (query.MoveNext(out var vesselEnt, out var vessel))
        {
            if (vessel.Anomaly is not { } anomUid)
                continue;

            if (!TryComp<AnomalyComponent>(anomUid, out var anomaly))
                continue;

            if (Timing.CurTime < vessel.NextBeep)
                continue;

            // a lerp between the max and min values for each threshold.
            // longer beeps that get shorter as the anomaly gets more extreme
            float timerPercentage;
            if (anomaly.Stability <= anomaly.DecayThreshold)
                timerPercentage = (anomaly.DecayThreshold - anomaly.Stability) / anomaly.DecayThreshold;
            else if (anomaly.Stability >= anomaly.GrowthThreshold)
                timerPercentage = (anomaly.Stability - anomaly.GrowthThreshold) / (1 - anomaly.GrowthThreshold);
            else //it's not unstable
                continue;

            Audio.PlayPvs(vessel.BeepSound, vesselEnt);
            var beepInterval = (vessel.MaxBeepInterval - vessel.MinBeepInterval) * (1 - timerPercentage) + vessel.MinBeepInterval;
            vessel.NextBeep = beepInterval + Timing.CurTime;
        }
    }
}
