using System.Linq;
using Content.Shared.Body;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Overlays;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Robust.Shared.Prototypes;

namespace Content.Shared.Cybernetics.Systems;

public sealed class CyberLimbInspectionSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly CyberLimbModuleSystem _moduleSystem = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private static readonly ProtoId<DamageContainerPrototype> Silicon = "Silicon";

    private const string ArmLeft = "ArmLeft";
    private const string ArmRight = "ArmRight";
    private const string LegLeft = "LegLeft";
    private const string LegRight = "LegRight";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberLimbStatsComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<CyberLimbStatsComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!Exists(args.Examiner))
            return;

        if (!_inventory.TryGetSlotEntity(args.Examiner, "eyes", out var eyesEntity))
            return;

        if (!TryComp<ShowHealthBarsComponent>(eyesEntity, out var showHealth))
            return;

        if (!showHealth.DamageContainers.Contains(Silicon))
            return;

        var stats = ent.Comp;
        var remaining = FormatServiceTime(stats.ServiceTimeRemaining);
        var max = FormatServiceTime(stats.ServiceTimeMax);
        var depleted = stats.ServiceTimeRemaining <= TimeSpan.Zero;
        var depletedMultiplier = depleted ? 0.5f : 1f;

        args.PushMarkup(
            Loc.GetString("cyber-limb-inspection-service-time",
                ("remaining", remaining),
                ("max", max)));

        var limbsByCategory = new Dictionary<string, List<EntityUid>>();
        foreach (var organ in _body.GetAllOrgans(ent.Owner))
        {
            if (!HasComp<CyberLimbComponent>(organ) || !TryComp<OrganComponent>(organ, out var organComp) || organComp.Category is not { } category)
                continue;
            var categoryStr = category.ToString();
            if (categoryStr is not (ArmLeft or ArmRight or LegLeft or LegRight))
                continue;
            if (!limbsByCategory.TryGetValue(categoryStr, out var list))
            {
                list = new List<EntityUid>();
                limbsByCategory[categoryStr] = list;
            }
            list.Add(organ);
        }

        var armLeftCount = limbsByCategory.GetValueOrDefault(ArmLeft)?.Count ?? 0;
        var armRightCount = limbsByCategory.GetValueOrDefault(ArmRight)?.Count ?? 0;
        var legLeftCount = limbsByCategory.GetValueOrDefault(LegLeft)?.Count ?? 0;
        var legRightCount = limbsByCategory.GetValueOrDefault(LegRight)?.Count ?? 0;

        if (armLeftCount > 0 || armRightCount > 0)
        {
            var armsManipulators = 0;
            foreach (var organ in limbsByCategory.GetValueOrDefault(ArmLeft) ?? [])
                armsManipulators += GetManipulatorCount(organ);
            foreach (var organ in limbsByCategory.GetValueOrDefault(ArmRight) ?? [])
                armsManipulators += GetManipulatorCount(organ);
            var efficiency = (int)(_moduleSystem.GetLimbEfficiencyFromManipulators(armsManipulators) * depletedMultiplier * 100);
            var labelKey = (armLeftCount > 0 && armRightCount > 0)
                ? "cyber-limb-inspection-efficiency-hands"
                : armLeftCount > 0 ? "cyber-limb-inspection-efficiency-left-arm" : "cyber-limb-inspection-efficiency-right-arm";
            args.PushMarkup(Loc.GetString(labelKey, ("efficiency", efficiency)));
        }

        if (legLeftCount > 0 || legRightCount > 0)
        {
            var legsManipulators = 0;
            foreach (var organ in limbsByCategory.GetValueOrDefault(LegLeft) ?? [])
                legsManipulators += GetManipulatorCount(organ);
            foreach (var organ in limbsByCategory.GetValueOrDefault(LegRight) ?? [])
                legsManipulators += GetManipulatorCount(organ);
            var efficiency = (int)(_moduleSystem.GetLimbEfficiencyFromManipulators(legsManipulators) * depletedMultiplier * 100);
            var labelKey = legLeftCount > 0 && legRightCount > 0
                ? "cyber-limb-inspection-efficiency-feet"
                : legLeftCount > 0 ? "cyber-limb-inspection-efficiency-left-leg" : "cyber-limb-inspection-efficiency-right-leg";
            args.PushMarkup(Loc.GetString(labelKey, ("efficiency", efficiency)));
        }

        var moduleNames = new List<string>();
        foreach (var organ in _body.GetAllOrgans(ent.Owner))
        {
            if (!HasComp<CyberLimbComponent>(organ) || !TryComp<StorageComponent>(organ, out var storage) || storage.Container == null)
                continue;

            foreach (var item in storage.Container.ContainedEntities)
            {
                var name = MetaData(item).EntityName;
                if (!string.IsNullOrWhiteSpace(name))
                    moduleNames.Add(name);
            }
        }

        if (moduleNames.Count > 0)
        {
            var modulesList = string.Join(", ", moduleNames.Distinct());
            args.PushMarkup(Loc.GetString("cyber-limb-inspection-modules", ("modules", modulesList)));
        }
    }

    private int GetManipulatorCount(EntityUid limb)
    {
        if (!TryComp<StorageComponent>(limb, out var storage) || storage.Container == null)
            return 0;
        var count = 0;
        foreach (var item in storage.Container.ContainedEntities)
        {
            if (TryComp<CyberLimbModuleComponent>(item, out var module) && module.ModuleType == CyberLimbModuleType.Manipulator)
                count += TryComp<StackComponent>(item, out var stack) ? stack.Count : 1;
        }
        return count;
    }

    private static string FormatServiceTime(TimeSpan time)
    {
        if (time.TotalMinutes >= 1)
            return $"{(int)time.TotalMinutes}m";
        return $"{(int)time.TotalSeconds}s";
    }
}
