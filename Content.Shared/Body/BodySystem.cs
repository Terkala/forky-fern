using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.DragDrop;
using Robust.Shared.Containers;

namespace Content.Shared.Body;

public sealed partial class BodySystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;

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
        if (!TryComp<BodyPartComponent>(args.BodyPart, out var bodyPart))
            return;

        // Raise general detachment event for all systems
        var detachingEv = new BodyPartDetachingEvent(ent, (args.BodyPart, bodyPart));
        RaiseLocalEvent(ent, ref detachingEv);

        // Raise specialized head detachment event if applicable
        if (bodyPart.PartType == BodyPartType.Head)
        {
            var headEv = new HeadDetachingEvent(ent, (args.BodyPart, bodyPart));
            RaiseLocalEvent(ent, ref headEv);
        }
    }

    private void OnBodyPartAddedToBody(Entity<BodyComponent> ent, ref BodyPartAddedToBodyEvent args)
    {
        if (!TryComp<BodyPartComponent>(args.BodyPart, out var bodyPart))
            return;

        // Raise general attachment event for all systems
        var attachingEv = new BodyPartAttachingEvent(ent, (args.BodyPart, bodyPart));
        RaiseLocalEvent(ent, ref attachingEv);

        // Raise specialized head attachment event if applicable
        if (bodyPart.PartType == BodyPartType.Head)
        {
            var headEv = new HeadAttachingEvent(ent, (args.BodyPart, bodyPart));
            RaiseLocalEvent(ent, ref headEv);
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
