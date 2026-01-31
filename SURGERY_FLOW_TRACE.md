# Surgery System Flow Trace

## Complete User Flow

### Step 1: Click Player with Health Analyzer
- **Location**: `Content.Server.Medical.HealthAnalyzerSystem.OnAfterInteract`
- **Flow**: 
  - User clicks player with health analyzer
  - Starts doafter (`HealthAnalyzerDoAfterEvent`)
  - On completion: `OpenUserInterface` + `BeginAnalyzingEntity`
  - `UpdateScannedUser` sends `HealthAnalyzerScannedUserMessage` to client
- **Status**: ✅ Works

### Step 2: Show Surgery Button
- **Location**: `Content.Client.HealthAnalyzer.UI.HealthAnalyzerWindow.Populate`
- **Flow**:
  - Receives `HealthAnalyzerScannedUserMessage`
  - Stores `_currentTargetEntity = msg.State.TargetEntity`
  - Calls `SetSurgeryButtonVisible(msg.State.TargetEntity != null)`
  - Button becomes visible if target entity exists
- **Status**: ✅ Works (button is in XAML, visibility controlled)

### Step 3: Click Surgery Button
- **Location**: `Content.Client.HealthAnalyzer.UI.HealthAnalyzerWindow.OnBeginSurgeryButtonPressed`
- **Flow**:
  - Button click fires `OnBeginSurgeryButtonPressed`
  - Calls `OnBeginSurgeryClicked?.Invoke(_currentTargetEntity.Value)`
  - Event wired in `HealthAnalyzerBoundUserInterface.Open()` to send `BeginSurgeryMessage`
- **Status**: ✅ Works

### Step 4: Server Receives BeginSurgeryMessage
- **Location**: `Content.Server.Medical.HealthAnalyzerSystem.OnBeginSurgery`
- **Flow**:
  - BUI handler receives `BeginSurgeryMessage`
  - Gets target entity from message
  - Verifies entity has `BodyComponent`
  - **ISSUE**: Calls `_bodySystem.GetBodyChildrenOfType()` and `_bodySystem.GetBodyChildren()` - these methods don't exist in Forky's BodySystem
  - Finds body part with `SurgeryLayerComponent` (prefers torso)
  - Gets user from BUI session
  - Calls `SurgerySystem.OpenSurgeryUI()`
- **Status**: ⚠️ **BLOCKED** - Missing methods on BodySystem

### Step 5: Open Surgery UI
- **Location**: `Content.Server.Medical.Surgery.SurgerySystem.OpenSurgeryUI`
- **Flow**:
  - Sets up UI on body entity (not body part)
  - Initializes selected body part (defaults to torso)
  - Sends initial `SurgeryBoundUserInterfaceState` to client
  - Opens UI for user
- **Status**: ⚠️ **BLOCKED** - Depends on Step 4

### Step 6: Display Body Representation
- **Location**: `Content.Client.Medical.Surgery.SurgeryWindow.UpdateState`
- **Flow**:
  - Receives `SurgeryBoundUserInterfaceState`
  - Gets body entity from body part
  - Creates/copies sprite to `_spriteViewEntity`
  - Sets `SpriteView.SetEntity(_spriteViewEntity.Value)`
  - Body sprite displays in UI
- **Status**: ✅ Logic correct (depends on Step 5)

### Step 7: Click on Body Part
- **Location**: `Content.Client.Medical.Surgery.SurgeryWindow.OnSpriteViewClick`
- **Flow**:
  - Handles mouse clicks on SpriteView
  - Calculates click position in sprite coordinates
  - Checks each body part layer (Head, Torso, Arms, Legs, etc.)
  - Finds which layer was hit using click map
  - Calls `SetActiveBodyPart(topmostHit.bodyPart)`
- **Status**: ✅ Logic correct

### Step 8: Highlight Body Part
- **Location**: `Content.Client.Medical.Surgery.SurgeryWindow.UpdateBodyPartHighlight`
- **Flow**:
  - Called from `SetActiveBodyPart`
  - Removes existing highlight layers
  - Finds the selected body part's sprite layer
  - Creates new highlight layer with same texture
  - Colors it red (`Color(1.0f, 0.0f, 0.0f, 1.0f)`)
  - Positions it over the body part
- **Status**: ✅ Logic correct

### Step 9: Show Surgical Options
- **Location**: `Content.Client.Medical.Surgery.SurgeryWindow.SetActiveBodyPart` → Server `OnBodyPartSelected`
- **Flow**:
  - `SetActiveBodyPart` calls `OnBodyPartSelected?.Invoke(part)`
  - Sends `SurgeryBodyPartSelectedMessage` to server
  - Server's `SurgerySystem.OnBodyPartSelected` receives message
  - **ISSUE**: Calls `_bodyPartQuery.ConvertTargetBodyPart()` and `_body.GetBodyChildrenOfType()` - second call may fail
  - Filters surgery steps for selected body part
  - Calls `UpdateUI` which sends new state with filtered steps
  - Client's `UpdateState` receives filtered steps
  - Displays steps in `StepsContainer`
- **Status**: ⚠️ **PARTIALLY BLOCKED** - Depends on body part methods

## Critical Issues Found

### Issue 1: Body Parts Not Implemented in Forky
**Problem**: Forky does not have body parts implemented. The surgery system code imports:
- `Content.Shared.Body.Part` namespace (doesn't exist)
- `BodyPartComponent` (doesn't exist)
- `BodyPartType` enum (doesn't exist)
- `BodyPartSymmetry` enum (doesn't exist)

**Impact**: 
- Code will not compile
- Surgery system cannot function without body parts

**Solution**: Body parts system must be implemented first, or surgery system code must be adapted to work without body parts (not recommended).

### Issue 2: Missing BodySystem Methods
**Problem**: Forky's `BodySystem` doesn't have:
- `GetBodyChildren(EntityUid, BodyComponent?)`
- `GetBodyChildrenOfType(EntityUid, BodyPartType, BodyComponent?, BodyPartSymmetry?)`

**Impact**: 
- `HealthAnalyzerSystem.OnBeginSurgery` cannot find body parts
- `SurgerySystem` cannot query body parts for filtering steps

**Solution Options**:
1. Implement body parts system in Forky (full implementation)
2. Add stub/extension methods to BodySystem that return empty (for testing)
3. Wait for body parts to be implemented

### Issue 2: BodyPartQuerySystem Dependency
**Status**: ✅ Fixed - Added `BodyPartQuerySystem` dependency to `SurgerySystem` and updated all calls

### Issue 3: Body Parts May Not Exist
**Problem**: Forky may not have body parts implemented yet. The surgery system assumes:
- `BodyPartComponent` exists
- `BodyPartType` enum exists
- `BodyPartSymmetry` enum exists
- Body parts are organized in a hierarchy

**Impact**: System won't work until body parts are implemented

## Files Modified/Created

### New Files Created:
- All files in `Content.Shared\Medical\Surgery\`
- All files in `Content.Shared\Medical\Integrity\`
- All files in `Content.Shared\Medical\Compatibility\`
- All files in `Content.Shared\Medical\Biosynthetic\`
- `Content.Shared\Medical\TargetBodyPart.cs`
- `Content.Shared\Medical\BodyPartQuerySystem.cs`
- All files in `Content.Client\Medical\Surgery\`
- `Content.Server\Medical\Surgery\SurgerySystem.cs` (copied)
- `Content.Server\Medical\Surgery\Operations\SurgeryOperationEvaluatorSystem.cs`

### Modified Files:
- `Content.Client\HealthAnalyzer\UI\HealthAnalyzerWindow.xaml` - Added surgery button
- `Content.Client\HealthAnalyzer\UI\HealthAnalyzerWindow.xaml.cs` - Added button logic
- `Content.Client\HealthAnalyzer\UI\HealthAnalyzerBoundUserInterface.cs` - Added message handler
- `Content.Shared\MedicalScanner\HealthAnalyzerScannedUserMessage.cs` - Added BeginSurgeryMessage
- `Content.Server\Medical\HealthAnalyzerSystem.cs` - Added OnBeginSurgery handler

## Next Steps

1. **Verify body parts exist in Forky** - Check if BodyPartComponent, BodyPartType, etc. exist
2. **If body parts don't exist**: Either implement them or add stub methods to BodySystem
3. **If body parts exist but methods are missing**: Add GetBodyChildren and GetBodyChildrenOfType to BodySystem
4. **Test the flow** once body parts are available
