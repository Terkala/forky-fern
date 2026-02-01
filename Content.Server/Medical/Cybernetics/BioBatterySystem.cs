using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Medical.Cybernetics;
using Content.Shared.Medical.Cybernetics.Modules;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Storage;
using Robust.Shared.Timing;

namespace Content.Server.Medical.Cybernetics;

/// <summary>
/// Server-side system that handles Bio-Battery modules converting hunger to battery charge.
/// </summary>
public sealed class BioBatterySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedBodyPartSystem _bodyPartSystem = default!;
    [Dependency] private readonly HungerSystem _hungerSystem = default!;

    /// <summary>
    /// Update interval for bio-battery processing (1 second).
    /// </summary>
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1.0);

    private TimeSpan _nextUpdate = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        if (curTime < _nextUpdate)
            return;

        _nextUpdate = curTime + UpdateInterval;

        // Query all entities with cyber-limb stats and hunger
        var query = EntityQueryEnumerator<CyberLimbStatsComponent, HungerComponent, BodyComponent>();
        while (query.MoveNext(out var uid, out var stats, out var hunger, out var body))
        {
            // Skip if battery is full
            if (stats.CurrentBatteryCharge >= stats.BatteryCapacity)
                continue;

            // Scan cyber-limbs for Bio-Battery modules
            foreach (var (partId, _) in _bodyPartSystem.GetBodyChildren(uid, body))
            {
                if (!HasComp<CyberLimbComponent>(partId))
                    continue;

                // Get modules from storage
                var modules = GetCyberLimbModules(partId);
                foreach (var module in modules)
                {
                    if (!TryComp<BioBatteryModuleComponent>(module, out var bioBattery))
                        continue;

                    // Check hunger threshold
                    var currentThreshold = _hungerSystem.GetHungerThreshold(hunger);
                    if (currentThreshold <= bioBattery.MinimumHungerThreshold)
                        continue;

                    // Calculate drain (hunger units per second)
                    var hungerDrain = bioBattery.HungerDrainRate * (float)UpdateInterval.TotalSeconds;

                    // Apply hunger drain
                    _hungerSystem.ModifyHunger(uid, -hungerDrain, hunger);

                    // Calculate charge gain
                    var chargeGain = hungerDrain * bioBattery.ChargeRate;

                    // Add to battery (clamp to capacity)
                    stats.CurrentBatteryCharge = Math.Min(stats.CurrentBatteryCharge + chargeGain, stats.BatteryCapacity);
                    Dirty(uid, stats);

                    // Only process one bio-battery module per update
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Gets all module entities from a cyber-limb's storage container.
    /// </summary>
    private List<EntityUid> GetCyberLimbModules(EntityUid cyberLimb)
    {
        var modules = new List<EntityUid>();

        if (!TryComp<StorageComponent>(cyberLimb, out var storage))
            return modules;

        if (storage.Container == null)
            return modules;

        foreach (var entity in storage.Container.ContainedEntities)
        {
            modules.Add(entity);
        }

        return modules;
    }
}
