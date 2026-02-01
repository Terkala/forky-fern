using Content.Shared.Medical.Cybernetics;
using Content.Shared.Medical.Integrity;
using Robust.Shared.Timing;

namespace Content.Server.Medical.Cybernetics;

/// <summary>
/// Server-side implementation of CyberLimbStatsSystem.
/// Handles battery drain and service time tracking with update loop.
/// </summary>
public sealed class CyberLimbStatsSystem : Content.Shared.Medical.Cybernetics.CyberLimbStatsSystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedIntegritySystem _integritySystem = default!;

    /// <summary>
    /// Update interval for battery drain and service time tracking (1 second).
    /// </summary>
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1.0);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberLimbStatsComponent, ComponentInit>(OnStatsComponentInit);
        SubscribeLocalEvent<CyberLimbStatsComponent, MapInitEvent>(OnStatsMapInit);
        SubscribeLocalEvent<CyberLimbStatsComponent, EntityUnpausedEvent>(OnStatsUnpaused);
    }

    /// <summary>
    /// Initializes LastUpdate timestamp on component creation.
    /// </summary>
    private void OnStatsComponentInit(Entity<CyberLimbStatsComponent> ent, ref ComponentInit args)
    {
        ent.Comp.LastUpdate = _gameTiming.CurTime;
        RecalculateStats(ent);
        
        // Initialize CurrentBatteryCharge to BatteryCapacity when component is created at runtime
        // This prevents immediate 50% efficiency penalty on newly created bodies
        // Fill when capacity transitions from 0 to >0 or when charge is 0 and capacity > 0
        if (ent.Comp.BatteryCapacity > 0 && ent.Comp.CurrentBatteryCharge <= 0)
        {
            ent.Comp.CurrentBatteryCharge = ent.Comp.BatteryCapacity;
        }
    }

    /// <summary>
    /// Initializes battery charge and service time on map initialization.
    /// </summary>
    private void OnStatsMapInit(Entity<CyberLimbStatsComponent> ent, ref MapInitEvent args)
    {
        // Set CurrentBatteryCharge to BatteryCapacity (start fully charged)
        ent.Comp.CurrentBatteryCharge = ent.Comp.BatteryCapacity;

        // ServiceTimeRemaining should already be set by RecalculateStats to the calculated maximum
        // But ensure it's set correctly if it wasn't
        if (ent.Comp.ServiceTimeRemaining == TimeSpan.Zero && ent.Comp.BatteryCapacity > 0)
        {
            // Recalculate to ensure ServiceTimeRemaining is set
            RecalculateStats(ent);
        }
    }

    /// <summary>
    /// Adjusts LastUpdate timestamp after entity is unpaused.
    /// </summary>
    private void OnStatsUnpaused(Entity<CyberLimbStatsComponent> ent, ref EntityUnpausedEvent args)
    {
        ent.Comp.LastUpdate += args.PausedTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateBatteryAndServiceTime();
    }

    /// <summary>
    /// Updates battery drain and service time tracking for all entities with CyberLimbStatsComponent.
    /// Runs every 1 second to drain battery and service time.
    /// </summary>
    private void UpdateBatteryAndServiceTime()
    {
        var query = EntityQueryEnumerator<CyberLimbStatsComponent, IntegrityComponent>();
        var curTime = _gameTiming.CurTime;

        while (query.MoveNext(out var uid, out var stats, out var integrity))
        {
            // Skip if not ready for update
            if (curTime < stats.LastUpdate + UpdateInterval)
                continue;

            // Calculate delta time
            var deltaTime = curTime - stats.LastUpdate;
            var deltaSeconds = deltaTime.TotalSeconds;

            // Update LastUpdate timestamp
            stats.LastUpdate = curTime;

            // Track previous states for efficiency recalculation
            bool wasBatteryDepleted = stats.CurrentBatteryCharge <= 0;
            bool wasServiceTimeExpired = stats.ServiceTimeRemaining <= TimeSpan.Zero;

            // Drain service time: reduce by delta time (always drain, even if no battery capacity)
            stats.ServiceTimeRemaining = TimeSpan.FromSeconds(Math.Max(0.0, stats.ServiceTimeRemaining.TotalSeconds - deltaSeconds));

            // Check if service time just expired (was > 0, now == 0)
            bool serviceTimeJustExpired = !wasServiceTimeExpired && stats.ServiceTimeRemaining <= TimeSpan.Zero;
            if (serviceTimeJustExpired)
            {
                UpdateServiceTimeExpiration(uid);
            }

            // Drain battery only if there is battery capacity
            if (stats.BatteryCapacity > 0)
            {
                // Drain battery: calculate drain rate based on battery capacity
                // 20 minutes for medium cell = 10000J, so drain = capacity / 1200 per second
                var batteryDrain = (float)(stats.BatteryCapacity / 1200.0 * deltaSeconds);
                stats.CurrentBatteryCharge = Math.Max(0f, stats.CurrentBatteryCharge - batteryDrain);
            }

            // Recalculate efficiency if battery or service time changed state (crossed zero threshold)
            bool isBatteryDepleted = stats.CurrentBatteryCharge <= 0;
            bool isServiceTimeExpired = stats.ServiceTimeRemaining <= TimeSpan.Zero;
            if (wasBatteryDepleted != isBatteryDepleted || wasServiceTimeExpired != isServiceTimeExpired)
            {
                RecalculateStats(uid);
            }

            // Mark component as dirty
            Dirty(uid, stats);
        }
    }

    /// <summary>
    /// Updates NextServiceTimeExpiration on IntegrityComponent when service time expires.
    /// Sets the expiration time to the current time since service time has just expired.
    /// </summary>
    protected override void UpdateServiceTimeExpiration(EntityUid body)
    {
        if (!TryComp<IntegrityComponent>(body, out var integrity))
            return;

        // Set NextServiceTimeExpirationTime to current time since service time just expired
        integrity.NextServiceTimeExpirationTime = _gameTiming.CurTime;
        
        // Set NextServiceTimeExpiration to null since service time has expired
        integrity.NextServiceTimeExpiration = null;

        // Trigger RecalculateTargetBioRejection to apply service-time expiry effects
        _integritySystem.RecalculateTargetBioRejection(body, integrity);
        Dirty(body, integrity);
    }
}
