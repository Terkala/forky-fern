// SPDX-FileCopyrightText: 2026 pathetic meowmeow <uhhadd@gmail.com>
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Part;

namespace Content.Shared.Body.Events;

/// <summary>
/// Raised on an entity to query its body parts.
/// Handlers ADD parts (any component can handle). Callers may see duplicates; apply deduplication if needed.
/// </summary>
[ByRefEvent]
public record struct GetBodyPartsEvent
{
    public List<(EntityUid Id, BodyPartComponent Component)> Parts;

    public GetBodyPartsEvent()
    {
        Parts = new();
    }
}
