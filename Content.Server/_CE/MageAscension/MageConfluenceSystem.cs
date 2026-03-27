using Content.Server.GameTicking;
using Content.Server.Research.Systems;
using Content.Shared.Research.Components;
using Content.Server.Station.Systems;
using Content.Shared._CE.MageAscension;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server._CE.MageAscension;

public sealed class MageConfluenceSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private static readonly TimeSpan RespawnDelay = TimeSpan.FromMinutes(2);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConfluenceComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ConfluenceComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnInteractUsing(Entity<ConfluenceComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Opened)
            return;

        if (!HasComp<MageOfAscensionComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("mage-confluence-fail-not-mage"), args.User, args.User);
            args.Handled = true;
            return;
        }

        if (!HasComp<MageGrimoireComponent>(args.Used))
        {
            _popup.PopupEntity(Loc.GetString("mage-confluence-fail-grimoire"), args.User, args.User);
            args.Handled = true;
            return;
        }

        OpenConfluence((ent.Owner, ent.Comp), args.User);
        args.Handled = true;
    }

    private void OpenConfluence(Entity<ConfluenceComponent> ent, EntityUid mage)
    {
        ent.Comp.Opened = true;
        ent.Comp.MageOnlyVisibility = false;
        Dirty(ent);

        if (TryComp<LeyLineSourceComponent>(ent, out var ley))
            ley.Enabled = true;

        if (TryComp<MageOfAscensionComponent>(mage, out var mageComp))
        {
            mageComp.ConfluencesOpened++;
            Dirty(mage, mageComp);

            var ev = new MageConfluenceOpenedEvent();
            RaiseLocalEvent(mage, ref ev);
        }

        if (TryComp<BatteryComponent>(mage, out var battery))
            _battery.SetMaxCharge((mage, battery), battery.MaxCharge + 10f);

        RemComp<ConfluenceMotePulseComponent>(ent.Owner);

        var harvest = EnsureComp<ConfluenceResearchHarvestComponent>(ent);
        harvest.PointsRemaining = 1000;
        Dirty(ent, harvest);

        var query = EntityQueryEnumerator<MageAscensionRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleUid, out var rule, out var gr))
        {
            if (!_ticker.IsGameRuleActive(ruleUid, gr))
                continue;

            rule.NextConfluenceSpawn = _timing.CurTime + RespawnDelay;
            if (rule.ActiveUnopenedConfluence == ent.Owner)
                rule.ActiveUnopenedConfluence = null;
        }

        _popup.PopupEntity(Loc.GetString("mage-confluence-opened"), mage, mage);
    }

    private void OnInteractHand(Entity<ConfluenceComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!ent.Comp.Opened)
            return;

        if (!TryComp<ConfluenceResearchHarvestComponent>(ent, out var harvest) || harvest.PointsRemaining <= 0)
            return;

        var station = _station.GetOwningStation(ent);
        if (station == null)
            return;

        EntityUid? serverEnt = null;
        var q = EntityQueryEnumerator<ResearchServerComponent, TransformComponent>();
        while (q.MoveNext(out var srvUid, out _, out _))
        {
            if (_station.GetOwningStation(srvUid) != station)
                continue;
            serverEnt = srvUid;
            break;
        }

        if (serverEnt == null)
        {
            _popup.PopupEntity(Loc.GetString("mage-confluence-research-no-server"), args.User, args.User);
            return;
        }

        var chunk = int.Min(100, harvest.PointsRemaining);
        _research.ModifyServerPoints(serverEnt.Value, chunk);
        harvest.PointsRemaining -= chunk;
        Dirty(ent, harvest);

        _popup.PopupEntity(Loc.GetString("mage-confluence-research-drain", ("amount", chunk)), args.User, args.User);

        if (harvest.PointsRemaining <= 0)
            QueueDel(ent.Owner);
    }
}
