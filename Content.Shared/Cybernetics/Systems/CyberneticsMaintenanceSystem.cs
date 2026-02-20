using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Cybernetics.Events;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Medical.Integrity.Events;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Tools.Systems;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.Cybernetics.Systems;

[UsedImplicitly]
public sealed class CyberneticsMaintenanceSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private const float ScrewdriverDelay = 2f;
    private const float WrenchDelay = 2f;
    private const float WireInsertDelay = 2.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberLimbComponent, EntGotInsertedIntoContainerMessage>(OnCyberLimbInserted);
        SubscribeLocalEvent<CyberLimbComponent, EntGotRemovedFromContainerMessage>(OnCyberLimbRemoved);

        SubscribeLocalEvent<CyberneticsMaintenanceComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CyberneticsMaintenanceComponent, CyberneticsScrewdriverDoAfterEvent>(OnScrewdriverDoAfter);
        SubscribeLocalEvent<CyberneticsMaintenanceComponent, CyberneticsWrenchDoAfterEvent>(OnWrenchDoAfter);
        SubscribeLocalEvent<CyberneticsMaintenanceComponent, CyberneticsWireInsertDoAfterEvent>(OnWireInsertDoAfter);
    }

    private void OnCyberLimbInserted(Entity<CyberLimbComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != BodyComponent.ContainerID)
            return;

        var body = args.Container.Owner;
        if (!HasComp<BodyComponent>(body))
            return;

        EnsureCyberneticsMaintenanceComponent((body, Comp<BodyComponent>(body)));
    }

    private void OnCyberLimbRemoved(Entity<CyberLimbComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (args.Container.ID != BodyComponent.ContainerID)
            return;

        var body = args.Container.Owner;
        if (!HasComp<BodyComponent>(body))
            return;

        RecalcCyberneticsMaintenanceComponent((body, Comp<BodyComponent>(body)));
    }

    private void EnsureCyberneticsMaintenanceComponent(Entity<BodyComponent> body)
    {
        if (HasComp<CyberneticsMaintenanceComponent>(body))
            return;

        var hasCyberLimb = _body.GetAllOrgans(body).Any(o => HasComp<CyberLimbComponent>(o));
        if (hasCyberLimb)
            EnsureComp<CyberneticsMaintenanceComponent>(body);
    }

    private void RecalcCyberneticsMaintenanceComponent(Entity<BodyComponent> body)
    {
        if (!HasComp<CyberneticsMaintenanceComponent>(body))
            return;

        var hasCyberLimb = _body.GetAllOrgans(body).Any(o => HasComp<CyberLimbComponent>(o));
        if (!hasCyberLimb)
            RemComp<CyberneticsMaintenanceComponent>(body);
    }

    private void OnInteractUsing(Entity<CyberneticsMaintenanceComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var comp = ent.Comp;
        var body = ent.Owner;
        var user = args.User;
        var used = args.Used;

        if (_tool.HasQuality(used, "Screwing"))
        {
            if (comp.PanelSecured || (comp.PanelExposed && !comp.PanelOpen))
            {
                args.Handled = _tool.UseTool(used, user, body, ScrewdriverDelay, "Screwing", new CyberneticsScrewdriverDoAfterEvent());
            }
            return;
        }

        if (_tool.HasQuality(used, "Anchoring"))
        {
            if ((comp.PanelExposed && !comp.PanelOpen) || comp.PanelOpen)
            {
                args.Handled = _tool.UseTool(used, user, body, WrenchDelay, "Anchoring", new CyberneticsWrenchDoAfterEvent());
            }
            return;
        }

        if (_tag.HasTag(used, "CableCoil") && TryComp<StackComponent>(used, out var stack))
        {
            if (!comp.PanelOpen)
            {
                _popup.PopupClient(Loc.GetString("cyber-maintenance-panel-closed"), body, user);
                return;
            }

            var cyberCount = _body.GetAllOrgans(body).Count(o => HasComp<CyberLimbComponent>(o));
            if (comp.WiresInsertedCount >= cyberCount)
            {
                _popup.PopupClient(Loc.GetString("cyber-maintenance-no-wires-needed"), body, user);
                return;
            }

            if (stack.Count < 1)
            {
                _popup.PopupClient(Loc.GetString("cyber-maintenance-insufficient-wires"), body, user);
                return;
            }

            var doAfterArgs = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(WireInsertDelay), new CyberneticsWireInsertDoAfterEvent(), body, body, used)
            {
                BreakOnDropItem = true,
                BreakOnMove = true,
                NeedHand = true,
            };

            args.Handled = _doAfter.TryStartDoAfter(doAfterArgs);
        }
    }

    private void OnScrewdriverDoAfter(Entity<CyberneticsMaintenanceComponent> ent, ref CyberneticsScrewdriverDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        var comp = ent.Comp;
        var body = ent.Owner;

        if (comp.PanelSecured)
        {
            comp.PanelExposed = true;
            comp.PanelSecured = false;
            ApplyPenaltyToCyberLimbs(body, 1);
            _popup.PopupEntity(Loc.GetString("cyber-maintenance-expose"), body, args.User);
        }
        else if (comp.PanelExposed && !comp.PanelOpen)
        {
            comp.PanelExposed = false;
            comp.PanelSecured = true;
            RemovePenaltyFromCyberLimbs(body, 1);
            _popup.PopupEntity(Loc.GetString("cyber-maintenance-secure"), body, args.User);
        }

        Dirty(ent, comp);
    }

    private void OnWrenchDoAfter(Entity<CyberneticsMaintenanceComponent> ent, ref CyberneticsWrenchDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        var comp = ent.Comp;
        var body = ent.Owner;

        if (comp.PanelExposed && !comp.PanelOpen)
        {
            comp.PanelOpen = true;
            ApplyPenaltyToCyberLimbs(body, 1);
            _popup.PopupEntity(Loc.GetString("cyber-maintenance-open"), body, args.User);
        }
        else if (comp.PanelOpen)
        {
            comp.PanelOpen = false;
            comp.WiresInsertedCount = 0;
            RemovePenaltyFromCyberLimbs(body, 1);
            _popup.PopupEntity(Loc.GetString("cyber-maintenance-close"), body, args.User);
        }

        Dirty(ent, comp);
    }

    private void OnWireInsertDoAfter(Entity<CyberneticsMaintenanceComponent> ent, ref CyberneticsWireInsertDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        var comp = ent.Comp;
        var body = ent.Owner;
        var used = args.Used;

        if (!comp.PanelOpen)
            return;

        var cyberCount = _body.GetAllOrgans(body).Count(o => HasComp<CyberLimbComponent>(o));
        if (comp.WiresInsertedCount >= cyberCount)
        {
            _popup.PopupClient(Loc.GetString("cyber-maintenance-no-wires-needed"), body, args.User);
            return;
        }

        if (used == null || !Exists(used) || !TryComp<StackComponent>(used, out var stack) || stack.Count < 1)
        {
            _popup.PopupClient(Loc.GetString("cyber-maintenance-insufficient-wires"), body, args.User);
            return;
        }

        if (!_stack.TryUse((used.Value, stack), 1))
        {
            _popup.PopupClient(Loc.GetString("cyber-maintenance-insufficient-wires"), body, args.User);
            return;
        }

        comp.WiresInsertedCount++;
        Dirty(ent, comp);

        if (comp.WiresInsertedCount >= cyberCount)
        {
            args.Repeat = false;
            _popup.PopupEntity(Loc.GetString("cyber-maintenance-complete"), body, args.User);
        }
        else
        {
            args.Repeat = Exists(used) && TryComp<StackComponent>(used, out var s) && s.Count > 0;
        }
    }

    private void ApplyPenaltyToCyberLimbs(EntityUid body, int amount)
    {
        foreach (var organ in _body.GetAllOrgans(body))
        {
            if (HasComp<CyberLimbComponent>(organ))
            {
                var ev = new SurgeryPenaltyAppliedEvent(organ, amount);
                RaiseLocalEvent(organ, ref ev);
            }
        }
    }

    private void RemovePenaltyFromCyberLimbs(EntityUid body, int amount)
    {
        foreach (var organ in _body.GetAllOrgans(body))
        {
            if (HasComp<CyberLimbComponent>(organ))
            {
                var ev = new SurgeryPenaltyRemovedEvent(organ, amount);
                RaiseLocalEvent(organ, ref ev);
            }
        }
    }
}
