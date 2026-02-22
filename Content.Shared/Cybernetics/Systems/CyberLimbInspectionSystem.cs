using System.Linq;
using Content.Shared.Body;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Overlays;
using Content.Shared.Storage;
using Robust.Shared.Prototypes;

namespace Content.Shared.Cybernetics.Systems;

public sealed class CyberLimbInspectionSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private static readonly ProtoId<DamageContainerPrototype> Silicon = "Silicon";

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
        var efficiency = (int)(stats.Efficiency * 100);

        args.PushMarkup(
            Loc.GetString("cyber-limb-inspection-service-time",
                ("remaining", remaining),
                ("max", max)));
        args.PushMarkup(
            Loc.GetString("cyber-limb-inspection-efficiency",
                ("efficiency", efficiency)));

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

    private static string FormatServiceTime(TimeSpan time)
    {
        if (time.TotalMinutes >= 1)
            return $"{(int)time.TotalMinutes}m";
        return $"{(int)time.TotalSeconds}s";
    }
}
