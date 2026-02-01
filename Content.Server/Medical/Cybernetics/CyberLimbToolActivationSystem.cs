using Content.Shared.Body.Components;
using Content.Shared.Containers;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Medical.Cybernetics;
using Content.Shared.Medical.Cybernetics.Modules;
using Content.Shared.Prying.Components;
using Content.Shared.Storage;
using Content.Shared.Tools.Components;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.Medical.Cybernetics;

/// <summary>
/// Server-side implementation that activates cyber-tools by adding temporary components to the user.
/// </summary>
public sealed class CyberLimbToolActivationSystem : Content.Shared.Medical.Cybernetics.CyberLimbToolActivationSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberLimbToolActivatedEvent>(OnToolActivated);
        SubscribeLocalEvent<ActiveCyberToolComponent, PriedEvent>(OnPryingComplete);
        SubscribeLocalEvent<ActiveCyberToolComponent, DidEquipHandEvent>(OnHandEquipped);
        SubscribeLocalEvent<ActiveCyberToolComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<ActiveCyberToolComponent, UseAttemptEvent>(OnUseAttempt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Cleanup active cyber-tools that have been active for more than 2 seconds
        var query = EntityQueryEnumerator<ActiveCyberToolComponent>();
        var curTime = _timing.CurTime;
        while (query.MoveNext(out var uid, out var activeTool))
        {
            if (curTime - activeTool.ActivationTime > TimeSpan.FromSeconds(2))
            {
                CleanupActiveTool(uid);
            }
        }
    }

    /// <summary>
    /// Handles cyber-tool activation by adding appropriate temporary components.
    /// </summary>
    private void OnToolActivated(ref CyberLimbToolActivatedEvent args)
    {
        var module = args.Module;
        var user = args.User;
        var specialModule = args.SpecialModule;

        // Prevent multiple active tools
        if (HasComp<ActiveCyberToolComponent>(user))
            return;

        // Check if already has a tool component (from previous activation)
        if (HasComp<PryingComponent>(user) || HasComp<ToolComponent>(user))
            return;

        // Activate based on module type
        if (TryComp<JawsOfLifeModuleComponent>(module, out var jawsModule))
        {
            // Add temporary PryingComponent
            var prying = EnsureComp<PryingComponent>(user);
            
            // Calculate final prying speed based on cyber-limb efficiency
            float finalPryingSpeed = jawsModule.PryingSpeed;
            if (TryComp<CyberLimbStatsComponent>(user, out var stats))
            {
                finalPryingSpeed = jawsModule.PryingSpeed * (stats.Efficiency / 100f);
            }
            
            prying.SpeedModifier = finalPryingSpeed;
            prying.PryPowered = jawsModule.PryPowered;
            prying.Enabled = true;
            Dirty(user, prying);

            // Track active tool
            var activeTool = EnsureComp<ActiveCyberToolComponent>(user);
            activeTool.ToolType = "JawsOfLife";
            activeTool.ActivationTime = _timing.CurTime;
            activeTool.SourceModule = module;
            Dirty(user, activeTool);

            // Update module cooldown
            specialModule.LastActivation = _timing.CurTime;
            Dirty(module, specialModule);

            args.Handled = true;
        }
        // Handle modules with SpecialModuleComponent where ModuleType is Tool and ToolQuality is Prying (e.g., CyberCrowbar)
        else if (specialModule.ModuleType == SpecialModuleType.Tool && specialModule.ToolQuality == "Prying")
        {
            // Add temporary PryingComponent (similar to Jaws of Life but with default crowbar values)
            var prying = EnsureComp<PryingComponent>(user);
            prying.SpeedModifier = 1.0f;
            prying.PryPowered = false;
            prying.Enabled = true;
            Dirty(user, prying);

            // Track active tool
            var activeTool = EnsureComp<ActiveCyberToolComponent>(user);
            activeTool.ToolType = "Crowbar";
            activeTool.ActivationTime = _timing.CurTime;
            activeTool.SourceModule = module;
            Dirty(user, activeTool);

            // Update module cooldown
            specialModule.LastActivation = _timing.CurTime;
            Dirty(module, specialModule);

            args.Handled = true;
        }
        // TODO: Add other tool types (screwdriver, wrench) as needed
    }

    /// <summary>
    /// Cleans up temporary tool components after prying is complete.
    /// </summary>
    private void OnPryingComplete(Entity<ActiveCyberToolComponent> ent, ref PriedEvent args)
    {
        CleanupActiveTool(ent);
    }

    /// <summary>
    /// Cleans up active tool when an item is equipped to a hand (hand is no longer empty).
    /// </summary>
    private void OnHandEquipped(Entity<ActiveCyberToolComponent> ent, ref DidEquipHandEvent args)
    {
        CleanupActiveTool(ent);
    }

    /// <summary>
    /// Cleans up active tool when an attack attempt is cancelled or fails.
    /// </summary>
    private void OnAttackAttempt(Entity<ActiveCyberToolComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Cancelled)
        {
            CleanupActiveTool(ent);
        }
    }

    /// <summary>
    /// Cleans up active tool when a use attempt is cancelled or fails.
    /// </summary>
    private void OnUseAttempt(Entity<ActiveCyberToolComponent> ent, ref UseAttemptEvent args)
    {
        if (args.Cancelled)
        {
            CleanupActiveTool(ent);
        }
    }

    /// <summary>
    /// Centralized cleanup method that removes temporary tool components and active tool tracking.
    /// </summary>
    private void CleanupActiveTool(EntityUid uid)
    {
        // Remove temporary tool components
        if (HasComp<PryingComponent>(uid))
        {
            RemComp<PryingComponent>(uid);
        }
        if (HasComp<ToolComponent>(uid))
        {
            RemComp<ToolComponent>(uid);
        }

        // Remove active tool tracking
        RemComp<ActiveCyberToolComponent>(uid);
    }

    /// <summary>
    /// Gets all module entities from a cyber-limb's storage container.
    /// </summary>
    protected override List<EntityUid> GetCyberLimbModules(EntityUid cyberLimb)
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
