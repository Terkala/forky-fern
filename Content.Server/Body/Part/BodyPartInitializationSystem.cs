using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Server.Body.Part;

/// <summary>
/// System that initializes body parts on entities with BodyComponent and HumanoidAppearanceComponent.
/// Uses prototype-based body part structures defined in SpeciesPrototype.
/// Also handles migration of existing organs from body container to body part containers.
/// </summary>
public sealed class BodyPartInitializationSystem : EntitySystem
{
    [Dependency] private readonly BodyPartSystem _bodyPartSystem = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        // Subscribe to BodyInitializedEvent instead of ComponentInit to avoid duplicate subscription
        // This event is raised by BodySystem after it finishes initializing containers
        SubscribeLocalEvent<BodyComponent, BodyInitializedEvent>(OnBodyInitialized);
        
        // Validate all body part structure prototypes
        ValidateBodyPartStructures();
    }

    /// <summary>
    /// Validates all body part structure prototypes for:
    /// - Circular dependencies
    /// - Missing parent prototypes
    /// - Slot requirements (child parts must have slotId)
    /// - Slot existence in BodyPartSlotPrototype
    /// </summary>
    private void ValidateBodyPartStructures()
    {
        foreach (var structure in _prototypes.EnumeratePrototypes<BodyPartStructurePrototype>())
        {
            ValidateBodyPartStructure(structure);
        }
    }

    /// <summary>
    /// Validates a single body part structure prototype.
    /// </summary>
    private void ValidateBodyPartStructure(BodyPartStructurePrototype structure)
    {
        var partMap = structure.Parts.ToDictionary(p => p.Prototype);
        var visited = new HashSet<EntProtoId>();
        var visiting = new HashSet<EntProtoId>();

        foreach (var part in structure.Parts)
        {
            ValidateBodyPart(part, structure, partMap, visited, visiting);
        }

        // Ensure torso exists and is a root part
        var torso = structure.Parts.FirstOrDefault(p => p.Prototype == "BodyPartTorso");
        if (torso == null)
        {
            Log.Error($"Body part structure {structure.ID} does not have a torso (BodyPartTorso). Torso is required as the root part for structural stability.");
        }
        else if (torso.ParentPrototype != null)
        {
            Log.Error($"Body part structure {structure.ID} has torso with a parent. Torso must be a root part (no parent).");
        }
    }

    /// <summary>
    /// Validates a single body part definition.
    /// </summary>
    private void ValidateBodyPart(
        BodyPartDefinition part,
        BodyPartStructurePrototype structure,
        Dictionary<EntProtoId, BodyPartDefinition> partMap,
        HashSet<EntProtoId> visited,
        HashSet<EntProtoId> visiting)
    {
        if (visited.Contains(part.Prototype))
            return;

        if (visiting.Contains(part.Prototype))
        {
            Log.Error($"Body part structure {structure.ID} has circular dependency involving {part.Prototype}");
            return;
        }

        visiting.Add(part.Prototype);

        // Validate parent exists
        if (part.ParentPrototype != null)
        {
            if (!partMap.ContainsKey(part.ParentPrototype.Value))
            {
                Log.Error($"Body part structure {structure.ID} has part {part.Prototype} with missing parent {part.ParentPrototype}");
            }
            else
            {
                // Validate parent recursively
                ValidateBodyPart(partMap[part.ParentPrototype.Value], structure, partMap, visited, visiting);
            }

            // Child parts must have a slot ID
            if (part.SlotId == null)
            {
                Log.Error($"Body part structure {structure.ID} has child part {part.Prototype} without slotId. Child parts must specify a slotId.");
            }
        }

        // Validate slot exists
        if (part.SlotId != null)
        {
            if (!_prototypes.HasIndex<BodyPartSlotPrototype>(part.SlotId.Value))
            {
                Log.Error($"Body part structure {structure.ID} has part {part.Prototype} with invalid slot {part.SlotId}. Slot prototype does not exist.");
            }
        }

        visiting.Remove(part.Prototype);
        visited.Add(part.Prototype);
    }

    private void OnBodyInitialized(Entity<BodyComponent> ent, ref BodyInitializedEvent args)
    {
        // Only initialize body parts if this entity has HumanoidAppearanceComponent
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        // Check if body parts already exist
        var bodyPartSystem = EntitySystem.Get<SharedBodyPartSystem>();
        if (bodyPartSystem.GetBodyChildren(ent).Any())
            return; // Body parts already initialized

        // Get species prototype
        if (!_prototypes.TryIndex<SpeciesPrototype>(humanoid.Species, out var speciesPrototype))
            return;

        // Get body part structure from species prototype
        if (speciesPrototype.BodyPartStructure == null)
            return; // No body part structure defined - backwards compatibility

        if (!_prototypes.TryIndex<BodyPartStructurePrototype>(speciesPrototype.BodyPartStructure, out var structurePrototype))
        {
            Log.Error($"Body part structure {speciesPrototype.BodyPartStructure} not found for species {humanoid.Species}");
            return;
        }

        // Initialize body parts from prototype
        InitializeBodyPartsFromPrototype(ent, structurePrototype);

        // Migrate existing organs from body container to appropriate body part containers
        MigrateOrgansToBodyParts(ent, structurePrototype);
    }

    /// <summary>
    /// Initializes body parts from a body part structure prototype.
    /// Body parts are initialized in dependency order (torso first, then parts that attach to torso, etc.).
    /// </summary>
    private void InitializeBodyPartsFromPrototype(Entity<BodyComponent> ent, BodyPartStructurePrototype structure)
    {
        // Topologically sort body parts by dependencies
        var sortedParts = TopologicalSortBodyParts(structure.Parts);
        
        // Map of prototype ID to spawned entity UID
        var partMap = new Dictionary<EntProtoId, EntityUid>();

        foreach (var partDef in sortedParts)
        {
            // Spawn the body part
            var partEntity = Spawn(partDef.Prototype, Transform(ent).Coordinates);
            partMap[partDef.Prototype] = partEntity;

            // Determine parent and slot
            EntityUid? parentPart = null;
            string? slotId = null;

            if (partDef.ParentPrototype != null)
            {
                // Child part - attach to parent
                if (!partMap.TryGetValue(partDef.ParentPrototype.Value, out var parentEntity))
                {
                    Log.Error($"Parent body part {partDef.ParentPrototype} not found for {partDef.Prototype}");
                    Del(partEntity);
                    continue;
                }

                parentPart = parentEntity;

                // Get slot ID from prototype
                if (partDef.SlotId != null)
                {
                    if (!_prototypes.TryIndex<BodyPartSlotPrototype>(partDef.SlotId.Value, out var slotPrototype))
                    {
                        Log.Error($"Body part slot {partDef.SlotId} not found");
                        Del(partEntity);
                        continue;
                    }
                    slotId = slotPrototype.ID;
                }
            }
            else
            {
                // Root part - attach to body
                // For root parts, slotId comes from the definition
                if (partDef.SlotId != null)
                {
                    if (!_prototypes.TryIndex<BodyPartSlotPrototype>(partDef.SlotId.Value, out var slotPrototype))
                    {
                        Log.Error($"Body part slot {partDef.SlotId} not found");
                        Del(partEntity);
                        continue;
                    }
                    slotId = slotPrototype.ID;
                }
            }

            // Attach the body part
            if (!_bodyPartSystem.AttachBodyPart(ent, partEntity, slotId, parentPart))
            {
                Log.Error($"Failed to attach body part {partDef.Prototype} to body {ent}");
                Del(partEntity);
            }
        }
    }

    /// <summary>
    /// Topologically sorts body parts by dependencies (parent parts must come before child parts).
    /// Ensures torso is always first (root part).
    /// </summary>
    private List<BodyPartDefinition> TopologicalSortBodyParts(List<BodyPartDefinition> parts)
    {
        var sorted = new List<BodyPartDefinition>();
        var visited = new HashSet<EntProtoId>();
        var visiting = new HashSet<EntProtoId>();

        // Build dependency graph
        var partMap = parts.ToDictionary(p => p.Prototype);
        var dependencies = new Dictionary<EntProtoId, List<EntProtoId>>();
        
        foreach (var part in parts)
        {
            dependencies[part.Prototype] = new List<EntProtoId>();
            if (part.ParentPrototype != null)
            {
                dependencies[part.Prototype].Add(part.ParentPrototype.Value);
            }
        }

        // Visit each part
        foreach (var part in parts)
        {
            if (!visited.Contains(part.Prototype))
            {
                Visit(part.Prototype, partMap, dependencies, visited, visiting, sorted);
            }
        }

        // Ensure torso is first (structural core)
        var torso = sorted.FirstOrDefault(p => p.Prototype == "BodyPartTorso");
        if (torso != null && sorted[0] != torso)
        {
            sorted.Remove(torso);
            sorted.Insert(0, torso);
        }

        return sorted;
    }

    private void Visit(
        EntProtoId partId,
        Dictionary<EntProtoId, BodyPartDefinition> partMap,
        Dictionary<EntProtoId, List<EntProtoId>> dependencies,
        HashSet<EntProtoId> visited,
        HashSet<EntProtoId> visiting,
        List<BodyPartDefinition> sorted)
    {
        if (visited.Contains(partId))
            return;

        if (visiting.Contains(partId))
        {
            Log.Error($"Circular dependency detected in body part structure involving {partId}");
            return;
        }

        visiting.Add(partId);

        // Visit dependencies first
        if (dependencies.TryGetValue(partId, out var deps))
        {
            foreach (var dep in deps)
            {
                if (partMap.ContainsKey(dep))
                {
                    Visit(dep, partMap, dependencies, visited, visiting, sorted);
                }
            }
        }

        visiting.Remove(partId);
        visited.Add(partId);

        if (partMap.TryGetValue(partId, out var part))
        {
            sorted.Add(part);
        }
    }

    /// <summary>
    /// Migrates existing organs from the body's container to appropriate body part containers
    /// based on organ placement rules from the prototype.
    /// </summary>
    private void MigrateOrgansToBodyParts(Entity<BodyComponent> ent, BodyPartStructurePrototype structure)
    {
        if (ent.Comp.Organs == null)
            return;

        var bodyPartSystem = EntitySystem.Get<SharedBodyPartSystem>();
        
        // Build map of body part type to entity
        var partTypeMap = new Dictionary<BodyPartType, EntityUid?>();
        
        foreach (var (partId, partComp) in bodyPartSystem.GetBodyChildren(ent))
        {
            partTypeMap[partComp.PartType] = partId;
        }

        // Migrate organs
        var organsToMigrate = ent.Comp.Organs.ContainedEntities.ToList();
        foreach (var organ in organsToMigrate)
        {
            if (!TryComp<OrganComponent>(organ, out var organComp))
                continue;

            // Find matching organ placement rule
            EntityUid? targetPart = null;
            
            foreach (var rule in structure.OrganPlacementRules)
            {
                // Check if this rule matches the organ
                bool matches = false;
                
                if (rule.OrganCategory == null)
                {
                    // Default rule - matches all organs
                    matches = true;
                }
                else if (organComp.Category == rule.OrganCategory)
                {
                    // Specific category match
                    matches = true;
                }

                if (matches)
                {
                    // Find body part of the target type
                    if (partTypeMap.TryGetValue(rule.TargetPartType, out var part))
                    {
                        targetPart = part;
                        break; // Use first matching rule
                    }
                }
            }

            if (targetPart == null)
                continue; // No appropriate body part found, leave organ in body container

            // Move organ to body part's container
            if (!TryComp<BodyPartComponent>(targetPart.Value, out var partComp) || partComp.Organs == null)
                continue;

            if (_container.Remove((organ, null, null), ent.Comp.Organs))
            {
                _container.Insert((organ, null, null, null), partComp.Organs);
            }
        }
    }
}
