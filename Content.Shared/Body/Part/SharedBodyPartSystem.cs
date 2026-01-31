using Content.Shared.Body.Components;
using Content.Shared.Containers;
using Content.Shared.Medical.Cybernetics;
using Content.Shared.Storage;
using Content.Shared.Wires;
using Robust.Shared.Containers;

namespace Content.Shared.Body.Part;

/// <summary>
/// Shared system for managing body parts. Handles attachment, detachment, and queries.
/// </summary>
public abstract class SharedBodyPartSystem : EntitySystem
{
    /// <summary>
    /// Container ID for body parts attached directly to the body (torso, head).
    /// </summary>
    public const string BodyRootContainerId = "body_parts_root";

    [Dependency] protected readonly SharedContainerSystem ContainerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyPartComponent, ComponentInit>(OnBodyPartInit);
        SubscribeLocalEvent<BodyPartComponent, ComponentShutdown>(OnBodyPartShutdown);
        SubscribeLocalEvent<BodyPartComponent, EntInsertedIntoContainerMessage>(OnBodyPartInserted);
        SubscribeLocalEvent<BodyPartComponent, EntRemovedFromContainerMessage>(OnBodyPartRemoved);

        SubscribeLocalEvent<CyberLimbComponent, ComponentInit>(OnCyberLimbInit);
    }

    private void OnBodyPartInit(Entity<BodyPartComponent> ent, ref ComponentInit args)
    {
        // Initialize organ container for this body part
        ent.Comp.Organs = ContainerSystem.EnsureContainer<Container>(ent, BodyPartComponent.OrganContainerId);
    }

    private void OnBodyPartShutdown(Entity<BodyPartComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Organs is { } organs)
        {
            ContainerSystem.ShutdownContainer(organs);
        }
    }

    private void OnBodyPartInserted(Entity<BodyPartComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        // Handle organ insertion into body part
        if (args.Container.ID == BodyPartComponent.OrganContainerId)
        {
            // Organ was inserted directly, likely via surgery, so we don't need to do anything.
            return;
        }

        // What the body part was inserted into
        var parent = args.Container.Owner;
        
        // Is it the torso?
        if (HasComp<BodyComponent>(parent))
        {
            // Attached to body (root part)
            ent.Comp.Body = parent;
            ent.Comp.Parent = null;
        }
        else if (TryComp<BodyPartComponent>(parent, out var parentPart))
        {
            // Attached to another body part
            ent.Comp.Body = parentPart.Body;
            ent.Comp.Parent = parent;
        }

        Dirty(ent);

        // Raise events
        var attachedEvent = new BodyPartAttachedEvent(ent.Comp.Body ?? EntityUid.Invalid, ent.Comp.Parent);
        RaiseLocalEvent(ent, ref attachedEvent);

        if (ent.Comp.Body != null)
        {
            var addedEvent = new BodyPartAddedToBodyEvent(ent);
            RaiseLocalEvent(ent.Comp.Body.Value, ref addedEvent);
        }
    }

    protected virtual void OnBodyPartRemoved(Entity<BodyPartComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        // Handle organ removal from body part
        if (args.Container.ID == BodyPartComponent.OrganContainerId)
        {
            // Organ was removed directly, likely via surgery, so we don't need to do anything.
            return;
        }

        // Body part was removed from a torso or another body part
        var oldBody = ent.Comp.Body;
        var oldParent = ent.Comp.Parent;

        ent.Comp.Body = null;
        ent.Comp.Parent = null;
        ent.Comp.SlotId = null;

        Dirty(ent);

        // Raise events
        var detachedEvent = new BodyPartDetachedEvent(oldBody, oldParent);
        RaiseLocalEvent(ent, ref detachedEvent);

        if (oldBody != null)
        {
            var removedEvent = new BodyPartRemovedFromBodyEvent(ent);
            RaiseLocalEvent(oldBody.Value, ref removedEvent);
        }
    }

    /// <summary>
    /// Gets all body parts attached to a body.
    /// </summary>
    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildren(EntityUid body, BodyComponent? bodyComp = null)
    {
        if (!Resolve(body, ref bodyComp, false))
            yield break;

        // Body parts are stored in containers on the body or on other body parts
        // We need to search for all body parts that have this body as their Body field
        var query = EntityQueryEnumerator<BodyPartComponent>();
        while (query.MoveNext(out var uid, out var part))
        {
            if (part.Body == body)
            {
                yield return (uid, part);
            }
        }
    }

    /// <summary>
    /// Gets body parts of a specific type and symmetry.
    /// IE: When you want the Left Arm, you can call this with type = BodyPartType.Arm and symmetry = BodyPartSymmetry.Left.
    /// </summary>
    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildrenOfType(
        EntityUid body,
        BodyPartType type,
        BodyComponent? bodyComp = null,
        BodyPartSymmetry? symmetry = null)
    {
        foreach (var (id, part) in GetBodyChildren(body, bodyComp))
        {
            if (part.PartType != type)
                continue;

            if (symmetry.HasValue && part.Symmetry != symmetry.Value)
                continue;

            yield return (id, part);
        }
    }

    public EntityUid? GetParentPart(EntityUid part)
    {
        if (!TryComp<BodyPartComponent>(part, out var partComp))
            return null;

        return partComp.Parent;
    }

    public (EntityUid Parent, string Slot)? GetParentPartAndSlotOrNull(EntityUid part)
    {
        if (!TryComp<BodyPartComponent>(part, out var partComp))
            return null;

        if (partComp.Parent == null || partComp.SlotId == null)
            return null;

        return (partComp.Parent.Value, partComp.SlotId);
    }

    /// <summary>
    /// Initializes cyber-limb storage container and wires panel.
    /// </summary>
    private void OnCyberLimbInit(Entity<CyberLimbComponent> ent, ref ComponentInit args)
    {
        // Set StorageContainer to the StorageComponent's container (uses StorageComponent.ContainerId)
        // StorageComponent initializes its container in its own OnComponentInit, but initialization order
        // is not guaranteed, so we ensure the container exists and then reference StorageComponent's container if available
        if (TryComp<StorageComponent>(ent, out var storage) && storage.Container != null)
        {
            // StorageComponent already initialized, use its container
            ent.Comp.StorageContainer = storage.Container;
        }
        else
        {
            // StorageComponent not initialized yet, ensure the container with StorageComponent.ContainerId
            // StorageComponent will use the same container when it initializes
            ent.Comp.StorageContainer = ContainerSystem.EnsureContainer<Container>(ent, StorageComponent.ContainerId);
        }

        // Initialize WiresPanelComponent if not present (should be in prototype, but ensure it exists)
        EnsureComp<WiresPanelComponent>(ent);
    }
}
