// SPDX-FileCopyrightText: 2025 Winkarst-cpu <74284083+Winkarst-cpu@users.noreply.github.com>
// SPDX-License-Identifier: MIT

using Robust.Shared.Audio;

namespace Content.Shared.Prying.Components;

/// <summary>
/// Server-only component that grants prying capability (e.g. from cyber limbs).
/// Used instead of networked PryingComponent to avoid LastComponentRemoved triggering
/// client crashes when the capability is removed.
/// </summary>
[RegisterComponent]
public sealed partial class PryingCapabilityComponent : Component
{
    [DataField]
    public bool PryPowered;

    [DataField]
    public bool Force;

    [DataField]
    public float SpeedModifier = 1.0f;

    [DataField]
    public bool Enabled = true;

    [DataField]
    public SoundSpecifier UseSound = new SoundPathSpecifier("/Audio/Items/crowbar.ogg");
}
