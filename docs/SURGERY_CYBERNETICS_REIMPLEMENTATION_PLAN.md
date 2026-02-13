# Surgery and Cybernetics Re-Implementation Plan

## Part 1: Current Implementation Summary

### 1.1 Architecture Overview

The current implementation spans multiple systems with tight coupling:

```
BodyComponent (body entity)
├── Organs container (legacy - organs migrated to body parts)
├── RootBodyParts container → torso, head
└── BodyPartSystem manages attachment via SharedBodyPartSystem

BodyPartComponent (body part entity)
├── Body (ref to body)
├── Parent (ref to parent body part)
├── Organs container (organs live HERE in body parts)
└── Attached via containers on parent part or body
```

**Key Coupling Issue**: `BodySystem` and `SharedBodyPartSystem` directly manipulate `BodyComponent`, `BodyPartComponent`, and containers. Other systems (Surgery, Integrity, Cybernetics) depend on these components for queries rather than subscribing to events.

---

### 1.2 Body System – Order of Operations

**Body Initialization (BodyPartInitializationSystem):**
1. `BodyComponent.ComponentInit` → BodySystem creates `Organs` and `RootBodyParts` containers
2. `BodyInitializedEvent` raised → BodyPartInitializationSystem responds
3. BodyPartInitializationSystem looks up `SpeciesPrototype.BodyPartStructure`
4. Spawns body parts from `BodyPartStructurePrototype`, inserts into containers
5. Migrates legacy organs from `BodyComponent.Organs` to `BodyPartComponent.Organs`

**Body Part Attachment (SharedBodyPartSystem):**
1. Entity inserted into container (body root or parent part slot)
2. `EntInsertedIntoContainerMessage` → SharedBodyPartSystem.OnBodyPartInserted
3. Slime check: blocks manual limb attachment to slimes (except RegeneratingLimbComponent)
4. Sets `Body`, `Parent`, `SlotId` on BodyPartComponent
5. Raises `BodyPartAttachedEvent` on body part
6. Raises `BodyPartAddedToBodyEvent` on body
7. BodySystem.OnBodyPartAddedToBody → raises `BodyPartAttachingEvent`, `HeadAttachingEvent`, or `CyberLimbAttachedEvent`

**Body Part Detachment:**
1. Entity removed from container
2. `EntRemovedFromContainerMessage` → SharedBodyPartSystem.OnBodyPartRemoved
3. Clears `Body`, `Parent`, `SlotId`
4. Raises `BodyPartDetachedEvent` on body part
5. Raises `BodyPartRemovedFromBodyEvent` on body
6. BodySystem.OnBodyPartRemovedFromBody → raises `BodyPartDetachingEvent`, etc.

**Organ Storage:**
- Organs stored in `BodyPartComponent.Organs` (not BodyComponent.Organs)
- BodySystem still has `Organs` container but BodyPartInitializationSystem migrates organs to parts
- OrganComponent.Body set by BodySystem when inserted into body container (legacy path)

---

### 1.3 Surgery System – Order of Operations

**Entry Point:**
1. Verb on body part with SurgeryLayerComponent: "Open Surgery" (requires surgical tool or slashing weapon)
2. Verb on body with SurgeryTargetComponent: "Open Surgery" (finds first body part, opens UI)
3. `OpenSurgeryUI` → UI registered on body entity, state sent to client

**UI Flow:**
1. `BoundUIOpenedEvent` → starts material scanning
2. Client sends `SurgeryHandItemsMessage` with held items
3. `SurgeryBodyPartSelectedMessage` → updates selected body part
4. `SurgeryLayerChangedMessage` → updates layer (Skin/Tissue/Organ)
5. `SurgeryStepSelectedMessage` → validates step, starts DoAfter
6. `SurgeryOperationMethodSelectedMessage` → primary vs improvised method

**Step Execution (OnSurgeryDoAfter):**
1. DoAfter completes → `ExecuteStep` called
2. **Operation handling**: SawBones, OpenMaintenancePanel, repair operations
3. **Improvised penalties**: ApplyImprovisedIntegrityCost (disabled - ImprovisedComponentMap commented out)
4. **Step effects**: Add/Remove components from body part
5. **HandleImplantAndOrganOperations**: RemoveImplant, AddImplant, RemoveOrgan, InsertOrgan, AttachLimb
6. **HandleCyberneticsMaintenanceSteps**: Panel state changes
7. **Layer state**: ApplyLayerStateChanges from YAML
8. **Penalties**: ApplySurgeryPenalty, RemoveSurgeryPenalty
9. **TrackStepProgress**: SurgeryStepProgressComponent for sequences
10. **UpdateUI**: Regenerate steps, send state

**Organ/Limb Installation (TryInstallImplant):**
1. Slime check: no limb implant, only core organ
2. Calculate integrity cost via `CalculateIntegrityCost`
3. Add `AppliedIntegrityCostComponent` to item
4. `_integrity.AddIntegrityUsage(body, cost)`
5. **Organ**: `_containers.Insert` into `BodyPartComponent.Organs`
6. **Limb**: `_bodyPartSystem.AttachBodyPart(body, item, slotId, parentPart)`

**Dynamic Step Generation:**
- `GenerateDynamicSteps` → SkinSteps, TissueSteps, OrganSteps
- Based on: SurgeryLayerComponent state, SurgeryStepProgressComponent, hand items, organ slots
- Steps spawned from prototypes, cached in `_cachedStepData` for performance

---

### 1.4 Cybernetics System – Order of Operations

**CyberLimbComponent:**
- StorageContainer (references StorageComponent container)
- PanelExposed, PanelOpen (maintenance state)
- NextServiceTime

**Initialization (SharedBodyPartSystem.OnCyberLimbInit):**
1. Ensure StorageContainer exists
2. Ensure WiresPanelComponent exists

**CyberLimbStatsComponent (on body):**
- Cached stats: BatteryCapacity, CurrentBatteryCharge, ServiceTimeRemaining, Efficiency
- Updated by CyberLimbStatsSystem when cyber-limbs attach/detach

**Stats Calculation (Shared CyberLimbStatsSystem):**
- Subscribes to `CyberLimbAttachedEvent`, `CyberLimbDetachedEvent`
- `RecalculateStats` iterates body parts with CyberLimbComponent, sums module stats

**Server CyberLimbStatsSystem:**
- Every 1 second: drain battery, decrement service time
- When depleted: 50% efficiency penalty

**Surgery Penalties (SurgeryPenaltyQuerySystem):**
- GetTotalSurgeryPenaltyEvent → iterates body parts via GetBodyPartsEvent
- CyberLimbComponent.PanelOpen → +2, PanelExposed → +1
- IonDamagedComponent → adds BioRejectionPenalty

---

### 1.5 Integrity System – Order of Operations

**IntegrityComponent (on body):**
- MaxIntegrity, UsedIntegrity, TemporaryIntegrityBonus
- TargetBioRejection, CurrentBioRejection, NeedsUpdate

**RecalculateTargetBioRejection:**
1. overLimit = UsedIntegrity - effectiveMaxIntegrity
2. baseTargetBioRejection = overLimit * BioRejectionPerPoint
3. Raise GetTotalSurgeryPenaltyEvent → SurgeryPenaltyQuerySystem sums penalties from body parts
4. TargetBioRejection = baseTargetBioRejection + surgeryPenalty

**Integrity applies when:**
- AddIntegrityUsage (organ/limb/cybernetic install)
- RemoveIntegrityUsage (removal)
- Immunosuppressant adds TemporaryIntegrityBonus

---

### 1.6 Current BodyComponent Dependencies (Problems)

| System | Direct BodyComponent Usage | Event-Based? |
|--------|---------------------------|--------------|
| BodySystem | Manages Organs, RootBodyParts containers | Raises BodyInitializedEvent, BodyPartAttachingEvent |
| SharedBodyPartSystem | Uses GetBodyChildren (iterates BodyPartComponent.Body == body) | Raises BodyPartAttachedEvent, BodyPartAddedToBodyEvent |
| SurgerySystem | Gets body via part.Body, calls Body.GetBodyChildrenOfType | No events for "get body parts" |
| SurgeryPenaltyQuerySystem | Subscribes to BodyComponent + GetTotalSurgeryPenaltyEvent | Uses GetBodyPartsEvent (BodySystem raises) |
| IntegritySystem | Operates on body entity directly | N/A |
| CyberLimbStatsSystem | Subscribes to CyberLimbAttachedEvent (BodySystem raises) | Event-driven |

**Core Issue**: `GetBodyChildren` and `GetBodyChildrenOfType` are methods on BodySystem/SharedBodyPartSystem that iterate entities with `BodyPartComponent.Body == body`. Any system needing body parts must call these methods or use GetBodyPartsEvent. The BodyComponent itself doesn't provide functionality—it's the BodyPartComponent references. But the **container hierarchy** is the source of truth; Body/Parent are derived from container insertion.

---

## Part 2: CyberMed Document Analysis

### 2.1 Design Principles (from Document)

1. **Integrity as Resource**: Organs, limbs, cybernetics consume integrity. Over-capacity = bio-rejection.
2. **3-Layer Surgery**: Skin → Tissue → Organ, with dynamic flowcharts.
3. **Surgery Penalties**: Retract skin +1, retract tissue +1, saw bones +8, smash bones +16.
4. **Crude Surgery**: Blunt damage alternative, 5-stage bone repair.
5. **Equipment Quality**: Tool/table quality affects integrity costs.
6. **Cyber-Limb Storage**: 2×3 grid, non-stacking, maintenance panel gated.
7. **Module Types**: Battery, MatterBin, Manipulator, Capacitor, Special (Jaws of Life, etc.).
8. **Shared Resource Pool**: Battery and service time averaged across all cyber-limbs.
9. **6-Step Maintenance**: Expose → Open → Adjust → Replace Wiring → (Optional Battery) → Close → Seal.
10. **Slime Restrictions**: Core-only organs, no limb implants, limb regeneration.
11. **Health Analyzer**: Health / Integrity / Surgery modes.
12. **Performance**: Cached stats, infrequent updates (1s), calculate at surgery time.

### 2.2 Divergence from Current Implementation

- **Surgery UI**: Document says surgery via Health Analyzer; current uses right-click verb on body/part.
- **Organ Slots**: Document implies organs have slots; current uses single Organs container per part.
- **CyberLimb Maintenance**: Document specifies 6-step procedure; current has HandleCyberneticsMaintenanceSteps with step ID pattern matching.
- **Unskilled Penalties**: Document has +2 for non-medical; current has UnskilledSurgeryPenaltyComponent but logic commented out.

---

## Part 3: Re-Implementation Plan (Event-Driven, Testable Stages)

### Guiding Principle

**Replace direct BodyComponent/BodyPartComponent queries with events.** Systems that need body structure should receive events or raise query events. The "body" becomes an event source; components that care (Integrity, Cybernetics, Surgery) subscribe.

---

### Stage 1: Event Foundation (No Surgery/Cybernetics Changes)

**Goal**: Introduce events for all body structure queries. Body/body part systems raise them; consumers subscribe.

**Deliverables:**
1. **GetBodyPartsEvent** (exists, ensure it's the canonical query)
   - Raised by: Consumer on body entity
   - Handled by: BodySystem (or dedicated handler) populates Parts list
   - All "get body parts" goes through this

2. **GetBodyPartAtSlotEvent** (new)
   - Request: body, slot/symmetry/type
   - Response: body part entity or null

3. **BodyPartSlotQueryEvent** (new)
   - Request: body part
   - Response: slot ID, parent part, body

4. **Refactor**: BodySystem.GetBodyChildren → raises GetBodyPartsEvent internally or is the handler
5. **Refactor**: SurgeryPenaltyQuerySystem, SurgerySystem, any direct Body.GetBodyChildrenOfType → use GetBodyPartsEvent

**Test**: Spawn body, raise GetBodyPartsEvent, verify correct parts returned. No surgery/cybernetics behavior change.

---

### Stage 2: Body Part Attachment Events

**Goal**: Attachment and detachment are fully event-driven. Container system remains, but logic responds to events.

**Deliverables:**
1. **BodyPartAttachmentRequestEvent** (new, cancellable)
   - Args: body, part, slotId, parentPart
   - Handled by: SharedBodyPartSystem performs container insert
   - Other systems can cancel (e.g., slime check)

2. **BodyPartDetachmentRequestEvent** (new, cancellable)
   - Args: body part
   - Handled by: SharedBodyPartSystem performs container remove

3. **BodyPartAttachedEvent** (exists) - raised after successful attach
4. **BodyPartDetachedEvent** (exists) - raised after successful detach

5. **Refactor**: SharedBodyPartSystem
   - OnBodyPartInserted: still handles container message, but could optionally validate via event
   - AttachBodyPart: raise BodyPartAttachmentRequestEvent, handler does actual insert

6. **Refactor**: Slime check moved to event handler (cancel request) rather than inside SharedBodyPartSystem

**Test**: Attach body part via event, verify container state and events. Detach, verify cleanup.

---

### Stage 3: Organ/Implant Events

**Goal**: Organ and implant insertion/removal go through events. No direct container manipulation from surgery.

**Deliverables:**
1. **OrganInsertRequestEvent** (new, cancellable)
   - Args: organ, body, targetPart, slot (optional)
   - Handled by: System that inserts into BodyPartComponent.Organs

2. **OrganRemoveRequestEvent** (new, cancellable)
   - Args: organ, body
   - Handled by: System that removes from container

3. **LimbInstallRequestEvent** (new, cancellable)
   - Args: limb, body, targetPart, slotId
   - Handled by: System that calls AttachBodyPart (or raises BodyPartAttachmentRequestEvent)

4. **LimbRemoveRequestEvent** (new, cancellable)
   - Args: limb (body part)
   - Handled by: System that detaches

5. **Refactor**: SurgerySystem.HandleImplantAndOrganOperations, TryInstallImplant, TryRemoveImplant
   - Replace direct container/AttachBodyPart calls with event raises

**Test**: Insert organ via event, verify in container. Remove organ. Install limb. Remove limb. All via events.

---

### Stage 4: Integrity and Surgery Penalty via Events

**Goal**: Integrity usage and surgery penalties are applied by subscribers to body part/organ events.

**Deliverables:**
1. **IntegrityCostQueryEvent** (new)
   - Raised when installing organ/limb/cybernetic
   - Args: item, body, tool, table
   - Handled by: SharedSurgerySystem.CalculateIntegrityCost logic (or IntegritySystem)
   - Returns: FixedPoint2 cost

2. **IntegrityUsageChangedEvent** (exists) - already raised by IntegritySystem

3. **Refactor**: SurgerySystem.TryInstallImplant
   - Raise IntegrityCostQueryEvent instead of calling CalculateIntegrityCost directly
   - AddIntegrityUsage remains on IntegritySystem

4. **SurgeryPenaltyEvent** (new)
   - Raised when step applies penalty: body part, amount
   - Handled by: System adds to SurgeryPenaltyComponent or equivalent

5. **Refactor**: SurgeryPenaltyQuerySystem - already uses GetTotalSurgeryPenaltyEvent; ensure GetBodyPartsEvent is used

**Test**: Install organ, verify integrity usage. Complete surgery step with penalty, verify penalty applied and included in GetTotalSurgeryPenalty.

---

### Stage 5: Surgery Step Execution via Events

**Goal**: Surgery steps do not directly modify body parts. They raise events; handlers perform effects.

**Deliverables:**
1. **SurgeryStepExecutingEvent** (new, cancellable)
   - Args: body part, step entity, step component, user
   - Raised before any effects
   - Handlers can cancel

2. **SurgeryStepCompletedEvent** (new)
   - Args: body part, step entity, step component, user
   - Raised after effects applied

3. **SurgeryLayerStateChangeRequestEvent** (new)
   - Args: body part, layer state changes (from SurgeryStepComponent.LayerStateChanges)
   - Handled by: System updates SurgeryLayerComponent

4. **SurgeryStepEffectRequestEvent** (new)
   - Args: body part, add components, remove components
   - Handled by: System adds/removes components

5. **Refactor**: SurgerySystem.ExecuteStep
   - Raise SurgeryStepExecutingEvent; if cancelled, return
   - For layer changes: raise SurgeryLayerStateChangeRequestEvent
   - For add/remove: raise SurgeryStepEffectRequestEvent
   - For organ/limb: raise OrganInsertRequestEvent, etc. (Stage 3)
   - Raise SurgeryStepCompletedEvent

**Test**: Execute retract skin step, verify SurgeryLayerComponent updated. Execute organ insert step, verify organ in container. All via events.

---

### Stage 6: Cybernetics Events

**Goal**: Cyber-limb stats, maintenance, and penalties are event-driven.

**Deliverables:**
1. **CyberLimbStatsRecalculateEvent** (new)
   - Raised on body when cyber-limbs attach/detach or modules change
   - Handled by: CyberLimbStatsSystem recalculates and updates CyberLimbStatsComponent

2. **CyberLimbMaintenanceStateChangeEvent** (new)
   - Args: cyber limb, PanelExposed, PanelOpen
   - Handled by: System updates CyberLimbComponent

3. **Refactor**: BodySystem.OnBodyPartAddedToBody / OnBodyPartRemovedFromBody
   - CyberLimbAttachedEvent / CyberLimbDetachedEvent already raised
   - CyberLimbStatsSystem subscribes; ensure it uses events only

4. **Refactor**: HandleCyberneticsMaintenanceSteps
   - Raise CyberLimbMaintenanceStateChangeEvent instead of directly setting component

5. **SurgeryPenaltyQuerySystem**: Already includes CyberLimbComponent panel state; no change needed if events update component

**Test**: Attach cyber limb, verify stats recalculated. Open maintenance panel via surgery step, verify PanelOpen set. Verify penalty in GetTotalSurgeryPenalty.

---

### Stage 7: Consolidation and Cleanup

**Goal**: Remove redundant code, ensure no direct BodyComponent logic for "functionality."

**Deliverables:**
1. Audit all BodyComponent usages: only init/shutdown, container creation, event raising
2. Audit all BodyPartComponent usages: only container insertion source, event raising
3. Remove any commented Shitmed code
4. Document event flow for future maintainers

**Test**: Full integration test: spawn human, perform full surgery sequence (retract skin, tissue, bones, insert organ, close), verify integrity and penalties. Attach cyber limb, open maintenance, verify stats.

---

## Part 4: Implementation Order Summary

| Stage | Focus | Testable Milestone |
|-------|-------|-------------------|
| 1 | Event foundation, GetBodyPartsEvent as canonical query | Body part iteration works via events |
| 2 | Body part attach/detach via requests | Attach/detach body parts via events |
| 3 | Organ/limb install/remove via events | Surgery installs organs/limbs via events |
| 4 | Integrity and penalties via events | Integrity and penalties applied by subscribers |
| 5 | Surgery step execution via events | Steps raise events, handlers perform effects |
| 6 | Cybernetics events | Cyber stats and maintenance via events |
| 7 | Consolidation | No direct BodyComponent functionality, full E2E test |

---

## Part 5: Event Reference (Proposed)

| Event | Raised By | Handled By | Purpose |
|-------|-----------|------------|---------|
| GetBodyPartsEvent | Consumer | BodySystem | Query body parts |
| GetBodyPartAtSlotEvent | Consumer | BodySystem | Query part by slot/type |
| BodyPartAttachmentRequestEvent | Surgery, etc. | SharedBodyPartSystem | Request attach |
| BodyPartDetachmentRequestEvent | Surgery, etc. | SharedBodyPartSystem | Request detach |
| OrganInsertRequestEvent | Surgery | Organ system | Install organ |
| OrganRemoveRequestEvent | Surgery | Organ system | Remove organ |
| LimbInstallRequestEvent | Surgery | SharedBodyPartSystem | Install limb |
| LimbRemoveRequestEvent | Surgery | SharedBodyPartSystem | Remove limb |
| IntegrityCostQueryEvent | Surgery | Integrity/cost system | Get cost for install |
| SurgeryStepExecutingEvent | SurgerySystem | (validation) | Pre-step validation |
| SurgeryStepCompletedEvent | SurgerySystem | (logging, etc.) | Post-step notification |
| SurgeryLayerStateChangeRequestEvent | SurgerySystem | Surgery handler | Update layer state |
| SurgeryStepEffectRequestEvent | SurgerySystem | Surgery handler | Add/remove components |
| CyberLimbStatsRecalculateEvent | Body, etc. | CyberLimbStatsSystem | Recalc stats |
| CyberLimbMaintenanceStateChangeEvent | SurgerySystem | Cyber handler | Panel state |

---

*Document generated for Forky re-implementation. Last updated: 2025-02-12*
