# Body Part System Architecture Analysis

## Executive Summary

This document analyzes the current implementation of the medical surgery and body part system, compares it to a proposed event-based architecture, and provides recommendations for improving maintainability while preventing duplicate subscription errors.

## Current Implementation Analysis

### Event Flow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    SURGERY DETACHMENT FLOW                      │
└─────────────────────────────────────────────────────────────────┘

1. SurgerySystem.DetachBodyPart()
   └─> ContainerSystem.Remove() 
       └─> EntRemovedFromContainerMessage (on BodyPartComponent)

2. SharedBodyPartSystem.OnBodyPartRemoved()
   ├─> Updates BodyPartComponent (Body=null, Parent=null)
   ├─> Raises BodyPartDetachedEvent (on body part entity)
   └─> Raises BodyPartRemovedFromBodyEvent (on body entity)

3. BodySystem.OnBodyPartRemovedFromBody()
   ├─> Direct call: ActionGrantSystem.RemoveSpeciesAbilitiesOnHeadDetach()
   ├─> Direct call: BodyPartAppearanceSystem.HandleBodyPartDetaching()
   └─> Raises BodyPartDetachingEvent (on body entity)

4. BodyPartSystem.OnBodyPartRemoved() [Server-side override]
   └─> Handles mind transfer to brain (if brain exists in part)

5. DetachedBodyPartSystem.OnBodyPartDetaching()
   └─> Spawns detached body part entity

6. BodyPartAppearanceSystem.OnBodyPartAttached()
   └─> Handles appearance changes (via BodyPartAttachedEvent)
```

### Current System Interactions

#### 1. **BodySystem** (Content.Shared/Body/BodySystem.cs)
**Responsibilities:**
- Container initialization (organs, root body parts)
- Organ insertion/removal event relaying
- **Body part attachment/detachment coordination** (current bottleneck)
- Direct calls to:
  - `ActionGrantSystem.RemoveSpeciesAbilitiesOnHeadDetach()`
  - `ActionGrantSystem.RestoreSpeciesAbilitiesOnHeadAttach()`
  - `BodyPartAppearanceSystem.HandleBodyPartDetaching()`
  - `BodyPartAppearanceSystem.HandleBodyPartAttaching()`

**Subscriptions:**
- `BodyPartRemovedFromBodyEvent` (on BodyComponent)
- `BodyPartAddedToBodyEvent` (on BodyComponent)

**Issues:**
- ❌ **Tight coupling**: Direct system calls create dependencies
- ❌ **Mixed responsibilities**: Coordinates multiple systems
- ❌ **Hard to extend**: Adding new systems requires modifying BodySystem

#### 2. **SharedBodyPartSystem** (Content.Shared/Body/Part/SharedBodyPartSystem.cs)
**Responsibilities:**
- Body part container management
- Body part attachment/detachment logic
- Event raising (BodyPartAttachedEvent, BodyPartDetachedEvent, BodyPartRemovedFromBodyEvent, BodyPartAddedToBodyEvent)

**Subscriptions:**
- `EntInsertedIntoContainerMessage` (on BodyPartComponent)
- `EntRemovedFromContainerMessage` (on BodyPartComponent)

**Issues:**
- ✅ Well-structured, minimal coupling

#### 3. **BodyPartSystem** (Content.Server/Body/Part/BodyPartSystem.cs)
**Responsibilities:**
- Server-side body part attachment/detachment
- **Mind transfer during detachment** (if brain in part)

**Subscriptions:**
- Inherits from SharedBodyPartSystem

**Issues:**
- ✅ Clean separation of concerns

#### 4. **BodyPartAppearanceSystem** (Content.Shared/Body/Part/BodyPartAppearanceSystem.cs)
**Responsibilities:**
- Humanoid appearance layer management
- Shows/hides visual layers when parts attach/detach

**Subscriptions:**
- `BodyPartAttachedEvent` (on BodyPartComponent) - for attachment
- **No subscription for detachment** - called directly by BodySystem

**Issues:**
- ⚠️ **Inconsistent**: Subscribes to attachment event but called directly for detachment
- ⚠️ **Mixed pattern**: Event subscription + direct calls

#### 5. **DetachedBodyPartSystem** (Content.Server/Body/Part/DetachedBodyPartSystem.cs)
**Responsibilities:**
- Spawns detached body part entities when parts are removed

**Subscriptions:**
- `BodyPartDetachingEvent` (on BodyComponent)

**Issues:**
- ✅ Clean event-based subscription

#### 6. **ActionGrantSystem** (Content.Shared/Actions/ActionGrantSystem.cs)
**Responsibilities:**
- Species ability management
- Removes/restores abilities when head detaches/attaches

**Subscriptions:**
- None (called directly by BodySystem)

**Issues:**
- ⚠️ **No event subscription**: Relies on direct calls from BodySystem
- ⚠️ **Tight coupling**: Cannot be extended without modifying BodySystem

#### 7. **BodyGibbingSystem** (Content.Server/Body/Systems/BodyGibbingSystem.cs)
**Responsibilities:**
- Mind transfer during gibbing events

**Subscriptions:**
- `BeingGibbedEvent` (on BodyPartComponent)

**Issues:**
- ✅ Clean event-based subscription

### Current Event Structure

```csharp
// Raised on body part entity
BodyPartAttachedEvent(EntityUid Body, EntityUid? Parent)
BodyPartDetachedEvent(EntityUid? Body, EntityUid? Parent)

// Raised on body entity
BodyPartAddedToBodyEvent(EntityUid BodyPart)
BodyPartRemovedFromBodyEvent(EntityUid BodyPart)

// Raised by BodySystem on body entity
BodyPartDetachingEvent(Entity<BodyComponent> Body, Entity<BodyPartComponent> BodyPart)
```

### Current Problems

1. **Duplicate Subscription Risk**
   - Multiple systems could subscribe to the same event on the same component
   - No clear ownership of event raising
   - Mixed patterns (events + direct calls)

2. **Tight Coupling**
   - BodySystem directly calls 4 different systems
   - Adding new functionality requires modifying BodySystem
   - Hard to test in isolation

3. **Inconsistent Patterns**
   - Some systems subscribe to events (DetachedBodyPartSystem)
   - Some systems are called directly (ActionGrantSystem, BodyPartAppearanceSystem)
   - Some systems do both (BodyPartAppearanceSystem)

4. **Maintainability Issues**
   - BodySystem has too many responsibilities
   - Changes to one system may require changes to BodySystem
   - Difficult to understand the full flow

## Proposed Future Implementation

### Design Principles

1. **Single Responsibility**: BodySystem only coordinates, doesn't implement
2. **Event-Driven**: All cross-system communication via events
3. **Minimal Coupling**: BodySystem has minimal knowledge of other systems
4. **Extensibility**: New systems can be added without modifying BodySystem
5. **No Duplicate Subscriptions**: Each event has a single, clear purpose

### Proposed Event Architecture

```csharp
// ============================================================
// CORE DETACHMENT EVENTS (Raised by BodySystem)
// ============================================================

/// <summary>
/// Raised by BodySystem when a body part is being detached.
/// This is the PRIMARY event for detachment logic.
/// Systems should subscribe to this instead of BodyPartRemovedFromBodyEvent.
/// </summary>
[ByRefEvent]
public readonly record struct BodyPartDetachingEvent(
    Entity<BodyComponent> Body, 
    Entity<BodyPartComponent> BodyPart
);

/// <summary>
/// Raised by BodySystem when a body part is being attached.
/// This is the PRIMARY event for attachment logic.
/// Systems should subscribe to this instead of BodyPartAddedToBodyEvent.
/// </summary>
[ByRefEvent]
public readonly record struct BodyPartAttachingEvent(
    Entity<BodyComponent> Body, 
    Entity<BodyPartComponent> BodyPart
);

// ============================================================
// SPECIALIZED EVENTS (Raised by BodySystem for specific concerns)
// ============================================================

/// <summary>
/// Raised by BodySystem when a head is being detached.
/// Specialized event for head-specific logic (species abilities, etc.)
/// </summary>
[ByRefEvent]
public readonly record struct HeadDetachingEvent(
    Entity<BodyComponent> Body, 
    Entity<BodyPartComponent> HeadPart
);

/// <summary>
/// Raised by BodySystem when a head is being attached.
/// Specialized event for head-specific logic (species abilities, etc.)
/// </summary>
[ByRefEvent]
public readonly record struct HeadAttachingEvent(
    Entity<BodyComponent> Body, 
    Entity<BodyPartComponent> HeadPart
);
```

### Proposed BodySystem Implementation

```csharp
public sealed partial class BodySystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    
    // NO dependencies on other systems!
    // All coordination happens via events

    private void OnBodyPartRemovedFromBody(Entity<BodyComponent> ent, ref BodyPartRemovedFromBodyEvent args)
    {
        if (!TryComp<BodyPartComponent>(args.BodyPart, out var bodyPart))
            return;

        // Raise general detachment event
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

        // Raise general attachment event
        var attachingEv = new BodyPartAttachingEvent(ent, (args.BodyPart, bodyPart));
        RaiseLocalEvent(ent, ref attachingEv);

        // Raise specialized head attachment event if applicable
        if (bodyPart.PartType == BodyPartType.Head)
        {
            var headEv = new HeadAttachingEvent(ent, (args.BodyPart, bodyPart));
            RaiseLocalEvent(ent, ref headEv);
        }
    }
}
```

### Proposed System Updates

#### 1. **BodyPartAppearanceSystem**
```csharp
public sealed class BodyPartAppearanceSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        
        // Subscribe to BodySystem events (not SharedBodyPartSystem events)
        SubscribeLocalEvent<BodyComponent, BodyPartDetachingEvent>(OnBodyPartDetaching);
        SubscribeLocalEvent<BodyComponent, BodyPartAttachingEvent>(OnBodyPartAttaching);
    }

    private void OnBodyPartDetaching(Entity<BodyComponent> ent, ref BodyPartDetachingEvent args)
    {
        HandleBodyPartDetaching(ent, args.BodyPart);
    }

    private void OnBodyPartAttaching(Entity<BodyComponent> ent, ref BodyPartAttachingEvent args)
    {
        HandleBodyPartAttaching(ent, args.BodyPart);
    }
}
```

#### 2. **ActionGrantSystem**
```csharp
public sealed class ActionGrantSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        
        // Subscribe to specialized head events
        SubscribeLocalEvent<BodyComponent, HeadDetachingEvent>(OnHeadDetaching);
        SubscribeLocalEvent<BodyComponent, HeadAttachingEvent>(OnHeadAttaching);
    }

    private void OnHeadDetaching(Entity<BodyComponent> ent, ref HeadDetachingEvent args)
    {
        RemoveSpeciesAbilitiesOnHeadDetach(ent, args.HeadPart);
    }

    private void OnHeadAttaching(Entity<BodyComponent> ent, ref HeadAttachingEvent args)
    {
        RestoreSpeciesAbilitiesOnHeadAttach(ent, args.HeadPart);
    }
}
```

#### 3. **DetachedBodyPartSystem** (No changes needed)
```csharp
// Already subscribes to BodyPartDetachingEvent - perfect!
SubscribeLocalEvent<BodyComponent, BodyPartDetachingEvent>(OnBodyPartDetaching);
```

### Event Subscription Matrix

| System | Detachment Event | Attachment Event | Specialized Events |
|--------|----------------|------------------|-------------------|
| **BodyPartAppearanceSystem** | `BodyPartDetachingEvent` | `BodyPartAttachingEvent` | None |
| **DetachedBodyPartSystem** | `BodyPartDetachingEvent` | None | None |
| **ActionGrantSystem** | None | None | `HeadDetachingEvent`, `HeadAttachingEvent` |
| **BodyPartSystem** (mind transfer) | Handles in `OnBodyPartRemoved` | None | None |

### Benefits of Proposed Architecture

1. **✅ No Duplicate Subscriptions**
   - Each event has a single, clear purpose
   - BodySystem is the only system raising these events
   - Clear ownership prevents conflicts

2. **✅ Minimal BodySystem Changes**
   - Only adds event raising logic
   - No direct system calls
   - No dependencies on other systems

3. **✅ Maintainability**
   - Single connection point: BodySystem raises events
   - Systems are decoupled
   - Easy to add new systems without modifying BodySystem

4. **✅ Extensibility**
   - New systems can subscribe to events
   - No need to modify BodySystem
   - Clear extension points

5. **✅ Consistent Pattern**
   - All systems use event subscriptions
   - No mixed patterns
   - Predictable behavior

### Migration Path

1. **Phase 1: Add New Events**
   - Create `BodyPartDetachingEvent`, `BodyPartAttachingEvent`
   - Create `HeadDetachingEvent`, `HeadAttachingEvent`
   - Keep existing events for backwards compatibility

2. **Phase 2: Update BodySystem**
   - Remove direct system calls
   - Add event raising logic
   - Remove dependencies on other systems

3. **Phase 3: Update Subscriber Systems**
   - BodyPartAppearanceSystem: Subscribe to new events
   - ActionGrantSystem: Subscribe to head events
   - DetachedBodyPartSystem: Already correct

4. **Phase 4: Remove Old Events (Optional)**
   - Deprecate `BodyPartRemovedFromBodyEvent` for cross-system communication
   - Keep for internal SharedBodyPartSystem use if needed

### Comparison Table

| Aspect | Current | Proposed |
|--------|---------|----------|
| **BodySystem Dependencies** | 1 (ActionGrantSystem) | 0 |
| **Direct System Calls** | 4 | 0 |
| **Event Types** | 5 | 4 (new) |
| **Connection Points** | 2 (OnBodyPartRemovedFromBody, OnBodyPartAddedToBody) | 2 (same) |
| **Duplicate Subscription Risk** | High | None |
| **Extensibility** | Low (requires BodySystem changes) | High (event subscription only) |
| **Maintainability** | Medium (mixed patterns) | High (consistent pattern) |
| **Testability** | Low (tight coupling) | High (loose coupling) |

## Recommendations

### Immediate Actions

1. **Implement Proposed Architecture**
   - Create new event types
   - Update BodySystem to raise events only
   - Update subscriber systems to use events

2. **Document Event Ownership**
   - Clearly document which system raises which events
   - Add XML comments explaining event purpose
   - Create event flow diagrams

3. **Add Event Validation**
   - Consider adding validation to ensure events are raised correctly
   - Add logging for event flow debugging

### Long-Term Improvements

1. **Event Ordering**
   - Consider using event ordering attributes if order matters
   - Document expected execution order

2. **Event Cancellation**
   - Consider adding cancellation support if needed
   - Allow systems to prevent detachment/attachment

3. **Performance Optimization**
   - Cache system lookups if needed
   - Consider event batching for bulk operations

## Conclusion

The proposed event-based architecture provides:
- **Zero duplicate subscription risk** through clear event ownership
- **Minimal BodySystem changes** (only event raising)
- **High maintainability** through consistent patterns and loose coupling
- **Easy extensibility** without modifying core systems

The migration is straightforward and can be done incrementally while maintaining backwards compatibility.
