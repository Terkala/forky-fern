# LayerState_ComputedFromConfig_NotHardcoded — Fix Plan

## Test Location
`Content.IntegrationTests/Tests/Medical/DynamicSurgeryConfigIntegrationTest.cs` (line 182)

## Observed Failure
```
RetractSkin: doafter-failed
Assert.That(reqEv.Valid, Is.True)
  Expected: True
  But was:  False
```
**Location:** RetractSkin `SurgeryRequestEvent` assertion — `TryStartDoAfter` returns false.

## Investigation Summary

### Root Cause (Hypothesis)
`SharedDoAfterSystem.TryStartDoAfter` returns false when starting the RetractSkin DoAfter. Possible causes:
1. **NeedHand / BreakOnHandChange** — Tool must be in active hand; `InitialItem` = `GetActiveItem()`.
2. **RequireCanInteract** — `ActionBlockerSystem.CanInteract(surgeon, patient)` may fail for spawned NPC surgeons.
3. **ProcessDuplicates** — A prior DoAfter might still be considered active (unlikely after 500 ticks).
4. **ShouldCancel** — Distance, `InRangeUnobstructed`, or other checks may fail.

### Attempted Fixes (Did Not Resolve)
- Set active hand to tool before each surgery step (ClampVessels, RetractSkin).
- Use `TryDrop((surgeon, hands), ...)` to match passing test.
- Add `RunTicksSync(1)` between tool swap and surgery request.

### Key Difference vs Passing Tests
- **CanPerformStep_RespectsConfigDrivenPrerequisites** passes with the same `SurgeryRequestEvent` + `RaiseLocalEvent` approach.
- **LegAmputationSurgeryIntegrationTest** uses `InteractionTest` + `SendBui` + `AwaitDoAfters`; surgeon is the attached player (SPlayer).

### Recommended Next Steps

1. **Refactor to use InteractionTest**  
   Make `LayerState_ComputedFromConfig_NotHardcoded` extend `InteractionTest` and use `SendBui` + `AwaitDoAfters`, matching `LegAmputationSurgeryIntegrationTest`. This ensures the surgeon is the attached player and DoAfters are awaited correctly.

2. **Add diagnostic logging**  
   In `SurgerySystem.OnSurgeryRequest`, before `TryStartDoAfter`, log: tool, active hand, `CanInteract`, and `ProcessDuplicates` result to pinpoint the failure.

3. **Bypass RequireCanInteract in tests**  
   If `CanInteract` fails for spawned surgeons, add a test-only bypass (e.g. `InstantDoAftersTag` or a test-specific component) so surgery DoAfters can start without interaction checks.

4. **Verify surgeon is attached player**  
   Use the session-attached entity as the surgeon instead of a separately spawned mob, matching `InteractionTest` behavior.

## Current Test Changes (Partial Fix)
- ClampVessels and RetractSkin blocks now set the active hand to the tool.
- `TryDrop((surgeon, hands), ...)` used for consistency.
- `RunTicksSync(1)` between RetractSkin tool swap and request (no effect observed).

## Related Files

- `Content.IntegrationTests/Tests/Medical/DynamicSurgeryConfigIntegrationTest.cs` — test
- `Content.IntegrationTests/Tests/Medical/LegAmputationSurgeryIntegrationTest.cs` — reference (uses InteractionTest)
- `Content.Shared/_Funkystation/Surgery/SurgerySystem.cs` — `DoAfterArgs` + `TryStartDoAfter` (line 527)
- `Content.Shared/DoAfter/SharedDoAfterSystem.cs` — `InitialHand` / `InitialItem` capture
- `Content.Shared/ActionBlocker/ActionBlockerSystem.cs` — `CanInteract`
