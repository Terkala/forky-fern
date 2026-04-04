using Content.Server.Antag;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.RoundEnd;
using Content.Server.Roles;
using Content.Shared.Blob;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.GameTicking.Rules;

public sealed class BlobRuleSystem : GameRuleSystem<BlobRuleComponent>
{
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly SharedMindSystem _sharedMind = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly EntityManager _ent = default!;
    [Dependency] private readonly AntagSelectionSystem _antagSelect = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagSelected);
        SubscribeLocalEvent<BlobRoleComponent, GetBriefingEvent>(OnGetBriefing);
    }

    private void OnGetBriefing(Entity<BlobRoleComponent> role, ref GetBriefingEvent args)
    {
        args.Append(Loc.GetString("blob-role-briefing"));
    }

    private void OnAfterAntagSelected(Entity<BlobRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (args.Session == null)
            return;

        var oldUid = args.EntityUid;
        if (!_ent.EntityExists(oldUid))
            return;

        // Already blob overmind (e.g. re-run)
        if (HasComp<BlobOvermindComponent>(oldUid) && HasComp<BlobHiveComponent>(oldUid))
            return;

        if (!HasComp<HumanoidProfileComponent>(oldUid))
            return;

        var coords = Transform(oldUid).Coordinates;
        var overmind = _ent.SpawnEntity("MobBlobOvermind", coords);

        if (!_sharedMind.TryGetMind(oldUid, out var mindId, out _))
        {
            QueueDel(overmind);
            return;
        }

        _mind.TransferTo(mindId, overmind, ghostCheckOverride: true);
        _ent.QueueDeleteEntity(oldUid);
        _antagSelect.SendBriefing(overmind, Loc.GetString("blob-role-briefing"), Color.LimeGreen, null);
    }

    protected override void Started(EntityUid uid, BlobRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        component.NextRoundEndCheck = _timing.CurTime + component.EndCheckDelay;
    }

    protected override void ActiveTick(EntityUid uid, BlobRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.NextRoundEndCheck == null || _timing.CurTime < component.NextRoundEndCheck)
            return;

        component.NextRoundEndCheck = _timing.CurTime + component.EndCheckDelay;
        CheckRoundEnd(uid, component);
    }

    private void CheckRoundEnd(EntityUid ruleUid, BlobRuleComponent rule)
    {
        var query = EntityQueryEnumerator<BlobHiveComponent, BlobOvermindComponent>();

        var anyHive = false;
        var blobWon = false;
        var allNucleiDead = true;

        while (query.MoveNext(out _, out var hive, out _))
        {
            if (!hive.Deployed)
                continue;

            anyHive = true;

            if (hive.LivingNuclei > 0)
                allNucleiDead = false;

            if (hive.TileCount >= rule.VictoryTileCount && hive.LivingNuclei > 0)
                blobWon = true;
        }

        if (!anyHive)
            return;

        if (blobWon)
        {
            _chat.DispatchGlobalAnnouncement(Loc.GetString("blob-round-end-blob-major"), colorOverride: Color.LimeGreen);
            _roundEnd.EndRound();
            return;
        }

        if (allNucleiDead)
        {
            _chat.DispatchGlobalAnnouncement(Loc.GetString("blob-round-end-crew-major"), colorOverride: Color.OrangeRed);
            _roundEnd.EndRound();
        }
    }

    protected override void AppendRoundEndText(EntityUid uid,
        BlobRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        var query = EntityQueryEnumerator<BlobHiveComponent>();
        while (query.MoveNext(out var ouid, out var hive))
        {
            if (!hive.Deployed)
                continue;

            var name = MetaData(ouid).EntityName;
            args.AddLine(Loc.GetString("blob-round-end-stats",
                ("tiles", hive.TileCount),
                ("bio", hive.BioPoints),
                ("evo", hive.EvoPoints),
                ("nuclei", hive.LivingNuclei)));

            if (_sharedMind.TryGetMind(ouid, out var mindId, out var mind) &&
                mind.UserId != null &&
                _player.TryGetSessionById(mind.UserId.Value, out var session))
            {
                args.AddLine(Loc.GetString("blob-round-end-overmind", ("name", name), ("user", session.Name)));
            }
        }
    }
}
