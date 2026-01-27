// SPDX-FileCopyrightText: 2024 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Голубь <124601871+Golubgik@users.noreply.github.com>
// SPDX-License-Identifier: MIT

using Content.Shared.Actions.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Inventory;
using Content.Shared.Mind;

namespace Content.Shared.Actions;

/// <summary>
/// <see cref="ActionGrantComponent"/>
/// </summary>
public sealed class ActionGrantSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActionGrantComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ActionGrantComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ItemActionGrantComponent, GetItemActionsEvent>(OnItemGet);
        
        // Subscribe to head-specific events for species ability management
        SubscribeLocalEvent<BodyComponent, HeadDetachingEvent>(OnHeadDetaching);
        SubscribeLocalEvent<BodyComponent, HeadAttachingEvent>(OnHeadAttaching);
    }

    private void OnItemGet(Entity<ItemActionGrantComponent> ent, ref GetItemActionsEvent args)
    {

        if (!TryComp(ent.Owner, out ActionGrantComponent? grant))
            return;

        if (ent.Comp.ActiveIfWorn && (args.SlotFlags == null || args.SlotFlags == SlotFlags.POCKET))
            return;

        foreach (var action in grant.ActionEntities)
        {
            args.AddAction(action);
        }
    }

    private void OnMapInit(Entity<ActionGrantComponent> ent, ref MapInitEvent args)
    {
        foreach (var action in ent.Comp.Actions)
        {
            EntityUid? actionEnt = null;
            _actions.AddAction(ent.Owner, ref actionEnt, action);

            if (actionEnt != null)
                ent.Comp.ActionEntities.Add(actionEnt.Value);
        }
    }

    private void OnShutdown(Entity<ActionGrantComponent> ent, ref ComponentShutdown args)
    {
        foreach (var actionEnt in ent.Comp.ActionEntities)
        {
            _actions.RemoveAction(ent.Owner, actionEnt);
        }
    }

    private void OnHeadDetaching(Entity<BodyComponent> ent, ref HeadDetachingEvent args)
    {
        RemoveSpeciesAbilitiesOnHeadDetach(ent, args.HeadPart);
    }

    private void OnHeadAttaching(Entity<BodyComponent> ent, ref HeadAttachingEvent args)
    {
        RestoreSpeciesAbilitiesOnHeadAttach(ent, args.HeadPart);
    }

    /// <summary>
    /// Removes species abilities when a head is detached.
    /// Called via HeadDetachingEvent subscription.
    /// </summary>
    public void RemoveSpeciesAbilitiesOnHeadDetach(Entity<BodyComponent> body, Entity<BodyPartComponent> headPart)
    {
        // Get body's ActionGrantComponent (contains species abilities)
        if (!TryComp<ActionGrantComponent>(body, out var actionGrant))
            return;
        
        // Find mind entity (could be on body or brain in detached head)
        // Note: BodyPartSystem may have already transferred mind to brain
        EntityUid? mindEntity = null;
        
        // First check if mind is still on body
        if (_mindSystem.TryGetMind(body, out var mindId, out _))
        {
            mindEntity = mindId;
        }
        else
        {
            // Check if mind is on brain in detached head
            // Brain is in the head's organs container
            if (headPart.Comp.Organs != null)
            {
                foreach (var organ in headPart.Comp.Organs.ContainedEntities)
                {
                    if (HasComp<BrainComponent>(organ) && _mindSystem.TryGetMind(organ, out mindId, out _))
                    {
                        mindEntity = mindId;
                        break;
                    }
                }
            }
        }
        
        if (mindEntity == null || mindEntity == EntityUid.Invalid)
            return;
        
        // Remove species abilities from mind entity
        if (!TryComp<ActionsComponent>(mindEntity.Value, out var actionsComp))
            return;
        
        foreach (var actionEnt in actionGrant.ActionEntities)
        {
            if (TryComp<ActionComponent>(actionEnt, out var actionComp))
                _actions.RemoveAction((mindEntity.Value, actionsComp), (actionEnt, actionComp));
        }
    }

    /// <summary>
    /// Restores species abilities when a head is reattached.
    /// Called via HeadAttachingEvent subscription.
    /// </summary>
    public void RestoreSpeciesAbilitiesOnHeadAttach(Entity<BodyComponent> body, Entity<BodyPartComponent> headPart)
    {
        // Get body's ActionGrantComponent (contains species abilities)
        if (!TryComp<ActionGrantComponent>(body, out var actionGrant))
            return;
        
        // Get mind from body (should be on body after brain insertion surgery)
        if (!_mindSystem.TryGetMind(body, out var mindId, out _))
            return;
        
        var mindEntity = mindId;
        if (mindEntity == EntityUid.Invalid)
            return;
        
        // Re-add species abilities to mind entity
        var actionsComp = EnsureComp<ActionsComponent>(mindEntity);
        foreach (var actionEnt in actionGrant.ActionEntities)
        {
            if (Exists(actionEnt) && TryComp<ActionComponent>(actionEnt, out var actionComp))
                _actions.AddActionDirect((mindEntity, actionsComp), (actionEnt, actionComp));
        }
    }
}
