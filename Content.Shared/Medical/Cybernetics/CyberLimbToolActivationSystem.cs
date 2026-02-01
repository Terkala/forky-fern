using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Medical.Cybernetics.Modules;
using Robust.Shared.Timing;

namespace Content.Shared.Medical.Cybernetics;

/// <summary>
/// Shared system that handles cyber-limb tool activation when using empty hands.
/// </summary>
public abstract class CyberLimbToolActivationSystem : EntitySystem
{
    [Dependency] protected readonly SharedBodyPartSystem BodyPartSystem = default!;
    [Dependency] protected readonly SharedHandsSystem HandsSystem = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, GetUsedEntityEvent>(OnGetUsedEntity);
    }

    /// <summary>
    /// Handles GetUsedEntityEvent to activate cyber-tools when hands are empty.
    /// This event is raised on the user to determine what entity they're using.
    /// When the hand is empty, Used will be null, allowing us to intercept and activate cyber-tools.
    /// </summary>
    private void OnGetUsedEntity(Entity<BodyComponent> ent, ref GetUsedEntityEvent args)
    {
        // Only handle if no entity is being used (empty hand)
        if (args.Used != null)
            return;

        // Check if user has empty active hand
        if (!HandsSystem.ActiveHandIsEmpty((ent, null)))
            return;

        // Scan all body parts for cyber-limbs with tool modules
        foreach (var (partId, _) in BodyPartSystem.GetBodyChildren(ent, ent.Comp))
        {
            if (!HasComp<CyberLimbComponent>(partId))
                continue;

            // Get modules from storage
            var modules = GetCyberLimbModules(partId);
            foreach (var module in modules)
            {
                if (!TryComp<SpecialModuleComponent>(module, out var specialModule))
                    continue;

                // Only activate tool modules
                if (specialModule.ModuleType != SpecialModuleType.Tool)
                    continue;

                // Check cooldown
                if (Timing.CurTime < specialModule.LastActivation + specialModule.ActivationCooldown)
                    continue;

                // Raise activation event
                var ev = new CyberLimbToolActivatedEvent(module, ent, specialModule);
                RaiseLocalEvent(ent, ref ev);

                if (ev.Handled)
                {
                    // Set Used to a temporary marker entity to indicate we're handling this interaction
                    // This prevents the default empty-hand interaction from firing
                    // We use the user entity itself as a marker since we've already handled the activation
                    args.Used = ent;
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Gets all module entities from a cyber-limb's storage container.
    /// </summary>
    protected abstract List<EntityUid> GetCyberLimbModules(EntityUid cyberLimb);
}

/// <summary>
/// Event raised when a cyber-tool module is activated.
/// </summary>
[ByRefEvent]
public record struct CyberLimbToolActivatedEvent(EntityUid Module, EntityUid User, SpecialModuleComponent SpecialModule)
{
    public bool Handled = false;
}
