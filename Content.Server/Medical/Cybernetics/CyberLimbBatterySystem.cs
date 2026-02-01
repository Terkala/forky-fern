// SPDX-FileCopyrightText: 2025
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Part;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Medical.Cybernetics;
using Content.Shared.Medical.Cybernetics.Modules;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Wires;
using Content.Server.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;

namespace Content.Server.Medical.Cybernetics;

/// <summary>
/// System that handles battery module insertion and removal for cyber-limbs when the maintenance panel is open.
/// </summary>
public sealed class CyberLimbBatterySystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _toolSystem = default!;
    [Dependency] private readonly SharedWiresSystem _wiresSystem = default!;

    private const string PryingQuality = "Prying";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberLimbComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CyberLimbComponent, RemoveBatteryModuleDoAfterEvent>(OnRemoveBatteryModule);
    }

    private void OnInteractUsing(EntityUid uid, CyberLimbComponent component, InteractUsingEvent args)
    {
        // Check if panel is open
        if (!TryComp<WiresPanelComponent>(uid, out var panel) || !panel.Open)
            return;

        // Check if user is holding a battery module
        if (HasComp<BatteryModuleComponent>(args.Used))
        {
            // Try to insert into storage
            if (!TryComp<StorageComponent>(uid, out var storage))
                return;

            if (storage.Container == null)
                return;

            // Check if storage has space
            if (storage.Container.ContainedEntities.Count >= storage.Grid.GetArea())
                return;

            // Insert battery module
            _container.Insert(args.Used, storage.Container);
            _popup.PopupEntity(Loc.GetString("cyber-limb-battery-installed"), uid, args.User);
            
            // Trigger stats recalculation
            if (TryComp<BodyPartComponent>(uid, out var part) && part.Body != null)
            {
                var statsSystem = EntityManager.System<CyberLimbStatsSystem>();
                statsSystem.RecalculateStats(part.Body.Value);
            }
            
            return;
        }

        // Check if user is holding a prying tool and storage has batteries
        if (_toolSystem.HasQuality(args.Used, PryingQuality))
        {
            if (!TryComp<StorageComponent>(uid, out var storage))
                return;

            if (storage.Container == null)
                return;

            // Find a battery module in storage
            EntityUid? batteryModule = null;
            foreach (var entity in storage.Container.ContainedEntities)
            {
                if (HasComp<BatteryModuleComponent>(entity))
                {
                    batteryModule = entity;
                    break;
                }
            }

            if (batteryModule == null)
                return;

            // Start DoAfter to remove battery
            var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(3),
                new RemoveBatteryModuleDoAfterEvent(), uid, target: uid, used: args.Used)
            {
                BreakOnMove = true
            };

            _doAfter.TryStartDoAfter(doAfterEventArgs);
        }
    }

    private void OnRemoveBatteryModule(EntityUid uid, CyberLimbComponent component, RemoveBatteryModuleDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (!TryComp<StorageComponent>(uid, out var storage))
            return;

        if (storage.Container == null)
            return;

        // Find and remove a battery module
        EntityUid? batteryModule = null;
        foreach (var entity in storage.Container.ContainedEntities)
        {
            if (HasComp<BatteryModuleComponent>(entity))
            {
                batteryModule = entity;
                break;
            }
        }

        if (batteryModule == null)
            return;

        // Remove battery module
        _container.Remove(batteryModule.Value, storage.Container);
        _popup.PopupEntity(Loc.GetString("cyber-limb-battery-removed"), uid, args.User);
        
        // Trigger stats recalculation
        if (TryComp<BodyPartComponent>(uid, out var part) && part.Body != null)
        {
            var statsSystem = EntityManager.System<CyberLimbStatsSystem>();
            statsSystem.RecalculateStats(part.Body.Value);
        }

        args.Handled = true;
    }
}

/// <summary>
/// Event raised when a battery module is successfully removed from a cyber-limb.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class RemoveBatteryModuleDoAfterEvent : SimpleDoAfterEvent
{
}
