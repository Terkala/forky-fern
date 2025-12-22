using System.Diagnostics.CodeAnalysis;
using Content.Server.Administration.Logs;
using Content.Shared._FarHorizons.Power.Generation.FissionGenerator;
using Content.Shared.Database;
using Content.Shared.DeviceLinking.Events;

namespace Content.Server._FarHorizons.Power.Generation.FissionGenerator;

public sealed partial class GasTurbineMonitorSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly TurbineSystem _turbineSystem = default!;

    private readonly float _threshold = 0.5f;
    private float _accumulator = 0f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GasTurbineMonitorComponent, NewLinkEvent>(OnNewLink);
        SubscribeLocalEvent<GasTurbineMonitorComponent, PortDisconnectedEvent>(OnPortDisconnected);

        SubscribeLocalEvent<GasTurbineMonitorComponent, TurbineChangeFlowRateMessage>(OnTurbineFlowRateChanged);
        SubscribeLocalEvent<GasTurbineMonitorComponent, TurbineChangeStatorLoadMessage>(OnTurbineStatorLoadChanged);
    }

    private void OnNewLink(EntityUid uid, GasTurbineMonitorComponent comp, ref NewLinkEvent args)
    {
        if (!HasComp<TurbineComponent>(args.Source))
            return;

        comp.turbine = GetNetEntity(args.Source);
        Dirty(uid, comp);
    }

    private void OnPortDisconnected(EntityUid uid, GasTurbineMonitorComponent comp, ref PortDisconnectedEvent args)
    {
        if (args.Port != comp.LinkingPort)
            return;

        comp.turbine = null;
        Dirty(uid, comp);
    }

    public bool TryGetTurbineComp(GasTurbineMonitorComponent turbineMonitor, [NotNullWhen(true)] out TurbineComponent? turbineComponent)
    {
        turbineComponent = null;
        if (!_entityManager.TryGetEntity(turbineMonitor.turbine, out var turbineUid) || turbineUid == null)
            return false;

        if (!_entityManager.TryGetComponent<TurbineComponent>(turbineUid, out var turbine))
            return false;

        turbineComponent = turbine;
        return true;
    }

    #region BUI
    public override void Update(float frameTime)
    {
        _accumulator += frameTime;
        if (_accumulator > _threshold)
        {
            AccUpdate();
            _accumulator = 0;
        }
    }

    private void AccUpdate()
    {
        var query = EntityQueryEnumerator<GasTurbineMonitorComponent>();

        while (query.MoveNext(out var uid, out var turbineMonitor))
        {
            if (!TryGetTurbineComp(turbineMonitor, out var turbine))
                continue;

            _turbineSystem.UpdateUI(uid, turbine);
        }
    }

    private void OnTurbineFlowRateChanged(EntityUid uid, GasTurbineMonitorComponent comp, TurbineChangeFlowRateMessage args)
    {
        if (!TryGetTurbineComp(comp, out var turbine) || !_entityManager.TryGetEntity(comp.turbine, out var turbineUid))
            return;

        turbine.FlowRate = Math.Clamp(args.FlowRate, 0f, turbine.FlowRateMax);
        Dirty(turbineUid.Value, turbine);
        _turbineSystem.UpdateUI(uid, turbine);
        _adminLog.Add(LogType.AtmosVolumeChanged, LogImpact.Medium,
            $"{ToPrettyString(args.Actor):player} set the flow rate on {ToPrettyString(uid):device} to {args.FlowRate} through {ToPrettyString(uid):monitor}");
    }

    private void OnTurbineStatorLoadChanged(EntityUid uid, GasTurbineMonitorComponent comp, TurbineChangeStatorLoadMessage args)
    {
        if (!TryGetTurbineComp(comp, out var turbine) || !_entityManager.TryGetEntity(comp.turbine, out var turbineUid))
            return;

        turbine.StatorLoad = Math.Clamp(args.StatorLoad, 1000f, turbine.StatorLoadMax);
        Dirty(turbineUid.Value, turbine);
        _turbineSystem.UpdateUI(uid, turbine);
        _adminLog.Add(LogType.AtmosDeviceSetting, LogImpact.Medium,
            $"{ToPrettyString(args.Actor):player} set the stator load on {ToPrettyString(uid):device} to {args.StatorLoad} through {ToPrettyString(uid):monitor}");
    }
    #endregion
}
