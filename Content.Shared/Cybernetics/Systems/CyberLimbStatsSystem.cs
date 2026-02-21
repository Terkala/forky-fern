using System.Linq;
using Content.Shared.Body;
using Robust.Shared.Network;
using Content.Shared.Body.Components;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Events;
using Robust.Shared.Timing;

namespace Content.Shared.Cybernetics.Systems;

public sealed class CyberLimbStatsSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;

    private const float UpdateInterval = 1f;
    private TimeSpan _nextUpdate = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, CyberLimbAttachedToBodyEvent>(OnCyberLimbAttached);
        SubscribeLocalEvent<BodyComponent, CyberLimbDetachedFromBodyEvent>(OnCyberLimbDetached);
        SubscribeLocalEvent<BodyComponent, CyberMaintenanceStateChangedEvent>(OnMaintenanceStateChanged);

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(UpdateInterval);
    }

    private void OnCyberLimbAttached(Entity<BodyComponent> ent, ref CyberLimbAttachedToBodyEvent args)
    {
        var body = args.Body;
        var cyberCount = _body.GetAllOrgans(body).Count(o => HasComp<CyberLimbComponent>(o));

        if (TryComp<CyberLimbStatsComponent>(body, out var existingStats))
        {
            existingStats.ServiceTimeMax = existingStats.ServiceTimePerLimb * cyberCount;
            existingStats.ServiceTimeRemaining += existingStats.ServiceTimePerLimb;
            Dirty(body, existingStats);
        }
        else
        {
            var stats = EnsureComp<CyberLimbStatsComponent>(body);
            stats.ServiceTimeMax = stats.ServiceTimePerLimb * cyberCount;
            stats.ServiceTimeRemaining = stats.ServiceTimeMax;
            Dirty(body, stats);
        }
    }

    private void OnCyberLimbDetached(Entity<BodyComponent> ent, ref CyberLimbDetachedFromBodyEvent args)
    {
        var body = args.Body;
        var cyberCount = _body.GetAllOrgans(body).Count(o => HasComp<CyberLimbComponent>(o));

        if (cyberCount == 0)
        {
            RemComp<CyberLimbStatsComponent>(body);
        }
        else if (TryComp<CyberLimbStatsComponent>(body, out var stats))
        {
            stats.ServiceTimeMax = stats.ServiceTimePerLimb * cyberCount;
            stats.ServiceTimeRemaining = TimeSpan.FromTicks(Math.Min(stats.ServiceTimeRemaining.Ticks, stats.ServiceTimeMax.Ticks));
            Dirty(body, stats);
        }
    }

    private void OnMaintenanceStateChanged(Entity<BodyComponent> ent, ref CyberMaintenanceStateChangedEvent args)
    {
        var body = ent.Owner;

        if (args.RepairCompleted && TryComp<CyberLimbStatsComponent>(body, out var stats))
        {
            stats.ServiceTimeRemaining = stats.ServiceTimeMax;
            stats.Efficiency = 1f;
            Dirty(body, stats);
        }
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
            stats.ServiceTimeRemaining -= TimeSpan.FromSeconds(1);
            if (stats.ServiceTimeRemaining < TimeSpan.Zero)
                stats.ServiceTimeRemaining = TimeSpan.Zero;

            stats.Efficiency = stats.ServiceTimeRemaining <= TimeSpan.Zero ? 0.5f : 1f;
            Dirty(uid, stats);
        }
    }
}
