using Content.Shared.Medical.Cybernetics;
using Content.Shared.Medical.Cybernetics.Modules;
using Content.Shared.Storage;
using Content.Shared.Stacks;
using Robust.Shared.Containers;

namespace Content.Server.Medical.Cybernetics;

/// <summary>
/// Server-side system that handles cyber-limb module installation/removal and stack splitting.
/// </summary>
public sealed class CyberLimbStorageSystem : Shared.Cybernetics.CyberLimbStorageSystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberLimbComponent, EntInsertedIntoContainerMessage>(OnModuleInserted);
        SubscribeLocalEvent<CyberLimbComponent, EntRemovedFromContainerMessage>(OnModuleRemoved);
    }

    /// <summary>
    /// Validates inserted items are valid modules, handles stack splitting, and raises installation event.
    /// </summary>
    private void OnModuleInserted(Entity<CyberLimbComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != StorageComponent.ContainerId)
            return;

        var module = args.Entity;

        // Handle stack splitting: if a stack with count > 1 is inserted, split it
        if (TryComp<StackComponent>(module, out var stack) && stack.Count > 1)
        {
            // Remove the stack from container
            _container.Remove(module, args.Container);

            // Split: take 1 from the stack
            var splitEntity = _stack.Split((module, stack), 1, Transform(module).Coordinates);
            if (splitEntity != null)
            {
                // Insert the split entity (single item)
                _container.Insert(splitEntity.Value, args.Container);
            }

            // The original stack remains outside with reduced count
            return;
        }

        // Validate that the inserted item is a valid module
        if (!HasAnyModuleComponent(module))
        {
            // Not a valid module, remove it
            _container.Remove(module, args.Container);
            return;
        }

        // Raise module installed event
        var ev = new CyberLimbModuleInstalledEvent(module, ent);
        RaiseLocalEvent(ent, ref ev);
    }

    /// <summary>
    /// Raises module removal event when modules are removed.
    /// </summary>
    private void OnModuleRemoved(Entity<CyberLimbComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != StorageComponent.ContainerId)
            return;

        var module = args.Entity;

        // Raise module removed event
        var ev = new CyberLimbModuleRemovedEvent(module, ent);
        RaiseLocalEvent(ent, ref ev);
    }

    /// <summary>
    /// Checks if an entity has any module component.
    /// </summary>
    private bool HasAnyModuleComponent(EntityUid uid)
    {
        return HasComp<BatteryModuleComponent>(uid)
               || HasComp<MatterBinModuleComponent>(uid)
               || HasComp<ManipulatorModuleComponent>(uid)
               || HasComp<CapacitorModuleComponent>(uid);
    }
}
