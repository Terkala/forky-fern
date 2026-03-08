# IRobustSerializer.FloatFlags Build Error — Analysis & Problem Statement

## Executive Summary

The Content.Server build fails with two compilation errors at `EntryPoint.cs:140`:

1. **CS1061**: `'IRobustSerializer' does not contain a definition for 'FloatFlags'`
2. **CS0103**: The name `'SerializerFloatFlags'` does not exist in the current context

The offending line:

```csharp
Dependencies.Resolve<IRobustSerializer>().FloatFlags = SerializerFloatFlags.RemoveReadNan;
```

**Root cause**: `FloatFlags` and `SerializerFloatFlags` are not defined anywhere in the codebase. The Content.Server references an API that does not exist in the current RobustToolbox/NetSerializer stack.

---

## 1. Architecture Overview

### 1.1 Serialization Stack

```
Content.Server.EntryPoint (Content)
    └── Resolves IRobustSerializer from IoC
            └── IRobustSerializer (Robust.Shared)
                    └── Implemented by ServerRobustSerializer (Robust.Server)
                            └── Extends RobustSerializer (Robust.Shared)
                                    └── Uses NetSerializer.Serializer (NetSerializer package)
```

### 1.2 Key Types

| Type | Location | Purpose |
|------|----------|---------|
| `IRobustSerializer` | Robust.Shared/Serialization/IRobustSerializer.cs | Public interface for network serialization |
| `RobustSerializer` | Robust.Shared/Serialization/RobustSerializer.cs | Base implementation; wraps NetSerializer |
| `ServerRobustSerializer` | Robust.Server/Serialization/ServerRobustSerializer.cs | Server-specific implementation (sealed, minimal) |
| `Serializer` | NetSerializer (RobustToolbox/NetSerializer) | Low-level binary serializer |
| `Settings` | NetSerializer | Configuration for Serializer (CustomTypeSerializers, etc.) |

---

## 2. What the Failing Code Intends to Do

The line:

```csharp
Dependencies.Resolve<IRobustSerializer>().FloatFlags = SerializerFloatFlags.RemoveReadNan;
```

suggests the intent is to configure how the serializer handles floating-point values, specifically:

- **`SerializerFloatFlags.RemoveReadNan`**: When deserializing floats/doubles, replace `NaN` with a safe value (e.g. 0) instead of propagating `NaN`.

This is useful because:

- `float.NaN` and `double.NaN` can cause unexpected behavior (e.g. physics, UI, comparisons).
- Network or save data can contain NaNs from bugs, corruption, or cross-platform differences.
- Some systems (e.g. Java) may produce NaNs that C# would not.

---

## 3. Why It Fails

### 3.1 IRobustSerializer Interface

The `IRobustSerializer` interface in `Robust.Shared/Serialization/IRobustSerializer.cs` defines:

- `Initialize()`, `Serialize()`, `Deserialize()`, `SerializeDirect()`, `DeserializeDirect()`
- `CanSerialize()`, `FindSerializedType()`, `Handshake()`
- `GetSerializableTypesHash()`, `GetStringSerializerPackage()`

There is no `FloatFlags` property or any float-related configuration.

### 3.2 IRobustSerializerInternal

The internal interface adds:

- `GetTypeMap()`
- Statistics: `LargestObjectSerializedBytes`, `BytesSerialized`, etc.

Again, no `FloatFlags` or similar.

### 3.3 RobustSerializer Implementation

`RobustSerializer` creates a NetSerializer `Serializer` like this:

```csharp
var settings = new Settings
{
    CustomTypeSerializers = new[]
    {
        MappedStringSerializer.TypeSerializer,
        new NetMathSerializer(),
        new NetBitArraySerializer(),
        new NetFormattedStringSerializer()
    }
};
_serializer = new Serializer(types, settings);
```

`Settings` only exposes `CustomTypeSerializers`. There is no `FloatFlags` or equivalent.

### 3.4 NetSerializer Package

RobustToolbox uses a local NetSerializer project (based on tomba/netserializer). The `Serializer` and `Settings` types do not define `FloatFlags` or `SerializerFloatFlags` in the current codebase.

### 3.5 Search Results

A search across the RobustToolbox for `FloatFlags`, `SerializerFloatFlags`, and `RemoveReadNan` returns no definitions. The only reference is in `Content.Server/Entry/EntryPoint.cs`.

---

## 4. Possible Origins

### 4.1 Removed API

The most likely explanation is that `FloatFlags` and `SerializerFloatFlags` existed in an older version of RobustToolbox or NetSerializer and were removed during a refactor. Content.Server was not updated to match.

### 4.2 Unimplemented / Planned API

They could have been planned but never implemented, or documented in a spec that was not fully implemented.

### 4.3 Fork / Version Mismatch

The Funkystation fork may use a different RobustToolbox or NetSerializer version than upstream Space Station 14. Upstream SS14’s `EntryPoint.cs` also contains this line, which suggests either:

- Upstream has the same build error, or
- Upstream uses a RobustToolbox/NetSerializer version that still defines these types.

A search for `FloatFlags` in the upstream RobustToolbox repository returns no matches, so the API does not appear to exist there either.

---

## 5. Impact

### 5.1 Build Failure

- Content.Server does not compile.
- Downstream projects (e.g. Content.IntegrationTests) that depend on Content.Server also fail to build.

### 5.2 Functional Impact

- The server cannot start until the error is fixed.
- The intended behavior (handling NaN during deserialization) is not applied. If NaNs appear in serialized data, they will be deserialized as-is, which can cause physics, UI, or gameplay issues.

---

## 6. Resolution Options

### 6.1 Remove the Line (Quick Fix)

If NaN handling is not critical:

```csharp
// Dependencies.Resolve<IRobustSerializer>().FloatFlags = SerializerFloatFlags.RemoveReadNan;
```

**Pros**: Restores the build immediately.  
**Cons**: No explicit NaN handling; behavior depends on NetSerializer defaults.

### 6.2 Implement the API in RobustToolbox

Add `FloatFlags` and `SerializerFloatFlags` to the Robust/NetSerializer stack:

1. Define `SerializerFloatFlags` (e.g. `[Flags]` enum with `RemoveReadNan`, etc.).
2. Add `FloatFlags` to `IRobustSerializer` (or an internal interface) and implement it in `RobustSerializer`.
3. Pass the flag through to NetSerializer’s `Settings` or `Serializer` if it supports it.
4. If NetSerializer does not support it, implement NaN handling in a custom type serializer (e.g. extend or wrap `NetMathSerializer`).

**Pros**: Matches the intended design and keeps Content.Server unchanged.  
**Cons**: Requires changes in RobustToolbox and possibly NetSerializer; more work.

### 6.3 Custom NaN Handling in Content

Implement NaN handling in Content without changing Robust:

- Use a custom type serializer registered via `Settings.CustomTypeSerializers`.
- Or sanitize floats after deserialization in specific systems.

**Pros**: No RobustToolbox changes.  
**Cons**: Scattered logic; may not cover all float/double usage.

---

## 7. Recommendations

1. **Short term**: Remove or comment out the failing line to unblock the build.
2. **Medium term**: Decide whether NaN handling is required. If yes, either:
   - Implement the API in RobustToolbox (option 6.2), or
   - Add a Content-side solution (option 6.3).
3. **Long term**: Align with upstream Space Station 14 and RobustToolbox to avoid similar mismatches.

---

## 8. References

| File | Path |
|------|------|
| Failing code | `Content.Server/Entry/EntryPoint.cs:140` |
| IRobustSerializer | `RobustToolbox/Robust.Shared/Serialization/IRobustSerializer.cs` |
| RobustSerializer | `RobustToolbox/Robust.Shared/Serialization/RobustSerializer.cs` |
| ServerRobustSerializer | `RobustToolbox/Robust.Server/Serialization/ServerRobustSerializer.cs` |
| NetSerializer project | `RobustToolbox/NetSerializer/NetSerializer/NetSerializer.csproj` |

---

*Document generated from codebase analysis. Last verified: 2025-03-07.*
