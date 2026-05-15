using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Station.Systems;
using Content.Shared._CE.MageAscension;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared.Chat;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Station;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._CE.MageAscension;

public sealed class MageDimensionalRiftSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly StationSafeSpotSystem _safeSpot = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;

    private static readonly EntProtoId HorrorProtoFallback = "MobMageRiftHorror";
    private static readonly EntProtoId HorrorProtoHonk = "MobMageRiftBigHonk";
    private static readonly EntProtoId HorrorProtoElemental = "MobMageRiftElementalCreature";
    private static readonly EntProtoId BeastMassacreObjective = "MageRiftBeastMassacreObjective";
    private static readonly EntProtoId MindRoleBeast = "MindRoleMageRiftBeast";

    private bool _stationDimensionalRiftSpawned;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<MageDimensionalRiftComponent, InteractUsingEvent>(OnRiftInteractUsing);
        SubscribeLocalEvent<MageDimensionalRiftComponent, DoAfterAttemptEvent<MagePryRiftDoAfterEvent>>(OnPryAttempt);
        SubscribeLocalEvent<MagePryRiftDoAfterEvent>(OnPryFinished);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _stationDimensionalRiftSpawned = false;
    }

    /// <summary>
    /// Invoked from <see cref="MageConfluenceSystem"/> after a confluence is opened.
    /// </summary>
    public void TrySpawnDimensionalRiftIfEligible(Entity<MageOfAscensionComponent> ent)
    {
        // Integration tests use DummyTicker; Recycle flushes entities without RoundRestartCleanupEvent,
        // so reset the once-per-round flag when no station rift marker exists.
        if (_gameTicker.DummyTicker && !AnyStationDimensionalRiftMarker())
            _stationDimensionalRiftSpawned = false;

        if (_stationDimensionalRiftSpawned)
            return;

        if (ent.Comp.ConfluencesOpened < ent.Comp.LeylinesRequiredForDimensionalRift)
            return;

        MapCoordinates spawnCoords;
        EntityUid? announceStation = null;

        if (TryResolveStationForMage(ent.Owner, out var stationUid, out var stationData))
        {
            announceStation = stationUid;

            var spec = new StationSafeSpotLocateSpec
            {
                Station = (stationUid, stationData),
                FootprintWidth = 1,
                FootprintHeight = 1,
                LocalAnchor = Transform(ent).Coordinates,
                LocalHalfExtent = 12,
                StationWideStrictAttempts = 40,
            };

            if (_safeSpot.TryLocateSafeSpotOnStation(spec, out _, out _, out var localCoords, out _))
                spawnCoords = _xform.ToMapCoordinates(localCoords).Offset(new Vector2(0.6f, 0f));
            else
                spawnCoords = _xform.GetMapCoordinates(ent).Offset(new Vector2(0.6f, 0f));
        }
        else
        {
            spawnCoords = _xform.GetMapCoordinates(ent).Offset(new Vector2(0.6f, 0f));
        }

        var rift = Spawn("MageDimensionalRift", spawnCoords);
        _stationDimensionalRiftSpawned = true;

        EnsureComp<MageStationDimensionalRiftComponent>(rift);

        if (TryComp(rift, out MageDimensionalRiftComponent? riftComp))
        {
            riftComp.EmittedEmoteMask = 0;
        }

        _popup.PopupEntity(Loc.GetString("mage-dimensional-rift-spawned"), ent.Owner, ent.Owner, PopupType.LargeCaution);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/blink.ogg"), rift, audioParams: new AudioParams { Volume = -6f });

        var place = ResolveAnnouncementPlaceName(rift);
        var msg = Loc.GetString("mage-dimensional-rift-station-announcement", ("place", place));
        var sender = Loc.GetString("comms-console-announcement-title-centcom");

        if (_station.GetOwningStation(rift) is { } stationSrc)
            _chat.DispatchStationAnnouncement(stationSrc, msg, sender, colorOverride: Color.Gold);
        else if (announceStation is { } fallbackStation)
            _chat.DispatchStationAnnouncement(fallbackStation, msg, sender, colorOverride: Color.Gold);
    }

    private string ResolveAnnouncementPlaceName(EntityUid rift)
    {
        var xform = Transform(rift);
        if (xform.GridUid is { } grid && TryComp<MetaDataComponent>(grid, out var meta))
            return meta.EntityName;

        if (xform.MapUid is { } map && TryComp<MetaDataComponent>(map, out var mapMeta))
            return mapMeta.EntityName;

        return Loc.GetString("mage-dimensional-rift-announcement-place-unknown");
    }

    private bool AnyStationDimensionalRiftMarker()
    {
        var query = EntityQueryEnumerator<MageStationDimensionalRiftComponent>();
        return query.MoveNext(out _, out _);
    }

    private bool TryResolveStationForMage(EntityUid mage, out EntityUid stationUid, out StationDataComponent stationData)
    {
        stationUid = default;
        stationData = default!;

        var owning = _station.GetOwningStation(mage);
        if (owning != null && TryComp(owning.Value, out StationDataComponent? sd) && sd.Grids.Count > 0)
        {
            stationUid = owning.Value;
            stationData = sd;
            return true;
        }

        if (_safeSpot.TryGetRandomEventEligibleStation(out var randomStation)
            && TryComp(randomStation.Value, out StationDataComponent? rsData)
            && rsData.Grids.Count > 0)
        {
            stationUid = randomStation.Value;
            stationData = rsData;
            return true;
        }

        return false;
    }

    private void OnRiftInteractUsing(Entity<MageDimensionalRiftComponent> rift, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<MageStationDimensionalRiftComponent>(rift.Owner, out var stationRift))
        {
            _popup.PopupEntity(Loc.GetString("mage-dimensional-rift-fail-not-station-rift"), args.User, args.User);
            args.Handled = true;
            return;
        }

        if (stationRift.MonsterReleased)
        {
            _popup.PopupEntity(Loc.GetString("mage-dimensional-rift-fail-already-opened"), args.User, args.User);
            args.Handled = true;
            return;
        }

        if (stationRift.ActivePryingUser is { } busy && busy.IsValid() && busy != args.User)
        {
            _popup.PopupEntity(Loc.GetString("mage-dimensional-rift-fail-someone-else"), args.User, args.User);
            args.Handled = true;
            return;
        }

        if (!HasComp<MageOfAscensionComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("mage-dimensional-rift-fail-not-mage"), args.User, args.User);
            args.Handled = true;
            return;
        }

        if (!TryComp<MageGrimoireComponent>(args.Used, out var grim) || !grim.IsOpen)
        {
            _popup.PopupEntity(Loc.GetString("mage-dimensional-rift-fail-grimoire"), args.User, args.User);
            args.Handled = true;
            return;
        }

        if (!_hands.IsHolding(args.User, args.Used))
        {
            _popup.PopupEntity(Loc.GetString("mage-dimensional-rift-fail-grimoire"), args.User, args.User);
            args.Handled = true;
            return;
        }

        var da = new DoAfterArgs(EntityManager, args.User, rift.Comp.PryDuration, new MagePryRiftDoAfterEvent(), rift.Owner,
            target: rift.Owner, used: args.Used)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            Broadcast = true,
            EventTarget = rift.Owner,
            AttemptFrequency = AttemptFrequency.EveryTick,
        };

        if (_doAfter.TryStartDoAfter(da))
        {
            rift.Comp.EmittedEmoteMask = 0;
            Dirty(rift);

            stationRift.ActivePryingUser = args.User;
            Dirty(rift.Owner, stationRift);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("mage-dimensional-rift-fail-busy"), args.User, args.User);
        }

        args.Handled = true;
    }

    private void OnPryAttempt(Entity<MageDimensionalRiftComponent> rift, ref DoAfterAttemptEvent<MagePryRiftDoAfterEvent> args)
    {
        if (args.Cancelled)
            return;

        var doAfter = args.DoAfter;
        var elapsed = _timing.CurTime - doAfter.StartTime;
        var total = doAfter.Args.Delay;

        for (var i = 0; i < 4; i++)
        {
            var bit = 1 << i;
            if ((rift.Comp.EmittedEmoteMask & bit) != 0)
                continue;

            var frac = (i + 1) / 5f;
            if (elapsed < total * frac)
                continue;

            rift.Comp.EmittedEmoteMask |= (byte) bit;
            Dirty(rift);

            _chat.TrySendInGameICMessage(doAfter.Args.User, Loc.GetString($"mage-dimensional-rift-pry-emote-{i + 1}"), InGameICChatType.Emote,
                ChatTransmitRange.Normal, hideLog: false, ignoreActionBlocker: true);
        }
    }

    private void OnPryFinished(MagePryRiftDoAfterEvent ev)
    {
        if (ev.Target is not { } riftUid || !TryComp<MageDimensionalRiftComponent>(riftUid, out _))
            return;

        if (TryComp<MageStationDimensionalRiftComponent>(riftUid, out var stationRift)
            && stationRift.ActivePryingUser == ev.User)
        {
            stationRift.ActivePryingUser = null;
            Dirty(riftUid, stationRift);
        }

        if (ev.Cancelled)
            return;

        var user = ev.User;
        if (!TryComp<MageOfAscensionComponent>(user, out var mage))
            return;

        if (ev.Used is not { } grimoire || !TryComp<MageGrimoireComponent>(grimoire, out var g) || !g.IsOpen)
            return;

        if (!TryComp<MageStationDimensionalRiftComponent>(riftUid, out var marker))
            return;

        if (marker.MonsterReleased)
            return;

        marker.MonsterReleased = true;
        Dirty(riftUid, marker);

        if (!_mind.TryGetMind(user, out var mindId, out var mind))
            return;

        var tracker = EnsureComp<MageAscensionMindTrackerComponent>(mindId);
        tracker.CompletedDimensionalRiftAscension = true;
        Dirty(mindId, tracker);

        var horrorProto = ResolveHorrorPrototype(mage.SchoolId);
        var horror = Spawn(horrorProto, _xform.GetMapCoordinates(riftUid));

        while (mind.Objectives.Count > 0)
        {
            _mind.TryRemoveObjective(mindId, mind, 0);
        }

        _role.MindRemoveRole<MageRoleComponent>(mindId);
        _role.MindAddRole(mindId, MindRoleBeast, mind, silent: false);

        _mind.TransferTo(mindId, horror, mind: mind, createGhost: false);
        _mind.TryAddObjective(mindId, mind, BeastMassacreObjective);

        QueueDel(user);
        QueueDel(riftUid);

        _popup.PopupEntity(Loc.GetString("mage-dimensional-rift-ascension-complete"), horror, PopupType.LargeCaution);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/blink.ogg"), horror);
    }

    private static EntProtoId ResolveHorrorPrototype(string? schoolId)
    {
        if (string.Equals(schoolId, "Honkamancy", StringComparison.OrdinalIgnoreCase))
            return HorrorProtoHonk;

        if (string.Equals(schoolId, "Elementalism", StringComparison.OrdinalIgnoreCase))
            return HorrorProtoElemental;

        return HorrorProtoFallback;
    }
}
