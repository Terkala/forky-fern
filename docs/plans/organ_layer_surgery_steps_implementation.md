# Organ Layer Surgery Steps Implementation Plan

## Overview

This plan implements the next step of the Medical Surgery Cybernetics reimplementation: **organ removal and insertion surgery steps** via the Health Analyzer. It builds on the existing foundation (organs in torso, organ referencing, integrity penalties, basic surgery steps) and is self-contained and fully testable.

**Scope:** Organ layer surgery steps only. No Integrity cost checks, no Cybernetics, no later stages.

**Reference:** Design document (Integrity-Based Medical and Cybernetics System), Plan (medical_surgery_cybernetics_reimplementation_4f9b4f86)

---

## Current State Summary

- Organs placed inside torso; BodyPartOrganSystem manages containers
- OrganInsertRequestEvent / OrganRemoveRequestEvent exist and work
- Integrity penalty system (SurgeryPenaltyAppliedEvent, IntegrityPenaltyAggregatorSystem)
- Health Analyzer: Health / Integrity / Surgery modes, body part diagram, layer tabs (Skin/Tissue/Organ)
- Basic steps: RetractSkin, RetractTissue, SawBones (Skin/Tissue layers)
- SurgeryRequestBuiMessage → HealthAnalyzerSystem → SurgeryRequestEvent → SurgerySystem
- SurgeryDoAfter completion applies layer state and penalty
- Integration tests: IntegrityPenaltyIntegrationTest, SurgeryRetractSkinIntegrationTest, OrganInsertRemoveIntegrationTest, SurgeryBodyPartDiagramIntegrationTest

---

## Design Principles (from main plan)

- Event-driven: Systems raise events; exactly one handler per event
- Clear edges: SurgerySystem handles SurgeryRequestEvent; BodyPartOrganSystem handles organ events
- Health Scanner is surgery entry point

---

## Implementation Tasks

### 1. Extend Messages and Events for Organ Steps

**1.1 SurgeryRequestBuiMessage** (`Content.Shared/MedicalScanner/SurgeryRequestBuiMessage.cs`)

- Add optional `NetEntity? Organ` (null for non-organ steps)
- Constructor: add optional parameter `NetEntity? organ = null`

**1.2 SurgeryRequestEvent** (`Content.Shared/Medical/Surgery/Events/SurgeryRequestEvent.cs`)

- Add optional `EntityUid? Organ` (null for non-organ steps)
- Constructor: add optional parameter

**1.3 SurgeryDoAfterEvent** (`Content.Shared/Medical/Surgery/Events/SurgeryDoAfterEvent.cs`)

- Add optional `NetEntity? Organ` for organ removal/insertion steps
- Constructor overload: `SurgeryDoAfterEvent(NetEntity bodyPart, string stepId, NetEntity? organ = null)`
- Clone: include Organ

---

### 2. Organ Slot System (Body Part Slots)

Body parts remember which organ categories they can host, even when empty. A human torso has slots for Heart, Lungs, Liver, etc.; limbs have slots for hands/feet only. Limbs cannot receive hearts or lungs.

**2.1 BodyPartComponent** (`Content.Shared/Body/Components/BodyPartComponent.cs`) – Add `List<ProtoId<OrganCategoryPrototype>> Slots`

**2.2 Species prototypes** (`Resources/Prototypes/Body/Species/*.yml`) – Add `slots` to body part components (e.g. OrganHumanTorso: slots: [Heart, Lungs, Stomach, Liver, Kidneys, Appendix])

**2.3 BodyPartOrganSystem** (`Content.Shared/Body/Systems/BodyPartOrganSystem.cs`) – OnOrganInsertRequest: check body part has slot for organ category; if slot already filled, set Success = false and return (do not move organ)

---

### 3. Add Surgery Step Prototypes for Organ Layer

**3.1 New prototypes** (`Resources/Prototypes/Medical/Surgery/surgery_steps.yml`) – **No requiredToolTag** (one hand holds health analyzer, other holds organ; prior steps ensure surgical tools were used)

```yaml
- type: surgeryStep
  id: RemoveOrgan
  layer: Organ
  name: health-analyzer-surgery-step-remove-organ
  penalty: 0
  procedureTypeIndex: 0
  doAfterDelay: 3

- type: surgeryStep
  id: InsertOrgan
  layer: Organ
  name: health-analyzer-surgery-step-insert-organ
  penalty: 0
  procedureTypeIndex: 0
  doAfterDelay: 3
```

**3.2 Localization** – Add step names and error message keys: organ no longer there, organ no longer in hand, slot already filled, surgical process invalid (limb severed)

---

### 4. Extend UI State with Organs and Empty Slots Per Body Part

**4.1 SurgeryLayerStateData** (`Content.Shared/MedicalScanner/HealthAnalyzerScannedUserMessage.cs`)

- Add `List<OrganInBodyPartData> Organs` – organs in this body part
- Add `List<string> EmptySlots` – organ category IDs for slots that have no organ (for insertion UI)
- Add struct `OrganInBodyPartData` with `NetEntity Organ`, `string? CategoryId`; mark `[Serializable, NetSerializable]`

**4.2 HealthAnalyzerSystem.GetHealthAnalyzerUiState** (`Content.Server/Medical/HealthAnalyzerSystem.cs`)

- Populate `Organs` (contained organs) and `EmptySlots` (slots with no organ) for each body part

---

### 5. Extend SurgerySystem for Organ Steps

**5.1 OnSurgeryRequest** (`Content.Shared/Medical/Surgery/SurgerySystem.cs`)

- For `RemoveOrgan`:
  - Require `args.Organ` (reject if null)
  - Validate organ exists and is in `args.BodyPart` (query body part’s container)
  - Validate organ layer is open (SkinRetracted, TissueRetracted, BonesSawed on body part)
  - Start DoAfter with `SurgeryDoAfterEvent(bodyPart, stepId, organ)`
- For `InsertOrgan`:
  - Require `args.Organ` (reject if null)
  - Validate organ is in user’s hand (SharedHandsSystem)
  - Validate organ is not already in a body (OrganComponent.Body == null)
  - Validate body part has empty slot for organ category (slot-based)
  - Validate organ layer is open on body part
  - Start DoAfter with `SurgeryDoAfterEvent(bodyPart, stepId, organ)`

**5.2 OnSurgeryDoAfter** – validate surgical process at completion before applying effects

- For `RemoveOrgan`:
  - Resolve `args.Organ` to entity
- For `InsertOrgan`:
  - Resolve `args.Organ` to entity
  - If exists and still in user’s hand (or validate), raise `OrganInsertRequestEvent(bodyPart, organ)`

- Common: Re-check body part exists, body part still in body (not severed), layer open. If invalid, show error popup and return.
- RemoveOrgan: If organ missing or no longer in body part: show error popup, return. Else raise OrganRemoveRequestEvent on organ entity.
- InsertOrgan: If organ missing or no longer in hand: show error popup, return. If slot already filled: show error popup, return. Else raise OrganInsertRequestEvent on body part entity; if Success == false, show error popup.

---

### 5. Extend HealthAnalyzerControl for Organ Steps

**5.1 UpdateSurgerySteps** (`Content.Client/HealthAnalyzer/UI/HealthAnalyzerControl.xaml.cs`)

- When `_selectedLayer == SurgeryLayer.Organ` and `layerState.BonesSawed`:
  - **Removal:** For selected body part’s `Organs` (from `_state.BodyPartLayerState`), add "Remove {organ name}" button per organ. Use stepId `"RemoveOrgan"`, pass `organ` in `SurgeryRequestBuiMessage`.
  - **Insertion:** For each organ in local player hands with OrganComponent not in body: only show "Insert {organ name}" if EmptySlots contains the organ category’s (slot-based; limbs cannot receive hearts/lungs).

**5.2 AddStepButton overload**

- Add overload or extend `AddStepButton` to accept optional `NetEntity? organ` and include it in `SurgeryRequestBuiMessage`.

**5.3 SurgeryRequestBuiMessage construction**

- When organ is provided, pass it to the message constructor.

---

### 6. HealthAnalyzerSystem.OnSurgeryRequest Relay

**6.1** (`Content.Server/Medical/HealthAnalyzerSystem.cs`)

- When relaying `SurgeryRequestBuiMessage` to `SurgeryRequestEvent`, include `args.Organ` (convert NetEntity to EntityUid via `GetEntity`).

---

### 7. Integration Test

**7.1 New test** (`Content.IntegrationTests/Tests/Medical/OrganRemovalSurgeryIntegrationTest.cs`)

- Extend InteractionTest or mirror SurgeryBodyPartDiagramIntegrationTest setup. Spawn surgeon, patient, analyzer, scalpel (SurgeryTool tag)
- Scan patient, open BUI
- Open organ layer: Send SurgeryRequestBuiMessage for RetractSkin, run DoAfter ticks; repeat for RetractTissue, then SawBones
- Send `SurgeryRequestBuiMessage` for RemoveOrgan with heart’s NetEntity
- Run ticks for DoAfter
- Assert heart is removed from body (not in BodySystem.GetAllOrgans(patient))
- Assert heart entity exists (in container or dropped)

**7.2 Organ insertion test** (same file)

- After removal: surgeon picks up heart (HandSys.TryPickupAnyHand)
- Send SurgeryRequestBuiMessage for InsertOrgan with target, torso, organ: heartNet
- Run ticks for DoAfter
- Assert heart is back in body (BodySystem.GetAllOrgans contains heart)

---

## File Summary

| File | Action |
|------|--------|
| `Content.Shared/MedicalScanner/SurgeryRequestBuiMessage.cs` | Add Organ |
| `Content.Shared/Medical/Surgery/Events/SurgeryRequestEvent.cs` | Add Organ |
| `Content.Shared/Medical/Surgery/Events/SurgeryDoAfterEvent.cs` | Add Organ |
| `Content.Shared/Body/Components/BodyPartComponent.cs` | Add Slots |
| `Content.Shared/Body/Systems/BodyPartOrganSystem.cs` | Slot validation on insert |
| `Content.Shared/MedicalScanner/HealthAnalyzerScannedUserMessage.cs` | Add Organs, EmptySlots |
| `Content.Server/Medical/HealthAnalyzerSystem.cs` | Populate Organs, EmptySlots; relay Organ |
| `Content.Shared/Medical/Surgery/SurgerySystem.cs` | Handle RemoveOrgan, InsertOrgan; DoAfter validation; error popups |
| `Content.Client/HealthAnalyzer/UI/HealthAnalyzerControl.xaml.cs` | Organ step buttons; filter insertion by EmptySlots |
| `Resources/Prototypes/Medical/Surgery/surgery_steps.yml` | Add RemoveOrgan, InsertOrgan (no requiredToolTag) |
| `Resources/Prototypes/Body/Species/*.yml` | Add slots to body parts |
| `Resources/Locale/en/medical.ftl` | Add step names (if needed) |
| `Content.IntegrationTests/Tests/Medical/OrganRemovalSurgeryIntegrationTest.cs` | New integration test |

---

## Validation Checklist

- [ ] Organ removal: UI shows "Remove Heart" etc. when organ layer open on torso
- [ ] Organ removal: DoAfter completes, OrganRemoveRequestEvent raised, organ removed
- [ ] Organ insertion: UI shows "Insert Heart" when holding heart and organ layer open
- [ ] Organ insertion: DoAfter completes, OrganInsertRequestEvent raised, organ in torso
- [ ] Reject removal when organ not in body part; show error popup when organ gone at DoAfter completion
- [ ] Reject insertion when organ not in hand or slot already filled; show error popup; do not move organ
- [ ] DoAfter validation: fail with error popup if body part severed (e.g. limb removed) at completion
- [ ] Organ slots: limbs cannot receive hearts/lungs; insertion only for empty slots
- [ ] Integration test passes
- [ ] No SPDX headers added to new/modified files per user request

---

## Out of Scope (Explicitly Excluded)

- Integrity cost check before organ install (Stage 4)
- Cybernetics maintenance (Stage 5)
- Cybernetics stats (Stage 6)
- Crude surgery (bone smashing)
- Equipment quality modifiers
- Medical job skills
