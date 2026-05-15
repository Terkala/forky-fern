using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Roles;
using Content.Shared._CE.MageAscension;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.Players;
using Content.Shared.Roles.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._CE.MageAscension;

public sealed class MageAscensionRuleSystem : GameRuleSystem<MageAscensionRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly EntProtoId ActionBlink = "ActionMageUniversalBlink";
    private static readonly EntProtoId ActionCreateGrimoire = "ActionMageCreateGrimoire";
    private static readonly EntProtoId ConfluenceProto = "MageLeyConfluence";

    private static readonly TimeSpan ConfluenceRespawnDelay = TimeSpan.FromMinutes(2);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MageAscensionRuleComponent, AfterAntagEntitySelectedEvent>(OnAntagSelected);
        SubscribeLocalEvent<MageRoleComponent, GetBriefingEvent>(OnGetBriefing);
    }

    protected override void Started(EntityUid uid, MageAscensionRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        component.NextConfluenceSpawn = TimeSpan.Zero;
        component.ActiveUnopenedConfluence = null;
    }

    protected override void ActiveTick(EntityUid uid, MageAscensionRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.ActiveUnopenedConfluence is { } tracked)
        {
            if (TerminatingOrDeleted(tracked) || !Exists(tracked))
            {
                component.ActiveUnopenedConfluence = null;
                component.NextConfluenceSpawn = _timing.CurTime + ConfluenceRespawnDelay;
            }
            else if (TryComp<ConfluenceComponent>(tracked, out var conf) && !conf.Opened)
            {
                return;
            }
            else
            {
                component.ActiveUnopenedConfluence = null;
            }
        }

        if (_timing.CurTime < component.NextConfluenceSpawn)
            return;

        if (!TryFindRandomTile(out _, out _, out var grid, out var coords))
            return;

        var ent = Spawn(ConfluenceProto, coords);
        component.ActiveUnopenedConfluence = ent;

        var pulse = EnsureComp<ConfluenceMotePulseComponent>(ent);
        var stagger = _random.NextFloat(0f, (float)Math.Max(1.0, pulse.PulsePeriod.TotalSeconds));
        pulse.NextPulseAt = _timing.CurTime + TimeSpan.FromSeconds(stagger);
    }

    private void OnAntagSelected(Entity<MageAscensionRuleComponent> _, ref AfterAntagEntitySelectedEvent args)
    {
        _antag.SendBriefing(args.EntityUid, MakeBriefing(args.EntityUid), null, null);

        if (args.Session?.GetMind() is not { } mindId)
            return;

        EnsureComp<MageAscensionMindTrackerComponent>(mindId);

        EnsureMindAction(mindId, ActionBlink, stealable: true);
        EnsureMindAction(mindId, ActionCreateGrimoire, stealable: false);
    }

    private void EnsureMindAction(EntityUid mindId, EntProtoId actionProto, bool stealable)
    {
        if (!TryComp<ActionsContainerComponent>(mindId, out var container))
        {
            EnsureComp<ActionsContainerComponent>(mindId);
            container = Comp<ActionsContainerComponent>(mindId);
        }

        foreach (var actionId in container.Container.ContainedEntities)
        {
            if (MetaData(actionId).EntityPrototype?.ID == actionProto.Id)
                return;
        }

        var spawned = _actionContainer.AddAction(mindId, actionProto.Id);
        if (spawned == null || !stealable)
            return;

        var steal = EnsureComp<MageMindStealableActionsComponent>(mindId);
        steal.SpellActions.Add(spawned.Value);
    }

    private void OnGetBriefing(Entity<MageRoleComponent> _, ref GetBriefingEvent args)
    {
        var ent = args.Mind.Comp.OwnedEntity;
        if (ent == null)
            return;
        args.Append(MakeBriefing(ent.Value));
    }

    private string MakeBriefing(EntityUid ent)
    {
        var isHuman = HasComp<HumanoidProfileComponent>(ent);
        return isHuman
            ? Loc.GetString("mage-ascension-role-greeting-human")
            : Loc.GetString("mage-ascension-role-greeting-generic");
    }
}
