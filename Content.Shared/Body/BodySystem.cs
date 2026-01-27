using Content.Shared.Actions;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.DragDrop;
using Robust.Shared.Containers;

namespace Content.Shared.Body;

public sealed partial class BodySystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ActionGrantSystem _actionGrant = default!;

    private EntityQuery<BodyComponent> _bodyQuery;
    private EntityQuery<OrganComponent> _organQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, ComponentInit>(OnBodyInit);
        SubscribeLocalEvent<BodyComponent, ComponentShutdown>(OnBodyShutdown);

        SubscribeLocalEvent<BodyComponent, CanDragEvent>(OnCanDrag);

        SubscribeLocalEvent<BodyComponent, EntInsertedIntoContainerMessage>(OnBodyEntInserted);
        SubscribeLocalEvent<BodyComponent, EntRemovedFromContainerMessage>(OnBodyEntRemoved);
        // Subscribe to body part removal events to raise BodyPartDetachingEvent
        SubscribeLocalEvent<BodyComponent, BodyPartRemovedFromBodyEvent>(OnBodyPartRemovedFromBody);
        // Subscribe to body part addition events to handle species ability restoration
        SubscribeLocalEvent<BodyComponent, BodyPartAddedToBodyEvent>(OnBodyPartAddedToBody);

        _bodyQuery = GetEntityQuery<BodyComponent>();
        _organQuery = GetEntityQuery<OrganComponent>();

        InitializeRelay();
    }

    private void OnBodyInit(Entity<BodyComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Organs =
            _container.EnsureContainer<Container>(ent, BodyComponent.ContainerID);
        
        // Initialize root body parts container
        ent.Comp.RootBodyParts =
            _container.EnsureContainer<Container>(ent, Content.Shared.Body.Part.SharedBodyPartSystem.BodyRootContainerId);
        
        // Raise event to allow other systems to perform additional initialization
        var ev = new BodyInitializedEvent(ent);
        RaiseLocalEvent(ent, ref ev);
        
        // Raise event for organ placement initialization
        var organEv = new BodyOrganPlacementInitializedEvent(ent);
        RaiseLocalEvent(ent, ref organEv);
    }

    private void OnBodyShutdown(Entity<BodyComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Organs is { } organs)
            _container.ShutdownContainer(organs);
        
        if (ent.Comp.RootBodyParts is { } rootParts)
            _container.ShutdownContainer(rootParts);
    }

    private void OnBodyEntInserted(Entity<BodyComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != BodyComponent.ContainerID)
            return;

        if (!_organQuery.TryComp(args.Entity, out var organ))
            return;

        var body = new OrganInsertedIntoEvent(args.Entity);
        RaiseLocalEvent(ent, ref body);

        var ev = new OrganGotInsertedEvent(ent);
        RaiseLocalEvent(args.Entity, ref ev);

        if (organ.Body != ent)
        {
            organ.Body = ent;
            Dirty(args.Entity, organ);
        }
    }

    private void OnBodyEntRemoved(Entity<BodyComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        // Handle organ removal from body container
        if (args.Container.ID != BodyComponent.ContainerID)
            return;

        if (!_organQuery.TryComp(args.Entity, out var organ))
            return;

        var body = new OrganRemovedFromEvent(args.Entity);
        RaiseLocalEvent(ent, ref body);

        var ev = new OrganGotRemovedEvent(ent);
        RaiseLocalEvent(args.Entity, ref ev);

        if (organ.Body == null)
            return;

        organ.Body = null;
        Dirty(args.Entity, organ);
    }

    private void OnBodyPartRemovedFromBody(Entity<BodyComponent> ent, ref BodyPartRemovedFromBodyEvent args)
    {
        // Check if a head is being detached and remove species abilities
        if (TryComp<BodyPartComponent>(args.BodyPart, out var bodyPart))
        {
            if (bodyPart.PartType == BodyPartType.Head)
            {
                _actionGrant.RemoveSpeciesAbilitiesOnHeadDetach(ent, (args.BodyPart, bodyPart));
            }
            
            // Handle appearance changes (hide visual layers)
            var appearanceSystem = EntityManager.System<BodyPartAppearanceSystem>();
            appearanceSystem.HandleBodyPartDetaching(ent, (args.BodyPart, bodyPart));
            
            // Raise event for body part detachment
            // This handles body parts removed from both root container and parent body parts
            // DetachedBodyPartSystem (server-only) will subscribe to this event to spawn detached entities
            var detachingEv = new BodyPartDetachingEvent(ent, (args.BodyPart, bodyPart));
            RaiseLocalEvent(ent, ref detachingEv);
        }
    }

    private void OnBodyPartAddedToBody(Entity<BodyComponent> ent, ref BodyPartAddedToBodyEvent args)
    {
        if (TryComp<BodyPartComponent>(args.BodyPart, out var bodyPart))
        {
            // Check if a head is being attached and restore species abilities
            if (bodyPart.PartType == BodyPartType.Head)
            {
                _actionGrant.RestoreSpeciesAbilitiesOnHeadAttach(ent, (args.BodyPart, bodyPart));
            }
            
            // Handle appearance changes (show visual layers)
            var appearanceSystem = EntityManager.System<BodyPartAppearanceSystem>();
            appearanceSystem.HandleBodyPartAttaching(ent, (args.BodyPart, bodyPart));
        }
    }

    private void OnCanDrag(Entity<BodyComponent> ent, ref CanDragEvent args)
    {
        args.Handled = true;
    }

    /// <summary>
    /// Gets all body parts attached to a body.
    /// </summary>
    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildren(EntityUid body, BodyComponent? bodyComp = null)
    {
        if (!Resolve(body, ref bodyComp, false))
            yield break;

        var bodyPartSystem = EntitySystem.Get<SharedBodyPartSystem>();
        foreach (var part in bodyPartSystem.GetBodyChildren(body, bodyComp))
        {
            yield return part;
        }
    }

    /// <summary>
    /// Gets body parts of a specific type and symmetry.
    /// </summary>
    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildrenOfType(
        EntityUid body,
        BodyPartType type,
        BodyComponent? bodyComp = null,
        BodyPartSymmetry? symmetry = null)
    {
        if (!Resolve(body, ref bodyComp, false))
            yield break;

        var bodyPartSystem = EntitySystem.Get<SharedBodyPartSystem>();
        foreach (var part in bodyPartSystem.GetBodyChildrenOfType(body, type, bodyComp, symmetry))
        {
            yield return part;
        }
    }
}
