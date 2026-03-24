using Content.Server.Atmos.EntitySystems;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared._CE.MagicEnergy.Systems;
using Content.Shared._Funkystation.Elementalism;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Power.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Server._Funkystation.Elementalism;

public sealed class ElementalFireBarrierSystem : EntitySystem
{
    [Dependency] private readonly MagicBatterySystem _magicBattery = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<(EntityUid Caster, EntityUid Other), TimeSpan> _contactCooldown = new();
    private readonly HashSet<EntityUid> _nearby = new();

    private const float DrainPerSecond = 0.5f;
    private const float ContactRange = 1.25f;
    private static readonly TimeSpan ContactInterval = TimeSpan.FromSeconds(2);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ToggleElementalFireBarrierEvent>(OnToggle);
        SubscribeLocalEvent<ElementalFireBarrierComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnToggle(ToggleElementalFireBarrierEvent ev)
    {
        var uid = ev.Target;

        if (!HasComp<MageOfAscensionComponent>(uid))
            return;

        var comp = EnsureComp<ElementalFireBarrierComponent>(uid);
        comp.Active = !comp.Active;

        if (!comp.Active)
            ClearContactCooldowns(uid);

        Dirty(uid, comp);
    }

    private void ClearContactCooldowns(EntityUid caster)
    {
        foreach (var key in _contactCooldown.Keys.ToArray())
        {
            if (key.Caster == caster)
                _contactCooldown.Remove(key);
        }
    }

    private void OnDamageModify(EntityUid uid, ElementalFireBarrierComponent barrier, DamageModifyEvent args)
    {
        if (!barrier.Active)
            return;

        if (args.Damage.DamageDict.TryGetValue("Heat", out var heat) && heat > 0)
        {
            args.Damage.DamageDict["Heat"] = 0;
            if (args.Damage.Empty)
                return;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ElementalFireBarrierComponent, TransformComponent, BatteryComponent>();
        while (query.MoveNext(out var uid, out var barrier, out var xform, out var battery))
        {
            if (!barrier.Active)
                continue;

            _magicBattery.ChangeMagicCharge((uid, battery), -DrainPerSecond * frameTime);
            if (battery.LastCharge <= 0.01f)
            {
                barrier.Active = false;
                ClearContactCooldowns(uid);
                Dirty(uid, barrier);
                continue;
            }

            if (TryComp<FlammableComponent>(uid, out var selfFlam))
                _flammable.SetFireStacks(uid, 0f, selfFlam);

            _nearby.Clear();
            _lookup.GetEntitiesInRange(xform.Coordinates, ContactRange, _nearby, LookupFlags.Uncontained);

            var now = _timing.CurTime;
            foreach (var other in _nearby)
            {
                if (other == uid || !Exists(other))
                    continue;

                if (!TryComp<FlammableComponent>(other, out var flammable))
                    continue;

                var key = (uid, other);
                if (_contactCooldown.TryGetValue(key, out var nextReady) && now < nextReady)
                    continue;

                var add = flammable.MaximumFireStacks * 0.5f;
                _flammable.AdjustFireStacks(other, add, flammable, ignite: true);
                _contactCooldown[key] = now + ContactInterval;
            }
        }
    }
}
