// SPDX-FileCopyrightText: 2021 Flipp Syder <76629141+vulppine@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 wrexbe <81056464+wrexbe@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 mirrorcult <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2022 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Shared.Damage
{
    [Serializable, NetSerializable]
    public enum DamageVisualizerKeys
    {
        Disabled,
        DamageSpecifierDelta,
        DamageUpdateGroups,
        ForceUpdate
    }

    /// <summary>
    /// Per-layer state for damage overlays. Controls which overlay groups are visible.
    /// </summary>
    [Serializable, NetSerializable]
    public enum DamageOverlayLayerState : byte
    {
        AllEnabled = 0,    // Normal limb - show all overlays
        BloodDisabled = 1, // Cybernetic - hide Brute only, show Burn etc.
        AllDisabled = 2    // Missing - hide all overlays
    }

    [Serializable, NetSerializable]
    public sealed class DamageVisualizerGroupData : ICloneable
    {
        public List<string> GroupList;

        public DamageVisualizerGroupData(List<string> groupList)
        {
            GroupList = groupList;
        }

        public object Clone()
        {
            return new DamageVisualizerGroupData(new List<string>(GroupList));
        }
    }
}
