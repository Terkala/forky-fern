using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Medical.Cybernetics.Modules;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;

namespace Content.Shared.Medical.Cybernetics;

/// <summary>
/// Shared system that applies speed modifiers from cyber-leg speed modules.
/// </summary>
public abstract class CyberLimbMovementSystem : EntitySystem
{
    [Dependency] protected readonly SharedBodyPartSystem BodyPartSystem = default!;
    [Dependency] protected readonly MovementSpeedModifierSystem MovementSpeedModifierSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<CyberLimbStatsComponent, GetStandUpTimeEvent>(OnGetStandUpTime);
    }

    /// <summary>
    /// Applies speed modifiers from cyber-leg speed modules.
    /// </summary>
    private void OnRefreshMovementSpeed(Entity<BodyComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        // Check if entity has cyber-limb stats (indicates cyber-limbs present)
        if (!TryComp<CyberLimbStatsComponent>(ent, out var stats))
            return;

        // Apply efficiency penalty if battery depleted
        if (stats.CurrentBatteryCharge <= 0)
        {
            args.ModifySpeed(0.5f, 0.5f);
        }

        // Scan all body parts for cyber-legs with speed modules
        float walkMultiplier = 1.0f;
        float sprintMultiplier = 1.0f;

        foreach (var (partId, _) in BodyPartSystem.GetBodyChildren(ent, ent.Comp))
        {
            // Only check legs
            if (!HasComp<CyberLimbComponent>(partId))
                continue;

            if (!TryComp<BodyPartComponent>(partId, out var bodyPart))
                continue;

            if (bodyPart.PartType != BodyPartType.Leg)
                continue;

            // Get modules from storage
            var modules = GetCyberLimbModules(partId);
            foreach (var module in modules)
            {
                if (!TryComp<SpeedModuleComponent>(module, out var speedModule))
                    continue;

                // Accumulate speed multipliers
                walkMultiplier *= speedModule.WalkSpeedMultiplier;
                sprintMultiplier *= speedModule.SprintSpeedMultiplier;
            }
        }

        // Apply speed modifiers
        if (walkMultiplier != 1.0f || sprintMultiplier != 1.0f)
        {
            args.ModifySpeed(walkMultiplier, sprintMultiplier);
        }
    }

    /// <summary>
    /// Reduces stand-up time based on speed modules in cyber-legs.
    /// </summary>
    private void OnGetStandUpTime(Entity<CyberLimbStatsComponent> ent, ref GetStandUpTimeEvent args)
    {
        if (!TryComp<BodyComponent>(ent, out var body))
            return;

        // Scan cyber-legs for speed modules
        foreach (var (partId, _) in BodyPartSystem.GetBodyChildren(ent, body))
        {
            if (!HasComp<CyberLimbComponent>(partId))
                continue;

            if (!TryComp<BodyPartComponent>(partId, out var bodyPart))
                continue;

            if (bodyPart.PartType != BodyPartType.Leg)
                continue;

            // Get modules from storage
            var modules = GetCyberLimbModules(partId);
            foreach (var module in modules)
            {
                if (!TryComp<SpeedModuleComponent>(module, out var speedModule))
                    continue;

                // Reduce stand-up time
                args.DoAfterTime = TimeSpan.FromSeconds(args.DoAfterTime.TotalSeconds * speedModule.StandUpSpeedMultiplier);
            }
        }
    }

    /// <summary>
    /// Gets all module entities from a cyber-limb's storage container.
    /// </summary>
    protected abstract List<EntityUid> GetCyberLimbModules(EntityUid cyberLimb);
}
