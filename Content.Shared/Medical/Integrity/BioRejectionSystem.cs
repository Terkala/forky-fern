using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Integrity.Components;
using Content.Shared.Medical.Integrity.Events;
using Robust.Shared.Timing;

namespace Content.Shared.Medical.Integrity;

public sealed class BioRejectionSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float UpdateInterval = 1f;
    private TimeSpan _nextUpdate = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(UpdateInterval);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(UpdateInterval);

        const float RampRatePerNegativeIntegrity = 0.1f;
        const string BioRejectionDamageType = "BioRejection";

        var query = EntityQueryEnumerator<BodyComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var body, out var damageable))
        {
            var usage = TryComp<IntegrityUsageComponent>(uid, out var usageComp) ? usageComp.Usage : 0;
            var penaltyEv = new IntegrityPenaltyTotalRequestEvent(uid);
            RaiseLocalEvent(uid, ref penaltyEv);
            var penalty = penaltyEv.Total;
            var capacity = TryComp<IntegrityCapacityComponent>(uid, out var cap) ? cap.MaxIntegrity : 6;

            var excess = usage + penalty - capacity;
            var current = damageable.Damage.DamageDict.TryGetValue(BioRejectionDamageType, out var d)
                ? d.Float()
                : 0f;

            var target = excess > 0 ? excess : 0f;
            if (target == 0 && current == 0)
                continue;

            var stepSize = excess > 0
                ? RampRatePerNegativeIntegrity * excess
                : RampRatePerNegativeIntegrity * current;
            var delta = Math.Clamp(target - current, -stepSize, stepSize);

            if (Math.Abs(delta) < 0.001f)
                continue;

            var damage = new DamageSpecifier();
            damage.DamageDict[BioRejectionDamageType] = FixedPoint2.New(delta);
            _damageable.TryChangeDamage(new Entity<DamageableComponent?>(uid, damageable), damage, ignoreResistances: false, interruptsDoAfters: false, origin: null);
        }
    }
}
