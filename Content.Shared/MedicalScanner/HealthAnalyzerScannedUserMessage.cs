// SPDX-FileCopyrightText: 2023-2024 Whisper <121047731+QuietlyWhisper@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Saphire Lattice <lattice@saphi.re>
// SPDX-FileCopyrightText: 2024 Rainfey <rainfey0+github@gmail.com>
// SPDX-FileCopyrightText: 2026 Fruitsalad <949631+Fruitsalad@users.noreply.github.com>
// SPDX-License-Identifier: MIT

using Content.Shared.FixedPoint;
using Content.Shared.Medical.Surgery;
using Robust.Shared.Serialization;

namespace Content.Shared.MedicalScanner;

/// <summary>
/// On interacting with an entity retrieves the entity UID for use with getting the current damage of the mob.
/// </summary>
[Serializable, NetSerializable]
public sealed class HealthAnalyzerScannedUserMessage : BoundUserInterfaceMessage
{
    public HealthAnalyzerUiState State;

    public HealthAnalyzerScannedUserMessage(HealthAnalyzerUiState state)
    {
        State = state;
    }
}

/// <summary>
/// Contains the current state of a health analyzer control. Used for the health analyzer and cryo pod.
/// </summary>
[Serializable, NetSerializable]
public struct HealthAnalyzerUiState
{
    public readonly NetEntity? TargetEntity;
    public float Temperature;
    public float BloodLevel;
    public bool? ScanMode;
    public bool? Bleeding;
    public bool? Unrevivable;
    public int? MaxIntegrity;
    public FixedPoint2? UsedIntegrity;
    public FixedPoint2? TemporaryIntegrityBonus;
    public FixedPoint2? CurrentBioRejection;
    public FixedPoint2? SurgeryPenalty;
    public List<IntegrityBreakdownEntry>? IntegrityBreakdown;
    
    // Surgery mode data
    public List<NetEntity>? SurgerySteps;
    public Dictionary<NetEntity, SurgeryStepOperationInfo>? SurgeryStepOperationInfo;
    public SurgeryLayer? CurrentSurgeryLayer;
    public TargetBodyPart? SelectedSurgeryBodyPart;

    public HealthAnalyzerUiState() {}

    public HealthAnalyzerUiState(NetEntity? targetEntity, float temperature, float bloodLevel, bool? scanMode, bool? bleeding, bool? unrevivable, int? maxIntegrity = null, FixedPoint2? usedIntegrity = null, FixedPoint2? temporaryIntegrityBonus = null, FixedPoint2? currentBioRejection = null, FixedPoint2? surgeryPenalty = null, List<IntegrityBreakdownEntry>? integrityBreakdown = null, List<NetEntity>? surgerySteps = null, Dictionary<NetEntity, SurgeryStepOperationInfo>? surgeryStepOperationInfo = null, SurgeryLayer? currentSurgeryLayer = null, TargetBodyPart? selectedSurgeryBodyPart = null)
    {
        TargetEntity = targetEntity;
        Temperature = temperature;
        BloodLevel = bloodLevel;
        ScanMode = scanMode;
        Bleeding = bleeding;
        Unrevivable = unrevivable;
        MaxIntegrity = maxIntegrity;
        UsedIntegrity = usedIntegrity;
        TemporaryIntegrityBonus = temporaryIntegrityBonus;
        CurrentBioRejection = currentBioRejection;
        SurgeryPenalty = surgeryPenalty;
        IntegrityBreakdown = integrityBreakdown;
        SurgerySteps = surgerySteps;
        SurgeryStepOperationInfo = surgeryStepOperationInfo;
        CurrentSurgeryLayer = currentSurgeryLayer;
        SelectedSurgeryBodyPart = selectedSurgeryBodyPart;
    }
}

/// <summary>
/// Represents a single entry in the integrity breakdown, showing which component uses integrity.
/// </summary>
[Serializable, NetSerializable]
public struct IntegrityBreakdownEntry
{
    public string ComponentName;
    public FixedPoint2 IntegrityCost;
    public string ComponentType;

    public IntegrityBreakdownEntry(string componentName, FixedPoint2 integrityCost, string componentType)
    {
        ComponentName = componentName;
        IntegrityCost = integrityCost;
        ComponentType = componentType;
    }
}

/// <summary>
/// Message sent from client to server when user clicks "Begin Surgery" button.
/// </summary>
[Serializable, NetSerializable]
public sealed class BeginSurgeryMessage : BoundUserInterfaceMessage
{
    public NetEntity TargetEntity;

    public BeginSurgeryMessage(NetEntity targetEntity)
    {
        TargetEntity = targetEntity;
    }
}

/// <summary>
/// Message sent from client to server when user attempts a surgery operation from health analyzer.
/// </summary>
[Serializable, NetSerializable]
public sealed class AttemptSurgeryMessage : BoundUserInterfaceMessage
{
    public NetEntity Step;
    public NetEntity TargetEntity;
    public SurgeryLayer Layer;
    public TargetBodyPart? SelectedBodyPart;
    public bool IsImprovised;

    public AttemptSurgeryMessage(NetEntity step, NetEntity targetEntity, SurgeryLayer layer, TargetBodyPart? selectedBodyPart = null, bool isImprovised = false)
    {
        Step = step;
        TargetEntity = targetEntity;
        Layer = layer;
        SelectedBodyPart = selectedBodyPart;
        IsImprovised = isImprovised;
    }
}