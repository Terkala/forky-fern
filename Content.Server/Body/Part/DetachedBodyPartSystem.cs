using System.Collections.Generic;
using Content.Server.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Containers;
using Content.Shared.Gibbing;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
// using Robust.Server.GameObjects; // SpriteSystem is client-only
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility; // For SpriteSpecifier

namespace Content.Server.Body.Part;

/// <summary>
/// System for managing detached body part entities.
/// Spawns entities with sprites when body parts are detached.
/// </summary>
public sealed class DetachedBodyPartSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    // [Dependency] private readonly SpriteSystem _spriteSystem = default!; // Client-only, sprite copying disabled on server
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly BodySystem _bodySystem = default!;

    [Dependency] private readonly SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        // Subscribe to BodyPartAppearanceHandledEvent instead of BodyPartDetachingEvent
        // This avoids duplicate subscriptions with BodyPartAppearanceSystem while maintaining
        // the event-based architecture. The event is raised after appearance is handled.
        SubscribeLocalEvent<BodyComponent, BodyPartAppearanceHandledEvent>(OnBodyPartAppearanceHandled);
        
        // Subscribe to BodyBeingGibbedEvent to spawn detached body parts before gib completes
        // This event is raised by BodySystem after relaying BeingGibbedEvent to organs,
        // ensuring it fires before the gib completes so body parts can be detached successfully
        SubscribeLocalEvent<BodyComponent, BodyBeingGibbedEvent>(OnBodyBeingGibbed);
        
        // Subscribe to BeingGibbedEvent on DetachedBodyPartComponent to drop organs when detached limbs are gibbed
        // Use event ordering to run before BodyGibbingSystem to ensure organs are dropped before any mind transfer logic
        SubscribeLocalEvent<DetachedBodyPartComponent, BeingGibbedEvent>(OnDetachedBodyPartGibbed, before: new[] { typeof(BodyGibbingSystem) });
    }

    private void OnBodyPartAppearanceHandled(Entity<BodyComponent> ent, ref BodyPartAppearanceHandledEvent args)
    {
        HandleBodyPartDetaching(ent, args.BodyPart);

        // Raise event for other systems (e.g. SlimeLimbRegenerationSystem) that need to react to detachment
        var detachedEv = new BodyPartFullyDetachedEvent(ent, args.BodyPart);
        RaiseLocalEvent(ent, ref detachedEv);
    }

    /// <summary>
    /// Handles gibbing of detached body parts by dropping contained organs as giblets.
    /// This ensures that when a detached limb (arm, leg, head) is gibbed, its organs are dropped.
    /// Runs before BodyGibbingSystem to ensure organs are dropped before mind transfer logic.
    /// </summary>
    private void OnDetachedBodyPartGibbed(Entity<DetachedBodyPartComponent> ent, ref BeingGibbedEvent args)
    {
        // Get the original body part entity if it still exists
        if (ent.Comp.OriginalBodyPart == null || !Exists(ent.Comp.OriginalBodyPart.Value))
            return;

        var originalPartUid = ent.Comp.OriginalBodyPart.Value;

        // Check if the original body part has a BodyPartComponent with organs
        if (!TryComp<BodyPartComponent>(originalPartUid, out var bodyPartComp))
            return;

        // Get organs from the original body part's container
        if (bodyPartComp.Organs == null)
            return;

        // Drop all organs as giblets
        // Create a list first since we'll be modifying the container during iteration
        var organsToDrop = new List<EntityUid>();
        foreach (var organ in bodyPartComp.Organs.ContainedEntities)
        {
            organsToDrop.Add(organ);
        }
        
        foreach (var organ in organsToDrop)
        {
            // Remove organ from container
            _containers.Remove((organ, null, null), bodyPartComp.Organs);
            
            // Add organ to giblets so it gets dropped when gib completes
            args.Giblets.Add(organ);
        }
    }

    /// <summary>
    /// Handles body gibbing by spawning detached entities for all body parts before the gib completes.
    /// This ensures body parts (arms, legs, head) are dropped as giblets.
    /// This event is raised during BeingGibbedEvent handling, before the gib completes,
    /// ensuring body parts can be detached successfully.
    /// </summary>
    private void OnBodyBeingGibbed(Entity<BodyComponent> ent, ref BodyBeingGibbedEvent args)
    {
        // Get all body parts by iterating through containers
        // This is more reliable than GetBodyChildren which relies on the Body field
        // which might be cleared during gibbing
        var bodyPartsToProcess = new List<EntityUid>();
        
        // Start with root body parts (torso, head) from the body's container
        if (ent.Comp.RootBodyParts != null)
        {
            foreach (var rootPart in ent.Comp.RootBodyParts.ContainedEntities)
            {
                bodyPartsToProcess.Add(rootPart);
            }
        }
        
        // Process all body parts (including children found recursively)
        var processedParts = new HashSet<EntityUid>();
        while (bodyPartsToProcess.Count > 0)
        {
            var partId = bodyPartsToProcess[0];
            bodyPartsToProcess.RemoveAt(0);
            
            if (processedParts.Contains(partId))
                continue;
                
            processedParts.Add(partId);
            
            if (!TryComp<BodyPartComponent>(partId, out var partComp))
                continue;
            
            // Spawn detached body part entity and add to giblets
            var detachedEntity = Spawn("DetachedBodyPart", _transform.GetMapCoordinates(ent));
            
            // Add DetachedBodyPartComponent
            var detachedComp = EnsureComp<DetachedBodyPartComponent>(detachedEntity);
            detachedComp.OriginalBodyPart = partId;
            
            // Add physics component so it can be picked up
            EnsureComp<Robust.Shared.Physics.Components.PhysicsComponent>(detachedEntity);
            
            Dirty(detachedEntity, detachedComp);
            
            // Add to giblets hashset so it gets dropped when gib completes
            args.GibbingEvent.Giblets.Add(detachedEntity);
            
            // Add child body parts to processing queue
            // Child parts are stored in containers on their parent parts
            if (partComp.Organs != null)
            {
                // Check for child parts in slot containers
                // Body parts attach to parent parts via slot containers
                var containerManager = CompOrNull<ContainerManagerComponent>(partId);
                if (containerManager != null)
                {
                    foreach (var container in _containers.GetAllContainers(partId, containerManager))
                    {
                        // Skip organ containers
                        if (container.ID == BodyPartComponent.OrganContainerId)
                            continue;
                            
                        // Add any body parts found in this container
                        foreach (var childPart in container.ContainedEntities)
                        {
                            if (HasComp<BodyPartComponent>(childPart) && !processedParts.Contains(childPart))
                            {
                                bodyPartsToProcess.Add(childPart);
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Handles spawning a detached body part entity when a body part is detached.
    /// Called by BodySystem when it detects a body part is being detached.
    /// </summary>
    public void HandleBodyPartDetaching(Entity<BodyComponent> body, Entity<BodyPartComponent> bodyPart)
    {
        // Spawn detached body part entity
        var detachedEntity = Spawn("DetachedBodyPart", _transform.GetMapCoordinates(body));

        // Add DetachedBodyPartComponent
        var detachedComp = EnsureComp<DetachedBodyPartComponent>(detachedEntity);
        detachedComp.OriginalBodyPart = bodyPart;

        // Copy sprite from humanoid appearance
        // Note: Sprite copying is disabled on server-side since SpriteSystem is client-only
        // Sprite copying should be handled by a client-side system if needed
        // if (TryComp<HumanoidAppearanceComponent>(body, out var appearance))
        // {
        //     CopyBodyPartSprite(detachedEntity, bodyPart.Comp, appearance);
        // }

        // Add physics component so it can be picked up
        EnsureComp<Robust.Shared.Physics.Components.PhysicsComponent>(detachedEntity);

        Dirty(detachedEntity, detachedComp);
    }

    /// <summary>
    /// Copies the sprite for a body part from the humanoid appearance to the detached entity.
    /// For arms, combines arm+hand sprites. For legs, combines leg+foot sprites.
    /// NOTE: This method is disabled on server-side since SpriteSystem is client-only.
    /// </summary>
    /*
    private void CopyBodyPartSprite(EntityUid detachedEntity, BodyPartComponent partComp, HumanoidAppearanceComponent appearance)
    {
        if (!TryComp<SpriteComponent>(detachedEntity, out var sprite))
            sprite = EnsureComp<SpriteComponent>(detachedEntity);

        // Get the layers for this body part
        var layers = GetLayersForBodyPart(partComp.PartType, partComp.Symmetry);

        // For arms and legs, we need to combine multiple layers
        // Use the primary layer (arm or leg) as the base sprite
        HumanoidVisualLayers? primaryLayer = null;
        foreach (var layer in layers)
        {
            if (layer == HumanoidVisualLayers.LArm || layer == HumanoidVisualLayers.RArm ||
                layer == HumanoidVisualLayers.LLeg || layer == HumanoidVisualLayers.RLeg)
            {
                primaryLayer = layer;
                break;
            }
        }

        if (primaryLayer == null)
        {
            // For head/torso, use the single layer
            primaryLayer = layers.FirstOrDefault();
        }

        if (primaryLayer == null)
            return;

        // Get the sprite specifier from the appearance component
        var spriteSpecifier = GetSpriteForLayer(appearance, primaryLayer.Value);

        if (spriteSpecifier != null)
        {
            // Set the sprite on layer 0 (base layer)
            _spriteSystem.LayerSetSprite((detachedEntity, sprite), 0, spriteSpecifier);
        }

        // Apply skin color if the layer matches skin
        if (appearance.BaseLayers.TryGetValue(primaryLayer.Value, out var baseLayer) && baseLayer.MatchSkin)
        {
            _spriteSystem.SetColor((detachedEntity, sprite), appearance.SkinColor.WithAlpha(baseLayer.LayerAlpha));
        }
    }

    /// <summary>
    /// Gets the sprite specifier for a humanoid visual layer from the appearance component.
    /// NOTE: This method is disabled on server-side since SpriteSystem is client-only.
    /// </summary>
    /*
    private SpriteSpecifier? GetSpriteForLayer(HumanoidAppearanceComponent appearance, HumanoidVisualLayers layer)
    {
        // Check custom base layers first
        if (appearance.CustomBaseLayers.TryGetValue(layer, out var customLayer) && customLayer.Id != null)
        {
            if (_prototypeManager.TryIndex<HumanoidSpeciesSpriteLayer>(customLayer.Id.Value, out var customProto))
            {
                return customProto.BaseSprite;
            }
        }

        // Check default species layers
        if (appearance.BaseLayers.TryGetValue(layer, out var baseLayer))
        {
            return baseLayer.BaseSprite;
        }

        // Try to get from species prototype
        if (_prototypeManager.TryIndex<SpeciesPrototype>(appearance.Species, out var speciesProto))
        {
            if (_prototypeManager.TryIndex<HumanoidSpeciesBaseSpritesPrototype>(speciesProto.SpriteSet, out var spriteSet))
            {
                if (spriteSet.Sprites.TryGetValue(layer, out var spriteId))
                {
                    if (_prototypeManager.TryIndex<HumanoidSpeciesSpriteLayer>(spriteId, out var spriteProto))
                    {
                        return spriteProto.BaseSprite;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the HumanoidVisualLayers that correspond to a body part type and symmetry.
    /// </summary>
    private HashSet<HumanoidVisualLayers> GetLayersForBodyPart(BodyPartType partType, BodyPartSymmetry symmetry)
    {
        var layers = new HashSet<HumanoidVisualLayers>();

        switch (partType)
        {
            case BodyPartType.Head:
                layers.Add(HumanoidVisualLayers.Head);
                break;

            case BodyPartType.Torso:
                layers.Add(HumanoidVisualLayers.Chest);
                break;

            case BodyPartType.Arm:
                if (symmetry == BodyPartSymmetry.Left)
                {
                    layers.Add(HumanoidVisualLayers.LArm);
                    layers.Add(HumanoidVisualLayers.LHand);
                }
                else if (symmetry == BodyPartSymmetry.Right)
                {
                    layers.Add(HumanoidVisualLayers.RArm);
                    layers.Add(HumanoidVisualLayers.RHand);
                }
                break;

            case BodyPartType.Leg:
                if (symmetry == BodyPartSymmetry.Left)
                {
                    layers.Add(HumanoidVisualLayers.LLeg);
                    layers.Add(HumanoidVisualLayers.LFoot);
                }
                else if (symmetry == BodyPartSymmetry.Right)
                {
                    layers.Add(HumanoidVisualLayers.RLeg);
                    layers.Add(HumanoidVisualLayers.RFoot);
                }
                break;
        }

        return layers;
    }
    */
}
