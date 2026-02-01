using Content.Shared.Medical.Cybernetics;
using Content.Shared.Storage;
using Robust.Shared.Containers;

namespace Content.Server.Medical.Cybernetics;

/// <summary>
/// Server-side implementation of CyberLimbMovementSystem.
/// </summary>
public sealed class CyberLimbMovementSystem : Content.Shared.Medical.Cybernetics.CyberLimbMovementSystem
{
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;

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
