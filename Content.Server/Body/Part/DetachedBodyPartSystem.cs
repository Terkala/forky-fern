using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
// using Robust.Server.GameObjects; // SpriteSystem is client-only
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

    public override void Initialize()
    {
        base.Initialize();
        
        // Subscribe to BodyPartDetachingEvent to spawn detached entities
        // This is safe because BodySystem is the only one raising this event
        SubscribeLocalEvent<BodyComponent, BodyPartDetachingEvent>(OnBodyPartDetaching);
    }

    private void OnBodyPartDetaching(Entity<BodyComponent> ent, ref BodyPartDetachingEvent args)
    {
        HandleBodyPartDetaching(ent, args.BodyPart);
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
