using System.Collections.Generic;
using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Events;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Stacks;
using Robust.Shared.Containers;

namespace Content.Shared.Cybernetics.Systems;

public sealed class CyberLimbModuleSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberLimbMatterBinComponent, EntGotInsertedIntoContainerMessage>(OnMatterBinInserted);
        SubscribeLocalEvent<CyberLimbComponent, EntInsertedIntoContainerMessage>(OnStorageInserted);
        SubscribeLocalEvent<CyberLimbComponent, EntRemovedFromContainerMessage>(OnStorageRemoved);
    }

    private void OnMatterBinInserted(Entity<CyberLimbMatterBinComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (!HasComp<CyberLimbComponent>(args.Container.Owner))
            return;

        if (args.Container.ID != StorageComponent.ContainerId)
            return;

        ent.Comp.ServiceRemaining = TimeSpan.Zero;
        Dirty(ent, ent.Comp);
    }

    private void OnStorageInserted(Entity<CyberLimbComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != StorageComponent.ContainerId)
            return;

        if (!_container.TryGetContainingContainer(ent.Owner, out var outer) ||
            outer.ID != BodyComponent.ContainerID ||
            !HasComp<BodyComponent>(outer.Owner))
            return;

        RecalculateStats(outer.Owner);
    }

    private void OnStorageRemoved(Entity<CyberLimbComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != StorageComponent.ContainerId)
            return;

        if (!_container.TryGetContainingContainer(ent.Owner, out var outer) ||
            outer.ID != BodyComponent.ContainerID ||
            !HasComp<BodyComponent>(outer.Owner))
            return;

        RecalculateStats(outer.Owner);
    }

    /// <summary>
    /// Returns matter bin entities, manipulator count, and capacitor count across all cyber limbs on the body.
    /// </summary>
    public (List<EntityUid> MatterBins, int ManipulatorCount, int CapacitorCount) GetModuleCounts(EntityUid body)
    {
        var matterBins = new List<EntityUid>();
        var manipulatorCount = 0;
        var capacitorCount = 0;

        foreach (var organ in _body.GetAllOrgans(body))
        {
            if (!HasComp<CyberLimbComponent>(organ) || !TryComp<StorageComponent>(organ, out var storage) || storage.Container == null)
                continue;

            foreach (var item in storage.Container.ContainedEntities)
            {
                if (!TryComp<CyberLimbModuleComponent>(item, out var module))
                    continue;

                switch (module.ModuleType)
                {
                    case CyberLimbModuleType.MatterBin:
                        if (HasComp<CyberLimbMatterBinComponent>(item))
                            matterBins.Add(item);
                        break;
                    case CyberLimbModuleType.Manipulator:
                        manipulatorCount += TryComp<StackComponent>(item, out var stack) ? stack.Count : 1;
                        break;
                    case CyberLimbModuleType.Capacitor:
                        capacitorCount += TryComp<StackComponent>(item, out var capStack) ? capStack.Count : 1;
                        break;
                }
            }
        }

        return (matterBins, manipulatorCount, capacitorCount);
    }

    /// <summary>
    /// Service time multiplier from capacitors. +10% per capacitor.
    /// </summary>
    public float GetCapacitorMultiplier(int count)
    {
        return 1f + 0.1f * Math.Max(0, count);
    }

    /// <summary>
    /// Limb efficiency from manipulators. 100% base, +10% per additional. External modifiers multiply this.
    /// </summary>
    public float GetLimbEfficiencyFromManipulators(int count)
    {
        return 1f + 0.1f * Math.Max(0, count - 1);
    }

    /// <summary>
    /// Total service remaining = BaseServiceRemaining + sum(matter bin ServiceRemaining).
    /// </summary>
    public TimeSpan GetTotalServiceRemaining(EntityUid body)
    {
        if (!TryComp<CyberLimbStatsComponent>(body, out var stats))
            return TimeSpan.Zero;

        var (matterBins, _, _) = GetModuleCounts(body);
        var matterBinTotal = matterBins
            .Select(mb => Comp<CyberLimbMatterBinComponent>(mb).ServiceRemaining)
            .Aggregate(TimeSpan.Zero, (a, b) => a + b);

        return stats.BaseServiceRemaining + matterBinTotal;
    }

    /// <summary>
    /// Total service time max = (BaseServiceTimePerLimb * limbCount + sum(matter bin ServiceTime)) * capacitor multiplier.
    /// </summary>
    public TimeSpan GetTotalServiceMax(EntityUid body)
    {
        if (!TryComp<CyberLimbStatsComponent>(body, out var stats))
            return TimeSpan.Zero;

        var cyberCount = _body.GetAllOrgans(body).Count(o => HasComp<CyberLimbComponent>(o));
        var (matterBins, _, capacitorCount) = GetModuleCounts(body);
        var matterBinTotal = matterBins
            .Select(mb =>
            {
                var comp = Comp<CyberLimbMatterBinComponent>(mb);
                var count = TryComp<StackComponent>(mb, out var s) ? s.Count : 1;
                return TimeSpan.FromTicks(comp.ServiceTime.Ticks * count);
            })
            .Aggregate(TimeSpan.Zero, (a, b) => a + b);

        var baseMax = stats.BaseServiceTimePerLimb * cyberCount + matterBinTotal;
        var multiplier = GetCapacitorMultiplier(capacitorCount);
        return TimeSpan.FromTicks((long)(baseMax.Ticks * multiplier));
    }

    public void RecalculateStats(EntityUid body)
    {
        var ev = new CyberLimbStatsRecalcEvent(body);
        RaiseLocalEvent(body, ref ev);
    }
}
