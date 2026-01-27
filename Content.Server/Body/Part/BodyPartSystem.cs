using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Containers;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.Containers;

namespace Content.Server.Body.Part;

/// <summary>
/// Server-side body part system. Handles attachment and detachment of body parts.
/// </summary>
public sealed class BodyPartSystem : SharedBodyPartSystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    /// <summary>
    /// Gets the container ID for a body part slot on a parent part.
    /// </summary>
    public static string GetPartSlotContainerId(string slotId) => $"body_part_slot_{slotId}";

    /// <summary>
    /// Attaches a body part to a body or parent part.
    /// </summary>
    /// <param name="body">The body entity to attach to (for root parts) or the body that owns the parent part</param>
    /// <param name="part">The body part entity to attach</param>
    /// <param name="slotId">The slot ID on the parent part, or null for root parts</param>
    /// <param name="parentPart">The parent body part, or null for root parts</param>
    /// <returns>True if attachment was successful</returns>
    public bool AttachBodyPart(EntityUid body, EntityUid part, string? slotId = null, EntityUid? parentPart = null)
    {
        if (!TryComp<BodyPartComponent>(part, out var partComp))
            return false;

        if (!HasComp<BodyComponent>(body))
            return false;

        // Determine which container to use
        BaseContainer? container;
        EntityUid containerOwner;

        if (parentPart == null)
        {
            // Root part - attach to body's root container
            container = _container.EnsureContainer<Container>(body, SharedBodyPartSystem.BodyRootContainerId);
            containerOwner = body;
        }
        else
        {
            // Child part - attach to parent part's slot container
            if (!TryComp<BodyPartComponent>(parentPart, out var parentComp))
                return false;

            var containerId = GetPartSlotContainerId(slotId ?? "");
            container = _container.EnsureContainer<Container>(parentPart.Value, containerId);
            containerOwner = parentPart.Value;
        }

        // Insert into container
        if (!_container.Insert((part, null, null, null), container))
            return false;

        // Set parent and slot info
        partComp.Parent = parentPart;
        partComp.SlotId = slotId;
        partComp.Body = body;

        Dirty(part, partComp);

        return true;
    }

    /// <summary>
    /// Detaches a body part from its body or parent part.
    /// </summary>
    /// <param name="part">The body part entity to detach</param>
    /// <returns>True if detachment was successful</returns>
    public bool DetachBodyPart(EntityUid part)
    {
        if (!TryComp<BodyPartComponent>(part, out var partComp))
            return false;

        if (partComp.Body == null)
            return false; // Already detached

        // Find the container this part is in
        BaseContainer? container = null;
        EntityUid? containerOwner = null;

        if (partComp.Parent == null)
        {
            // Root part - in body's root container
            if (TryComp<BodyComponent>(partComp.Body.Value, out var bodyComp))
            {
                if (_container.TryGetContainer(partComp.Body.Value, SharedBodyPartSystem.BodyRootContainerId, out var rootContainer))
                {
                    container = rootContainer;
                    containerOwner = partComp.Body.Value;
                }
            }
        }
        else
        {
            // Child part - in parent's slot container
            if (partComp.SlotId != null)
            {
                var containerId = GetPartSlotContainerId(partComp.SlotId);
                if (_container.TryGetContainer(partComp.Parent.Value, containerId, out var slotContainer))
                {
                    container = slotContainer;
                    containerOwner = partComp.Parent.Value;
                }
            }
        }

        if (container == null || containerOwner == null)
            return false;

        // Remove from container
        if (!_container.Remove((part, null, null), container))
            return false;

        // Component will be updated by OnBodyPartRemoved event handler
        return true;
    }

    /// <summary>
    /// Gets the parent part and slot for a body part, if any.
    /// </summary>
    public (EntityUid Parent, string Slot)? GetParentPartAndSlot(EntityUid part)
    {
        if (!TryComp<BodyPartComponent>(part, out var partComp))
            return null;

        if (partComp.Parent == null || partComp.SlotId == null)
            return null;

        return (partComp.Parent.Value, partComp.SlotId);
    }

    protected override void OnBodyPartRemoved(Entity<BodyPartComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        // Handle mind transfer if this body part contains a brain
        // This must happen BEFORE we call base, which raises the BodyPartDetachedEvent
        var oldBody = ent.Comp.Body;
        
        if (oldBody != null && ent.Comp.Organs != null)
        {
            // Check if this body part contains a brain
            // If no brain is found, the mind stays on the body (or wherever it currently is)
            EntityUid? brainEntity = null;
            foreach (var organ in ent.Comp.Organs.ContainedEntities)
            {
                if (HasComp<BrainComponent>(organ))
                {
                    brainEntity = organ;
                    break; // Only one brain per head
                }
            }

            // Only transfer mind if a brain was found in the detached body part
            // If brainEntity is null, no mind transfer occurs and the player stays on the body
            if (brainEntity != null)
            {
                // Get mind from the body entity
                if (_mindSystem.TryGetMind(oldBody.Value, out var mindId, out var mind))
                {
                    // Ensure brain has mind container components
                    EnsureComp<MindContainerComponent>(brainEntity.Value);
                    EnsureComp<GhostOnMoveComponent>(brainEntity.Value);
                    
                    // Transfer mind to brain entity (in detached head)
                    _mindSystem.TransferTo(mindId, brainEntity.Value, mind: mind);
                    
                    // Ensure mind has action container and transfer actions
                    EnsureComp<ActionsContainerComponent>(mindId);
                    var mindActionContainer = Comp<ActionsContainerComponent>(mindId);
                    
                    // Grant actions from mind's container to brain entity
                    if (mindActionContainer.Container.ContainedEntities.Count > 0)
                    {
                        EnsureComp<ActionsComponent>(brainEntity.Value);
                        _actions.GrantContainedActions((brainEntity.Value, null), (mindId, mindActionContainer));
                    }
                    
                    // Species abilities are automatically removed by ActionGrantSystem when head is detached
                }
            }
        }

        // Call base to handle normal detachment logic and raise events
        base.OnBodyPartRemoved(ent, ref args);
    }
}
