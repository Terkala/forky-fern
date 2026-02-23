using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.Cybernetics.Systems;

public sealed class CyberLimbStatsSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly CyberLimbModuleSystem _moduleSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly INetManager _net = default!;

    private const float UpdateInterval = 1f;
    private TimeSpan _nextUpdate = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, CyberLimbAttachedToBodyEvent>(OnCyberLimbAttached);
        SubscribeLocalEvent<BodyComponent, CyberLimbDetachedFromBodyEvent>(OnCyberLimbDetached);
        SubscribeLocalEvent<BodyComponent, CyberMaintenanceStateChangedEvent>(OnMaintenanceStateChanged);
        SubscribeLocalEvent<BodyComponent, CyberLimbStatsRecalcEvent>(OnStatsRecalc);
        SubscribeLocalEvent<CyberLimbStatsComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(UpdateInterval);
    }

    private void OnCyberLimbAttached(Entity<BodyComponent> ent, ref CyberLimbAttachedToBodyEvent args)
    {
        var body = args.Body;
        var limb = args.Limb;

        if (TryComp<CyberLimbStatsComponent>(body, out var existingStats))
        {
            existingStats.BaseServiceRemaining += existingStats.BaseServiceTimePerLimb;
            FillMatterBinsInLimb(limb);
        }
        else
        {
            var stats = EnsureComp<CyberLimbStatsComponent>(body);
            stats.BaseServiceRemaining = stats.BaseServiceTimePerLimb;
            FillMatterBinsInLimb(limb);
        }

        RecomputeAndRefresh(body);
    }

    private void OnCyberLimbDetached(Entity<BodyComponent> ent, ref CyberLimbDetachedFromBodyEvent args)
    {
        var body = args.Body;
        var cyberCount = _body.GetAllOrgans(body).Count(o => HasComp<CyberLimbComponent>(o));

        if (cyberCount == 0)
        {
            RemComp<CyberLimbStatsComponent>(body);
            _movementSpeed.RefreshMovementSpeedModifiers(body);
            return;
        }

        if (TryComp<CyberLimbStatsComponent>(body, out var stats))
        {
            stats.BaseServiceRemaining = stats.BaseServiceTimePerLimb * cyberCount;
            RecomputeAndRefresh(body);
        }
    }

    private void OnMaintenanceStateChanged(Entity<BodyComponent> ent, ref CyberMaintenanceStateChangedEvent args)
    {
        var body = ent.Owner;

        if (!args.RepairCompleted || !TryComp<CyberLimbStatsComponent>(body, out var stats))
            return;

        var cyberCount = _body.GetAllOrgans(body).Count(o => HasComp<CyberLimbComponent>(o));
        stats.BaseServiceRemaining = stats.BaseServiceTimePerLimb * cyberCount;

        foreach (var organ in _body.GetAllOrgans(body))
        {
            if (!HasComp<CyberLimbComponent>(organ))
                continue;
            FillMatterBinsInLimb(organ);
        }

        var (_, manipulatorCount, _) = _moduleSystem.GetModuleCounts(body);
        stats.Efficiency = _moduleSystem.GetLimbEfficiencyFromManipulators(manipulatorCount);

        RecomputeAndRefresh(body);
    }

    private void OnStatsRecalc(Entity<BodyComponent> ent, ref CyberLimbStatsRecalcEvent args)
    {
        if (args.Body != ent.Owner)
            return;

        if (!HasComp<CyberLimbStatsComponent>(ent.Owner))
            return;

        RecomputeAndRefresh(ent.Owner);
    }

    private void OnRefreshMovementSpeed(Entity<CyberLimbStatsComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.Efficiency);
    }

    private void FillMatterBinsInLimb(EntityUid limb)
    {
        if (!TryComp<StorageComponent>(limb, out var storage) || storage.Container == null)
            return;

        foreach (var item in storage.Container.ContainedEntities)
        {
            if (TryComp<CyberLimbMatterBinComponent>(item, out var matterBin))
            {
                var count = TryComp<StackComponent>(item, out var stack) ? stack.Count : 1;
                matterBin.ServiceRemaining = TimeSpan.FromTicks(matterBin.ServiceTime.Ticks * count);
                Dirty(item, matterBin);
            }
        }
    }

    public void RecomputeAndRefresh(EntityUid body)
    {
        if (!TryComp<CyberLimbStatsComponent>(body, out var stats))
            return;

        stats.ServiceTimeMax = _moduleSystem.GetTotalServiceMax(body);
        var totalRemaining = _moduleSystem.GetTotalServiceRemaining(body);
        stats.ServiceTimeRemaining = TimeSpan.FromTicks(Math.Min(totalRemaining.Ticks, stats.ServiceTimeMax.Ticks));

        var (_, manipulatorCount, _) = _moduleSystem.GetModuleCounts(body);
        var limbEfficiency = _moduleSystem.GetLimbEfficiencyFromManipulators(manipulatorCount);
        var depleted = totalRemaining <= TimeSpan.Zero;
        var newEfficiency = limbEfficiency * (depleted ? 0.5f : 1f);
        stats.Efficiency = newEfficiency;

        Dirty(body, stats);
        _movementSpeed.RefreshMovementSpeedModifiers(body);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(UpdateInterval);

        var query = EntityQueryEnumerator<CyberLimbStatsComponent>();
        while (query.MoveNext(out var uid, out var stats))
        {
            var drainRemaining = TimeSpan.FromSeconds(1);

            if (stats.BaseServiceRemaining >= drainRemaining)
            {
                stats.BaseServiceRemaining -= drainRemaining;
                drainRemaining = TimeSpan.Zero;
            }
            else
            {
                drainRemaining -= stats.BaseServiceRemaining;
                stats.BaseServiceRemaining = TimeSpan.Zero;
            }

            if (drainRemaining > TimeSpan.Zero)
            {
                var (matterBins, _, _) = _moduleSystem.GetModuleCounts(uid);
                foreach (var mb in matterBins)
                {
                    if (drainRemaining <= TimeSpan.Zero)
                        break;

                    var comp = Comp<CyberLimbMatterBinComponent>(mb);
                    if (comp.ServiceRemaining >= drainRemaining)
                    {
                        comp.ServiceRemaining -= drainRemaining;
                        drainRemaining = TimeSpan.Zero;
                    }
                    else
                    {
                        drainRemaining -= comp.ServiceRemaining;
                        comp.ServiceRemaining = TimeSpan.Zero;
                    }
                    Dirty(mb, comp);
                }
            }

            stats.ServiceTimeRemaining = _moduleSystem.GetTotalServiceRemaining(uid);
            if (stats.ServiceTimeRemaining < TimeSpan.Zero)
                stats.ServiceTimeRemaining = TimeSpan.Zero;

            var (_, manipulatorCount, _) = _moduleSystem.GetModuleCounts(uid);
            var limbEfficiency = _moduleSystem.GetLimbEfficiencyFromManipulators(manipulatorCount);
            var depleted = stats.ServiceTimeRemaining <= TimeSpan.Zero;
            var newEfficiency = limbEfficiency * (depleted ? 0.5f : 1f);

            if (stats.Efficiency != newEfficiency)
            {
                stats.Efficiency = newEfficiency;
                _movementSpeed.RefreshMovementSpeedModifiers(uid);
            }

            Dirty(uid, stats);
        }
    }
}
