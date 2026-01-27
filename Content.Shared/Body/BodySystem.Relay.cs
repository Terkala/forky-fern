using Content.Shared.Body.Events;
using Content.Shared.Gibbing;
using Content.Shared.Medical;

namespace Content.Shared.Body;

public sealed partial class BodySystem
{
    private void InitializeRelay()
    {
        SubscribeLocalEvent<BodyComponent, ApplyMetabolicMultiplierEvent>(RefRelayBodyEvent);
        SubscribeLocalEvent<BodyComponent, TryVomitEvent>(RefRelayBodyEvent);
        SubscribeLocalEvent<BodyComponent, BeingGibbedEvent>(OnBodyBeingGibbedRelay);
    }

    private void RefRelayBodyEvent<T>(EntityUid uid, BodyComponent component, ref T args) where T : struct
    {
        RelayEvent((uid, component), ref args);
    }

    /// <summary>
    /// Special handler for BeingGibbedEvent that relays to organs and then raises BodyBeingGibbedEvent.
    /// This avoids using Unsafe.As which is not allowed in RobustToolbox's sandboxed environment.
    /// </summary>
    private void OnBodyBeingGibbedRelay(EntityUid uid, BodyComponent component, ref BeingGibbedEvent args)
    {
        // First relay to organs
        RelayEvent((uid, component), ref args);
        
        // After relaying BeingGibbedEvent, raise BodyBeingGibbedEvent for other systems
        // This allows systems like DetachedBodyPartSystem to react to body gibbing
        // without conflicting with the relay subscription. The event is raised during
        // BeingGibbedEvent handling, before the gib completes, ensuring body parts
        // can be detached successfully.
        // Note: We pass args directly - since BeingGibbedEvent contains a HashSet (reference type),
        // modifications to Giblets in the event handler will be automatically reflected in args
        var bodyEv = new BodyBeingGibbedEvent((uid, component), args);
        RaiseLocalEvent(uid, ref bodyEv);
        
        // The HashSet reference is shared, so modifications in bodyEv.GibbingEvent.Giblets
        // are already reflected in args.Giblets - no need to copy back
    }

    private void RelayBodyEvent<T>(EntityUid uid, BodyComponent component, T args) where T : class
    {
        RelayEvent((uid, component), args);
    }

    public void RelayEvent<T>(Entity<BodyComponent> ent, ref T args) where T : struct
    {
        var ev = new BodyRelayedEvent<T>(ent, args);
        foreach (var organ in ent.Comp.Organs?.ContainedEntities ?? [])
        {
            RaiseLocalEvent(organ, ref ev);
        }
        args = ev.Args;
    }

    public void RelayEvent<T>(Entity<BodyComponent> ent, T args) where T : class
    {
        var ev = new BodyRelayedEvent<T>(ent, args);
        foreach (var organ in ent.Comp.Organs?.ContainedEntities ?? [])
        {
            RaiseLocalEvent(organ, ref ev);
        }
    }
}

/// <summary>
/// Event wrapper for relayed events.
/// </summary>
[ByRefEvent]
public record struct BodyRelayedEvent<TEvent>(Entity<BodyComponent> Body, TEvent Args);
