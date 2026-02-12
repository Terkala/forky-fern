using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Containers;
using Content.Shared.Medical.Cybernetics.Modules;
using Content.Shared.Medical.Integrity;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Reflection;

namespace Content.Shared.Medical.Cybernetics;

/// <summary>
/// Shared system that calculates aggregate stats across all cyber-limbs on a body.
/// Handles stat recalculation when body parts are attached/detached or modules are installed/removed.
/// </summary>
[Reflect(false)]
public abstract class CyberLimbStatsSystem : EntitySystem
{
    [Dependency] protected readonly SharedBodyPartSystem _bodyPartSystem = default!;
    [Dependency] protected readonly SharedContainerSystem _containerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Subscribe to cyber-limb specific events raised by BodySystem
        // These events are raised when a cyber-limb is attached/detached via any method (surgery, direct attachment, etc.)
        SubscribeLocalEvent<BodyComponent, CyberLimbAttachedEvent>(OnCyberLimbAttached);
        SubscribeLocalEvent<BodyComponent, CyberLimbDetachedEvent>(OnCyberLimbDetached);
        SubscribeLocalEvent<CyberLimbComponent, CyberLimbModuleInstalledEvent>(OnModuleInstalled);
        SubscribeLocalEvent<CyberLimbComponent, CyberLimbModuleRemovedEvent>(OnModuleRemoved);
        SubscribeLocalEvent<IntegrityComponent, IntegrityUsageChangedEvent>(OnIntegrityUsageChanged);
        SubscribeLocalEvent<ServiceTimeResetComponent, ComponentAdd>(OnServiceTimeReset);
        SubscribeLocalEvent<IonDamageRepairedComponent, ComponentAdd>(OnIonDamageRepaired);
    }

    /// <summary>
    /// Handles cyber-limb attachment and recalculates stats when a cyber-limb is attached to a body.
    /// </summary>
    private void OnCyberLimbAttached(Entity<BodyComponent> ent, ref CyberLimbAttachedEvent args)
    {
        RecalculateStats(ent);
    }

    /// <summary>
    /// Handles cyber-limb detachment and recalculates stats when a cyber-limb is detached from a body.
    /// </summary>
    private void OnCyberLimbDetached(Entity<BodyComponent> ent, ref CyberLimbDetachedEvent args)
    {
        RecalculateStats(ent);
    }

    /// <summary>
    /// Handles module installation and recalculates stats for the body.
    /// </summary>
    private void OnModuleInstalled(Entity<CyberLimbComponent> ent, ref CyberLimbModuleInstalledEvent args)
    {
        if (!TryComp<BodyPartComponent>(ent, out var bodyPart) || bodyPart.Body == null)
            return;

        var body = bodyPart.Body.Value;
        RecalculateStats(body);
        RaiseLocalEvent(body, new RefreshMovementSpeedModifiersEvent());
    }

    /// <summary>
    /// Handles module removal and recalculates stats for the body.
    /// </summary>
    private void OnModuleRemoved(Entity<CyberLimbComponent> ent, ref CyberLimbModuleRemovedEvent args)
    {
        if (!TryComp<BodyPartComponent>(ent, out var bodyPart) || bodyPart.Body == null)
            return;

        var body = bodyPart.Body.Value;
        RecalculateStats(body);
        RaiseLocalEvent(body, new RefreshMovementSpeedModifiersEvent());
    }

    /// <summary>
    /// Handles integrity usage changes to add/remove CyberLimbStatsComponent when cyber-limbs are added/removed.
    /// </summary>
    private void OnIntegrityUsageChanged(Entity<IntegrityComponent> ent, ref IntegrityUsageChangedEvent args)
    {
        // Check if there are any cyber-limbs on this body
        if (!TryComp<BodyComponent>(ent, out var body))
            return;

        bool hasCyberLimbs = false;
        foreach (var (partId, _) in _bodyPartSystem.GetBodyChildren(ent, body))
        {
            if (HasComp<CyberLimbComponent>(partId))
            {
                hasCyberLimbs = true;
                break;
            }
        }

        // Add component if cyber-limbs exist, remove if they don't
        if (hasCyberLimbs)
        {
            EnsureComp<CyberLimbStatsComponent>(ent);
            RecalculateStats(ent);
        }
        else
        {
            RemComp<CyberLimbStatsComponent>(ent);
        }
    }

    /// <summary>
    /// Recalculates aggregate stats for all cyber-limbs on a body.
    /// Sums battery capacity, service time, counts manipulators, and calculates efficiency.
    /// </summary>
    /// <param name="body">The body entity to recalculate stats for.</param>
    /// <param name="preserveExpiredServiceTime">If true, prevents refilling service time when it's zero (e.g., after ion storm expiration).</param>
    public void RecalculateStats(EntityUid body, bool preserveExpiredServiceTime = false)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return;

        // Ensure stats component exists
        if (!TryComp<CyberLimbStatsComponent>(body, out var stats))
        {
            // Check if there are any cyber-limbs first
            bool hasCyberLimbs = false;
            foreach (var (partId, _) in _bodyPartSystem.GetBodyChildren(body, bodyComp))
            {
                if (HasComp<CyberLimbComponent>(partId))
                {
                    hasCyberLimbs = true;
                    break;
                }
            }

            if (!hasCyberLimbs)
                return; // No cyber-limbs, no stats component needed

            stats = EnsureComp<CyberLimbStatsComponent>(body);
        }

        // Initialize sums
        float totalBatteryCapacity = 0f;
        float totalServiceTimeSeconds = 0f;
        int manipulatorCount = 0;
        int capacitorCount = 0;
        int specialModuleCount = 0;

        // Iterate through all body parts
        foreach (var (partId, _) in _bodyPartSystem.GetBodyChildren(body, bodyComp))
        {
            // Filter for cyber-limbs
            if (!HasComp<CyberLimbComponent>(partId))
                continue;

            // Get modules from storage container
            var modules = GetCyberLimbModules(partId);
            foreach (var module in modules)
            {
                // Sum battery capacity
                if (TryComp<BatteryModuleComponent>(module, out var battery))
                {
                    totalBatteryCapacity += battery.CapacityContribution;
                }

                // Sum service time (600 seconds per matter bin)
                if (TryComp<MatterBinModuleComponent>(module, out var matterBin))
                {
                    totalServiceTimeSeconds += matterBin.ServiceTimeContribution;
                }

                // Count manipulators
                if (HasComp<ManipulatorModuleComponent>(module))
                {
                    manipulatorCount++;
                }

                // Count capacitors
                if (HasComp<CapacitorModuleComponent>(module))
                {
                    capacitorCount++;
                }

                // Count special modules
                if (HasComp<SpecialModuleComponent>(module))
                {
                    specialModuleCount++;
                }
            }
        }

        // Calculate service time multiplier from capacitors (1.0 + count × 0.10)
        float serviceTimeMultiplier = 1.0f + (capacitorCount * 0.10f);
        totalServiceTimeSeconds *= serviceTimeMultiplier;

        // Calculate efficiency: 100% + (manipulatorCount - 1) × 10% (minimum 100% for first manipulator)
        float efficiency = 100f;
        if (manipulatorCount > 1)
        {
            efficiency = 100f + ((manipulatorCount - 1) * 10f);
        }

        // Preserve CurrentBatteryCharge (don't reset it on recalculation)
        // Only clamp if charge exceeds capacity (shouldn't happen, but safety check)
        if (stats.CurrentBatteryCharge > totalBatteryCapacity)
        {
            stats.CurrentBatteryCharge = totalBatteryCapacity;
        }

        // Preserve ServiceTimeRemaining if it's less than the new maximum
        // Clamp it to the new maximum if the maximum decreased
        var newMaxServiceTime = TimeSpan.FromSeconds(totalServiceTimeSeconds);
        if (stats.ServiceTimeRemaining > newMaxServiceTime)
        {
            stats.ServiceTimeRemaining = newMaxServiceTime;
        }
        // If ServiceTimeRemaining is zero or uninitialized, set it to the new maximum
        // This handles the case where the component is first created
        // However, if preserveExpiredServiceTime is true, don't refill when zero (e.g., after ion storm expiration)
        else if (stats.ServiceTimeRemaining == TimeSpan.Zero && totalServiceTimeSeconds > 0 && !preserveExpiredServiceTime)
        {
            stats.ServiceTimeRemaining = newMaxServiceTime;
        }

        // Apply 50% efficiency penalty if battery depleted OR service time expired
        bool batteryDepleted = stats.CurrentBatteryCharge <= 0;
        bool serviceTimeExpired = stats.ServiceTimeRemaining <= TimeSpan.Zero;
        if (batteryDepleted || serviceTimeExpired)
        {
            efficiency *= 0.5f;
        }

        // Update stats component
        stats.BatteryCapacity = totalBatteryCapacity;
        stats.Efficiency = efficiency;
        stats.SpecialModuleCount = specialModuleCount;

        // Mark component as dirty
        Dirty(body, stats);
    }

    /// <summary>
    /// Handles service time reset when wiring is replaced during maintenance.
    /// Resets service time to maximum for all cyber-limbs on the body.
    /// </summary>
    private void OnServiceTimeReset(EntityUid uid, ServiceTimeResetComponent component, ComponentAdd args)
    {
        // Get the body that this body part belongs to
        if (!TryComp<BodyPartComponent>(uid, out var bodyPart) || bodyPart.Body == null)
        {
            // Remove the marker component if we can't process it
            RemComp<ServiceTimeResetComponent>(uid);
            return;
        }

        var body = bodyPart.Body.Value;

        // Recalculate stats to get the maximum service time
        RecalculateStats(body);

        // Get the stats component and reset service time to maximum
        if (TryComp<CyberLimbStatsComponent>(body, out var stats))
        {
            // Calculate maximum service time from all cyber-limbs
            if (!TryComp<BodyComponent>(body, out var bodyComp))
            {
                RemComp<ServiceTimeResetComponent>(uid);
                return;
            }

            float totalServiceTimeSeconds = 0f;
            int capacitorCount = 0;

            // Iterate through all body parts to calculate maximum service time
            foreach (var (partId, _) in _bodyPartSystem.GetBodyChildren(body, bodyComp))
            {
                if (!HasComp<CyberLimbComponent>(partId))
                    continue;

                var modules = GetCyberLimbModules(partId);
                foreach (var module in modules)
                {
                    if (TryComp<MatterBinModuleComponent>(module, out var matterBin))
                    {
                        totalServiceTimeSeconds += matterBin.ServiceTimeContribution;
                    }

                    if (HasComp<CapacitorModuleComponent>(module))
                    {
                        capacitorCount++;
                    }
                }
            }

            // Apply capacitor multiplier
            float serviceTimeMultiplier = 1.0f + (capacitorCount * 0.10f);
            totalServiceTimeSeconds *= serviceTimeMultiplier;

            // Set service time to maximum
            stats.ServiceTimeRemaining = TimeSpan.FromSeconds(totalServiceTimeSeconds);
            Dirty(body, stats);

            // Update next service time expiration if needed
            if (TryComp<IntegrityComponent>(body, out var integrity))
            {
                UpdateServiceTimeExpiration(body);
            }
        }

        // Remove the marker component
        RemComp<ServiceTimeResetComponent>(uid);
    }

    /// <summary>
    /// Updates the next service time expiration on IntegrityComponent.
    /// Called when service time is reset or expires.
    /// </summary>
    protected virtual void UpdateServiceTimeExpiration(EntityUid body)
    {
        // Override in server implementation if needed
    }

    /// <summary>
    /// Handles ion damage repair when wiring is replaced during maintenance.
    /// Removes IonDamagedComponent from all cyber-limbs on the body.
    /// </summary>
    private void OnIonDamageRepaired(EntityUid uid, IonDamageRepairedComponent component, ComponentAdd args)
    {
        // Get the body that this body part belongs to
        if (!TryComp<BodyPartComponent>(uid, out var bodyPart) || bodyPart.Body == null)
        {
            // Remove the marker component if we can't process it
            RemComp<IonDamageRepairedComponent>(uid);
            return;
        }

        var body = bodyPart.Body.Value;

        // Get the body component
        if (!TryComp<BodyComponent>(body, out var bodyComp))
        {
            RemComp<IonDamageRepairedComponent>(uid);
            return;
        }

        // Iterate through all body parts and remove IonDamagedComponent from cyber-limbs
        bool anyRepaired = false;
        foreach (var (partId, _) in _bodyPartSystem.GetBodyChildren(body, bodyComp))
        {
            if (!HasComp<CyberLimbComponent>(partId))
                continue;

            if (HasComp<IonDamagedComponent>(partId))
            {
                RemComp<IonDamagedComponent>(partId);
                anyRepaired = true;
            }
        }

        // Trigger integrity recalculation if any damage was repaired
        if (anyRepaired)
        {
            TriggerIntegrityRecalculation(body);
        }

        // Remove the marker component
        RemComp<IonDamageRepairedComponent>(uid);
    }

    /// <summary>
    /// Triggers integrity recalculation on the body.
    /// Override in server implementation to call SharedIntegritySystem.
    /// </summary>
    protected virtual void TriggerIntegrityRecalculation(EntityUid body)
    {
        // Override in server implementation if needed
    }

    /// <summary>
    /// Gets all module entities from a cyber-limb's storage container.
    /// </summary>
    private List<EntityUid> GetCyberLimbModules(EntityUid cyberLimb)
    {
        var modules = new List<EntityUid>();

        if (!TryComp<StorageComponent>(cyberLimb, out var storage))
            return modules;

        // Use the StorageComponent's Container property directly
        if (storage.Container == null)
            return modules;

        // Iterate through all entities in the container
        foreach (var entity in storage.Container.ContainedEntities)
        {
            modules.Add(entity);
        }

        return modules;
    }
}
