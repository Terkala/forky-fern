using Content.Server._Funkystation.MageAscension;
using Content.Server.GameTicking;
using Content.Shared._CE.MageAscension;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server._CE.MageAscension;

public sealed class MageConfluenceSystem : EntitySystem
{
    [Dependency] private readonly SharedConfluencePhysicsSystem _confluencePhysics = default!;
    [Dependency] private readonly SharedConfluenceVisualSystem _confluenceVisual = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MageDimensionalRiftSystem _dimensionalRift = default!;

    private static readonly TimeSpan RespawnDelay = TimeSpan.FromMinutes(2);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConfluenceComponent, InteractUsingEvent>(OnInteractUsing);
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
        _confluenceVisual.UpdateLeyLineAppearance(ent);
        _confluencePhysics.UpdateCollision(ent);

        if (TryComp<LeyLineSourceComponent>(ent, out var ley))
            ley.Enabled = true;

        if (TryComp<MageOfAscensionComponent>(mage, out var mageComp))
        {
            mageComp.ConfluencesOpened++;
            Dirty(mage, mageComp);

            var ev = new MageConfluenceOpenedEvent();
            RaiseLocalEvent(mage, ref ev);

            _dimensionalRift.TrySpawnDimensionalRiftIfEligible((mage, mageComp));

            if (_mind.TryGetMind(mage, out var mindId, out _))
            {
                var tracker = EnsureComp<MageAscensionMindTrackerComponent>(mindId);
                tracker.LeylinesOpenedCount = mageComp.ConfluencesOpened;
                Dirty(mindId, tracker);
            }
        }

        if (TryComp<BatteryComponent>(mage, out var battery))
            _battery.SetMaxCharge((mage, battery), battery.MaxCharge + 10f);

        RemComp<ConfluenceMotePulseComponent>(ent.Owner);

        var harvest = EnsureComp<ConfluenceResearchHarvestComponent>(ent);
        harvest.PointsRemaining = 1000;
        Dirty(ent, harvest);

        EnsureComp<ConfluenceVesselLinkComponent>(ent.Owner);

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
}
