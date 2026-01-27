using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Ghost;
using Content.Shared.Gibbing;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;

namespace Content.Server.Body.Systems;

/// <summary>
/// System that handles mind transfer during gibbing.
/// If a body or body part contains a brain when it's gibbed, transfers the mind to the brain.
/// </summary>
public sealed class BodyGibbingSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedBodyPartSystem _bodyPartSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        // Handle body part gibbing - transfer mind to brain if part contains one
        // Note: We only subscribe to BodyPartComponent to avoid duplicate subscription with BodySystem.Relay
        // Body gibbing will be handled when individual body parts are gibbed
        SubscribeLocalEvent<BodyPartComponent, BeingGibbedEvent>(OnBodyPartGibbed);
    }

    /// <summary>
    /// Handles mind transfer when a body part is gibbed.
    /// If the body part contains a brain, transfers the mind to it.
    /// </summary>
    private void OnBodyPartGibbed(Entity<BodyPartComponent> ent, ref BeingGibbedEvent args)
    {
        // Check if this body part contains a brain
        if (ent.Comp.Organs == null)
            return;

        EntityUid? brainEntity = null;
        foreach (var organ in ent.Comp.Organs.ContainedEntities)
        {
            if (HasComp<BrainComponent>(organ))
            {
                brainEntity = organ;
                break; // Only one brain per head
            }
        }

        if (brainEntity == null)
            return;

        // Find where the mind currently is
        EntityUid mindId = EntityUid.Invalid;
        MindComponent? mind = null;
        
        // Check body first (normal case)
        var bodyEntity = ent.Comp.Body;
        if (bodyEntity != null && TryComp<MindContainerComponent>(bodyEntity.Value, out var bodyMindContainer) &&
            _mindSystem.TryGetMind(bodyEntity.Value, out mindId, out mind, bodyMindContainer))
        {
            // Mind is on body - transfer to brain
            EnsureComp<MindContainerComponent>(brainEntity.Value);
            EnsureComp<GhostOnMoveComponent>(brainEntity.Value);
            _mindSystem.TransferTo(mindId, brainEntity.Value, mind: mind);
            
            // Ensure mind has action container
            EnsureComp<ActionsContainerComponent>(mindId);
            var mindActionContainer = Comp<ActionsContainerComponent>(mindId);
            
            // Grant actions from mind's container to brain
            if (mindActionContainer.Container.ContainedEntities.Count > 0)
            {
                EnsureComp<ActionsComponent>(brainEntity.Value);
                _actions.GrantContainedActions((brainEntity.Value, null), (mindId, mindActionContainer));
            }
            
            // Species abilities are automatically removed by ActionGrantSystem when head is gibbed
        }
        // Check brain (head already detached case)
        else if (TryComp<MindContainerComponent>(brainEntity.Value, out var brainMindContainer) &&
                 _mindSystem.TryGetMind(brainEntity.Value, out mindId, out mind, brainMindContainer))
        {
            // Mind is already on brain - no transfer needed, just ensure components
            EnsureComp<MindContainerComponent>(brainEntity.Value);
            EnsureComp<GhostOnMoveComponent>(brainEntity.Value);
            // Actions should already be granted, but ensure they are
            if (TryComp<ActionsContainerComponent>(mindId, out var mindActionContainer))
            {
                if (mindActionContainer.Container.ContainedEntities.Count > 0)
                {
                    EnsureComp<ActionsComponent>(brainEntity.Value);
                    _actions.GrantContainedActions((brainEntity.Value, null), (mindId, mindActionContainer));
                }
            }
        }
    }

    /// <summary>
    /// Finds the brain entity in a body or its body parts.
    /// </summary>
    private EntityUid? FindBrainInBody(Entity<BodyComponent> body)
    {
        // Check body's organ container first
        if (body.Comp.Organs != null)
        {
            foreach (var organ in body.Comp.Organs.ContainedEntities)
            {
                if (HasComp<BrainComponent>(organ))
                    return organ;
            }
        }

        // Check body parts for brain
        foreach (var (partId, partComp) in _bodyPartSystem.GetBodyChildren(body))
        {
            if (partComp.Organs == null)
                continue;

            foreach (var organ in partComp.Organs.ContainedEntities)
            {
                if (HasComp<BrainComponent>(organ))
                    return organ;
            }
        }

        return null;
    }
}
