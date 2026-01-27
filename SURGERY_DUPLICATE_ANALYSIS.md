# Surgery System - Duplicate Functions and Generic Handler Opportunities

## Duplicate Patterns Found

### 1. Finding Body Part with SurgeryLayerComponent (Prefer Torso)
**Duplicated in:**
- `HealthAnalyzerSystem.OnBeginSurgery` (lines 101-125)
- `SurgerySystem.OnGetSurgeryVerb` (lines 344-362)

**Current Code:**
```csharp
// Pattern repeated twice with slight variations
EntityUid? bodyPartToOpen = null;

// First try to find torso
foreach (var part in _bodySystem.GetBodyChildrenOfType(targetEntity, BodyPartType.Torso))
{
    if (HasComp<SurgeryLayerComponent>(part.Id))
    {
        bodyPartToOpen = part.Id;
        break;
    }
}

// If no torso found, find any body part with SurgeryLayerComponent
if (bodyPartToOpen == null)
{
    foreach (var part in _bodySystem.GetBodyChildren(targetEntity))
    {
        if (HasComp<SurgeryLayerComponent>(part.Id))
        {
            bodyPartToOpen = part.Id;
            break;
        }
    }
}
```

**Recommendation:** Create helper method in SurgerySystem:
```csharp
/// <summary>
/// Finds a body part with SurgeryLayerComponent, preferring torso.
/// </summary>
private EntityUid? FindBodyPartForSurgery(EntityUid bodyEntity)
{
    // First try torso
    foreach (var part in _body.GetBodyChildrenOfType(bodyEntity, BodyPartType.Torso))
    {
        if (HasComp<SurgeryLayerComponent>(part.Id))
            return part.Id;
    }
    
    // Fallback to any body part
    foreach (var part in _body.GetBodyChildren(bodyEntity))
    {
        if (HasComp<SurgeryLayerComponent>(part.Id))
            return part.Id;
    }
    
    return null;
}
```

---

### 2. Getting Torso as Fallback (Repeated 4+ Times)
**Duplicated in:**
- `SurgerySystem.OpenSurgeryUI` (line 448)
- `SurgerySystem.OnBodyPartSelected` (lines 802, 822, 843)

**Current Code:**
```csharp
// Repeated 4 times
var torsoParts = _body.GetBodyChildrenOfType(body, BodyPartType.Torso);
var torso = torsoParts.FirstOrDefault();
if (torso.Id != default)
{
    selectedPart = torso.Id;
}
```

**Recommendation:** Create helper method:
```csharp
/// <summary>
/// Gets the torso body part from a body entity, or null if not found.
/// </summary>
private (EntityUid Id, BodyPartComponent Component)? GetTorso(EntityUid bodyEntity)
{
    var torsoParts = _body.GetBodyChildrenOfType(bodyEntity, BodyPartType.Torso);
    var torso = torsoParts.FirstOrDefault();
    return torso.Id != default ? torso : null;
}
```

---

### 3. TargetBodyPart to HumanoidVisualLayers Mapping (Duplicated)
**Duplicated in:**
- `SurgeryWindow.xaml.cs` - `LayerToBodyPartMap` dictionary (lines 49-61)
- `SurgeryWindow.xaml.cs` - Switch statement in `UpdateBodyPartHighlight` (lines 312-325)

**Current Code:**
```csharp
// Dictionary (line 49)
private static readonly Dictionary<HumanoidVisualLayers, TargetBodyPart> LayerToBodyPartMap = new()
{
    { HumanoidVisualLayers.Head, TargetBodyPart.Head },
    // ... etc
};

// Switch statement (line 312) - does reverse mapping
HumanoidVisualLayers? bodyPartLayer = part switch
{
    TargetBodyPart.Head => HumanoidVisualLayers.Head,
    // ... etc
};
```

**Recommendation:** Create reverse lookup dictionary or helper method:
```csharp
// Add reverse mapping
private static readonly Dictionary<TargetBodyPart, HumanoidVisualLayers> BodyPartToLayerMap = new()
{
    { TargetBodyPart.Head, HumanoidVisualLayers.Head },
    { TargetBodyPart.Torso, HumanoidVisualLayers.Chest },
    // ... etc
};

// Then use: BodyPartToLayerMap.TryGetValue(part, out var layer)
```

---

### 4. Missing Limb Parent Lookup (Complex Nested Logic)
**Location:** `SurgerySystem.OnBodyPartSelected` (lines 800-849)

**Current Code:**
```csharp
// Complex nested if/else for finding parent part for missing limbs
if (targetType == BodyPartType.Arm || targetType == BodyPartType.Leg || targetType == BodyPartType.Head)
{
    var torsoParts = _body.GetBodyChildrenOfType(body, BodyPartType.Torso);
    var torso = torsoParts.FirstOrDefault();
    if (torso.Id != default)
    {
        selectedPart = torso.Id;
    }
}
else if (targetType == BodyPartType.Hand)
{
    // Find the corresponding arm
    var armType = targetSymmetry == BodyPartSymmetry.Left ? BodyPartSymmetry.Left : BodyPartSymmetry.Right;
    var armParts = _body.GetBodyChildrenOfType(body, BodyPartType.Arm, symmetry: armType);
    var arm = armParts.FirstOrDefault();
    if (arm.Id != default)
    {
        selectedPart = arm.Id;
    }
    else
    {
        // Arm is also missing, use torso
        var torsoParts = _body.GetBodyChildrenOfType(body, BodyPartType.Torso);
        var torso = torsoParts.FirstOrDefault();
        if (torso.Id != default)
        {
            selectedPart = torso.Id;
        }
    }
}
// ... similar for Foot
```

**Recommendation:** Create helper method:
```csharp
/// <summary>
/// Finds the parent body part where a missing limb would attach.
/// </summary>
private EntityUid? FindParentPartForMissingLimb(EntityUid bodyEntity, BodyPartType targetType, BodyPartSymmetry? targetSymmetry)
{
    // Arms, legs, and head attach to torso
    if (targetType == BodyPartType.Arm || targetType == BodyPartType.Leg || targetType == BodyPartType.Head)
    {
        return GetTorso(bodyEntity)?.Id;
    }
    
    // Hands attach to arms
    if (targetType == BodyPartType.Hand)
    {
        var armType = targetSymmetry ?? BodyPartSymmetry.Left;
        var armParts = _body.GetBodyChildrenOfType(bodyEntity, BodyPartType.Arm, symmetry: armType);
        var arm = armParts.FirstOrDefault();
        if (arm.Id != default)
            return arm.Id;
        
        // Arm missing, fallback to torso
        return GetTorso(bodyEntity)?.Id;
    }
    
    // Feet attach to legs
    if (targetType == BodyPartType.Foot)
    {
        var legType = targetSymmetry ?? BodyPartSymmetry.Left;
        var legParts = _body.GetBodyChildrenOfType(bodyEntity, BodyPartType.Leg, symmetry: legType);
        var leg = legParts.FirstOrDefault();
        if (leg.Id != default)
            return leg.Id;
        
        // Leg missing, fallback to torso
        return GetTorso(bodyEntity)?.Id;
    }
    
    return null;
}
```

---

### 5. Getting Body from Body Part (Repeated Pattern)
**Duplicated in:** Multiple locations in `SurgerySystem.cs`

**Current Code:**
```csharp
// Pattern repeated ~14 times
if (!TryComp<BodyPartComponent>(ent, out var part) || part.Body == null)
    return;

var body = part.Body.Value;
```

**Recommendation:** Create helper method:
```csharp
/// <summary>
/// Gets the body entity from a body part, or null if not found.
/// </summary>
private EntityUid? GetBodyFromPart(EntityUid partEntity)
{
    if (!TryComp<BodyPartComponent>(partEntity, out var part) || part.Body == null)
        return null;
    return part.Body.Value;
}
```

---

### 6. Creating SurgeryLayerComponent with Defaults (Complex Logic)
**Location:** `SurgerySystem.UpdateUI` (lines 2081-2136)

**Current Code:**
```csharp
// Complex nested if/else creating SurgeryLayerComponent instances
if (!TryComp<SurgeryLayerComponent>(selectedPart, out var selectedLayerComp))
{
    selectedLayer = new SurgeryLayerComponent
    {
        SkinRetracted = layer.SkinRetracted,
        TissueRetracted = layer.TissueRetracted,
        BonesSawed = layer.BonesSawed,
        BonesSmashed = layer.BonesSmashed,
        PartType = selectedPartType ?? layer.PartType
    };
}
else
{
    selectedLayer = selectedLayerComp;
    
    // Multiple conditions checking PartType...
    if (selectedPartType != null && selectedLayer.PartType != selectedPartType)
    {
        selectedLayer = new SurgeryLayerComponent { /* ... */ };
    }
    else if (selectedLayer.PartType == null && selectedPartType != null)
    {
        selectedLayer = new SurgeryLayerComponent { /* ... */ };
    }
    // ... more conditions
}
```

**Recommendation:** Create helper method:
```csharp
/// <summary>
/// Gets or creates a SurgeryLayerComponent for a body part, ensuring PartType is set correctly.
/// </summary>
private SurgeryLayerComponent GetOrCreateLayerComponent(EntityUid partEntity, SurgeryLayerComponent? fallbackLayer, BodyPartType? preferredPartType)
{
    if (TryComp<SurgeryLayerComponent>(partEntity, out var existingLayer))
    {
        // If PartType matches or both are null, use existing
        if (existingLayer.PartType == preferredPartType || 
            (existingLayer.PartType == null && preferredPartType == null))
        {
            return existingLayer;
        }
        
        // Create new instance with correct PartType
        return new SurgeryLayerComponent
        {
            SkinRetracted = existingLayer.SkinRetracted,
            TissueRetracted = existingLayer.TissueRetracted,
            BonesSawed = existingLayer.BonesSawed,
            BonesSmashed = existingLayer.BonesSmashed,
            PartType = preferredPartType ?? existingLayer.PartType ?? fallbackLayer?.PartType
        };
    }
    
    // No existing layer, create from fallback
    return new SurgeryLayerComponent
    {
        SkinRetracted = fallbackLayer?.SkinRetracted ?? false,
        TissueRetracted = fallbackLayer?.TissueRetracted ?? false,
        BonesSawed = fallbackLayer?.BonesSawed ?? false,
        BonesSmashed = fallbackLayer?.BonesSmashed ?? false,
        PartType = preferredPartType ?? fallbackLayer?.PartType
    };
}
```

---

### 7. Getting User from BUI Session
**Location:** `HealthAnalyzerSystem.OnBeginSurgery` (lines 135-147)

**Current Code:**
```csharp
var bui = _uiSystem.GetUiOrNull(ent, HealthAnalyzerUiKey.Key);
if (bui == null)
    return;
    
var sessions = bui.SubscribedSessions;
if (sessions.Count == 0)
    return;
    
var user = sessions.First().AttachedEntity;
if (user == null)
    return;
```

**Recommendation:** Create generic helper extension method:
```csharp
// In a shared utility class or extension
public static EntityUid? GetFirstUserFromBui(this UserInterfaceSystem uiSystem, EntityUid entity, Enum uiKey)
{
    var bui = uiSystem.GetUiOrNull(entity, uiKey);
    if (bui == null)
        return null;
    
    var sessions = bui.SubscribedSessions;
    if (sessions.Count == 0)
        return null;
    
    return sessions.First().AttachedEntity;
}
```

---

### 8. Body Part Selection State Management (Complex Logic)
**Location:** `SurgeryWindow.UpdateState` (lines 464-508)

**Current Code:**
```csharp
// Complex logic for handling pending selections and state updates
bool selectionChanged = false;
if (state.SelectedTargetBodyPart.HasValue)
{
    var targetPart = state.SelectedTargetBodyPart.Value;
    
    if (_pendingSelection.HasValue)
    {
        if (targetPart == _pendingSelection.Value)
        {
            _pendingSelection = null;
            if (targetPart != _selectedBodyPart)
            {
                _selectedBodyPart = targetPart;
                selectionChanged = true;
            }
        }
    }
    else if (targetPart != _selectedBodyPart)
    {
        _selectedBodyPart = targetPart;
        selectionChanged = true;
    }
}
else if (_selectedBodyPart == null && !_pendingSelection.HasValue)
{
    _selectedBodyPart = TargetBodyPart.Torso;
    selectionChanged = true;
}
```

**Recommendation:** Extract to helper method:
```csharp
/// <summary>
/// Updates the selected body part from server state, handling pending selections.
/// </summary>
private bool UpdateSelectedBodyPartFromState(TargetBodyPart? stateSelection)
{
    bool changed = false;
    
    if (stateSelection.HasValue)
    {
        // If we have a pending selection, only accept matching updates
        if (_pendingSelection.HasValue)
        {
            if (stateSelection.Value == _pendingSelection.Value)
            {
                _pendingSelection = null;
                if (stateSelection.Value != _selectedBodyPart)
                {
                    _selectedBodyPart = stateSelection.Value;
                    changed = true;
                }
            }
        }
        else if (stateSelection.Value != _selectedBodyPart)
        {
            _selectedBodyPart = stateSelection.Value;
            changed = true;
        }
    }
    else if (_selectedBodyPart == null && !_pendingSelection.HasValue)
    {
        _selectedBodyPart = TargetBodyPart.Torso;
        changed = true;
    }
    
    return changed;
}
```

---

### 9. TryComp<BodyPartComponent> Pattern (Used 19 Times)
**Location:** Throughout `SurgerySystem.cs`

**Current Code:**
```csharp
// Pattern repeated 19 times
if (!TryComp<BodyPartComponent>(ent, out var part) || part.Body == null)
    return;

var body = part.Body.Value;
```

**Recommendation:** Already covered by #5 (`GetBodyFromPart()` helper)

---

### 10. Click Detection Logic (Texture vs RSI Duplication)
**Location:** `SurgeryWindow.OnSpriteViewClick` (lines 225-266)

**Current Code:**
```csharp
// Similar logic for texture and RSI hit detection
if (layer.Texture != null)
{
    var imagePos = (Vector2i)(layerClickPos * EyeManager.PixelsPerMeter * new Vector2(1, -1) + layer.Texture.Size / 2f);
    if (imagePos.X >= 0 && imagePos.X < layer.Texture.Size.X &&
        imagePos.Y >= 0 && imagePos.Y < layer.Texture.Size.Y)
    {
        isHit = _clickMapManager.IsOccluding(layer.Texture, imagePos);
    }
}
else if (layer.RSI != null && layer.State != null)
{
    // Similar logic but for RSI
    var imagePos = (Vector2i)(layerClickPos * EyeManager.PixelsPerMeter * new Vector2(1, -1) + rsiState.Size / 2f);
    if (imagePos.X >= 0 && imagePos.X < rsiState.Size.X &&
        imagePos.Y >= 0 && imagePos.Y < rsiState.Size.Y)
    {
        isHit = _clickMapManager.IsOccluding(layer.RSI, layer.State, direction, frame, imagePos);
    }
}
```

**Recommendation:** Extract to helper method:
```csharp
/// <summary>
/// Checks if a click position hits a sprite layer (texture or RSI).
/// </summary>
private bool CheckLayerHit(SpriteLayer layer, Vector2 layerClickPos, RsiDirection? direction = null, int frame = 0)
{
    Vector2i imagePos;
    Vector2i size;
    
    if (layer.Texture != null)
    {
        size = layer.Texture.Size;
        imagePos = (Vector2i)(layerClickPos * EyeManager.PixelsPerMeter * new Vector2(1, -1) + size / 2f);
        
        if (imagePos.X >= 0 && imagePos.X < size.X && imagePos.Y >= 0 && imagePos.Y < size.Y)
        {
            return _clickMapManager.IsOccluding(layer.Texture, imagePos);
        }
    }
    else if (layer.RSI != null && layer.State != null)
    {
        var rsiState = layer.RSI[layer.State];
        if (rsiState == null)
            return false;
            
        size = rsiState.Size;
        var dir = direction ?? RsiDirection.South;
        imagePos = (Vector2i)(layerClickPos * EyeManager.PixelsPerMeter * new Vector2(1, -1) + size / 2f);
        
        if (imagePos.X >= 0 && imagePos.X < size.X && imagePos.Y >= 0 && imagePos.Y < size.Y)
        {
            return _clickMapManager.IsOccluding(layer.RSI, layer.State, dir, frame, imagePos);
        }
    }
    
    return false;
}
```

---

### 11. BUI Message Handler Pattern
**Location:** `SurgerySystem.Initialize` (lines 187-194)

**Current Code:**
```csharp
// All handlers follow similar pattern
Subs.BuiEvents<SurgeryLayerComponent>(SurgeryUIKey.Key, subs =>
{
    subs.Event<SurgeryStepSelectedMessage>(OnStepSelected);
    subs.Event<SurgeryOperationMethodSelectedMessage>(OnOperationMethodSelected);
    subs.Event<SurgeryLayerChangedMessage>(OnLayerChanged);
    subs.Event<SurgeryBodyPartSelectedMessage>(OnBodyPartSelected);
    subs.Event<SurgeryHandItemsMessage>(OnHandItemsReceived);
    subs.Event<BoundUIOpenedEvent>(OnSurgeryUIOpened);
    subs.Event<BoundUIClosedEvent>(OnSurgeryUIClosed);
});
```

**Analysis:** This is standard BUI pattern - not a duplication issue. Each handler is appropriately specific.

---

## Summary of Recommendations

### High Priority (Code Duplication)
1. ✅ **Create `FindBodyPartForSurgery()` helper** - Eliminates duplicate in HealthAnalyzerSystem and SurgerySystem
2. ✅ **Create `GetTorso()` helper** - Used 4+ times
3. ✅ **Create reverse mapping dictionary** - Eliminates duplicate TargetBodyPart ↔ HumanoidVisualLayers mapping
4. ✅ **Create `FindParentPartForMissingLimb()` helper** - Simplifies complex nested logic

### Medium Priority (Code Clarity)
5. ✅ **Create `GetBodyFromPart()` helper** - Used ~14 times
6. ✅ **Create `GetOrCreateLayerComponent()` helper** - Simplifies complex conditional logic
7. ✅ **Create `UpdateSelectedBodyPartFromState()` helper** - Extracts complex state management

### Low Priority (Utility)
8. ✅ **Create `GetFirstUserFromBui()` extension** - Generic utility for BUI user extraction

### 12. Getting Body Entity from Body Part (Repeated in UpdateUI)
**Location:** `SurgerySystem.UpdateUI` (lines 2140-2302)

**Current Code:**
```csharp
// Pattern repeated 3 times in UpdateUI
EntityUid? bodyEntity = null;
if (TryComp<BodyPartComponent>(selectedPart, out var selectedPartComp) && selectedPartComp.Body != null)
{
    bodyEntity = selectedPartComp.Body.Value;
}
else if (TryComp<BodyPartComponent>(uid, out var originalPart) && originalPart.Body != null)
{
    bodyEntity = originalPart.Body.Value;
}
```

**Recommendation:** Already covered by #5 (`GetBodyFromPart()` helper), but could create a more specific helper:
```csharp
/// <summary>
/// Gets the body entity from a body part, with fallback to another body part.
/// </summary>
private EntityUid? GetBodyFromPartWithFallback(EntityUid primaryPart, EntityUid? fallbackPart = null)
{
    var body = GetBodyFromPart(primaryPart);
    if (body != null)
        return body;
    
    if (fallbackPart != null)
        return GetBodyFromPart(fallbackPart.Value);
    
    return null;
}
```

---

## Summary of Recommendations

### High Priority (Code Duplication - Eliminates 2+ Duplicates)
1. ✅ **Create `FindBodyPartForSurgery()` helper** - Eliminates duplicate in HealthAnalyzerSystem and SurgerySystem
2. ✅ **Create `GetTorso()` helper** - Used 4+ times
3. ✅ **Create reverse mapping dictionary** - Eliminates duplicate TargetBodyPart ↔ HumanoidVisualLayers mapping
4. ✅ **Create `FindParentPartForMissingLimb()` helper** - Simplifies complex nested logic

### Medium Priority (Code Clarity - Used Frequently)
5. ✅ **Create `GetBodyFromPart()` helper** - Used ~19 times
6. ✅ **Create `GetOrCreateLayerComponent()` helper** - Simplifies complex conditional logic (4 instances)
7. ✅ **Create `UpdateSelectedBodyPartFromState()` helper** - Extracts complex state management
8. ✅ **Create `CheckLayerHit()` helper** - Simplifies click detection logic

### Low Priority (Utility - Single Use or Minor Improvement)
9. ✅ **Create `GetFirstUserFromBui()` extension** - Generic utility for BUI user extraction
10. ✅ **Create `GetBodyFromPartWithFallback()` helper** - Specific to UpdateUI pattern

## Implementation Order
1. **Phase 1:** High priority helpers (1-4) - Eliminates most duplication
2. **Phase 2:** Medium priority helpers (5-8) - Improves code clarity and maintainability
3. **Phase 3:** Low priority utilities (9-10) - Nice-to-have improvements

## Estimated Impact
- **Lines of code reduced:** ~200-300 lines
- **Duplication eliminated:** 8 major patterns
- **Maintainability:** Significantly improved (single source of truth for common operations)
- **Bug risk reduction:** Centralized logic reduces chance of inconsistencies
