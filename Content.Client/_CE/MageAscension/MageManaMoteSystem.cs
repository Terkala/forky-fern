using System.Numerics;
using Content.Shared._CE.MageAscension.Components;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._CE.MageAscension;

/// <summary>
/// Mana mote visuals: pulse scheduling and motion run only for the local player when they are a mage.
/// </summary>
public sealed class MageManaMoteSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly EntProtoId MoteProto = "MageManaMote";

    private readonly Dictionary<EntityUid, TimeSpan> _nextLocalPulseAt = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConfluenceMotePulseComponent, ComponentShutdown>(OnPulseShutdown);
    }

    private void OnPulseShutdown(EntityUid uid, ConfluenceMotePulseComponent _, ComponentShutdown args)
    {
        _nextLocalPulseAt.Remove(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_playerManager.LocalSession?.AttachedEntity is not { } player ||
            !HasComp<MageOfAscensionComponent>(player))
        {
            return;
        }

        var now = _timing.CurTime;
        ProcessSchedulers(now);
        ProcessMoteMovement(frameTime);
    }

    private void ProcessSchedulers(TimeSpan now)
    {
        var pulseQuery = EntityQueryEnumerator<ConfluenceMotePulseComponent, ConfluenceComponent, TransformComponent>();
        while (pulseQuery.MoveNext(out var uid, out var pulse, out var conf, out var xform))
        {
            if (conf.Opened)
                continue;

            if (!_nextLocalPulseAt.TryGetValue(uid, out var nextAt))
            {
                nextAt = pulse.NextPulseAt;
                _nextLocalPulseAt[uid] = nextAt;
            }

            if (now < nextAt)
                continue;

            var period = pulse.PulsePeriod <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : pulse.PulsePeriod;
            _nextLocalPulseAt[uid] = now + period;

            if (xform.MapID == MapId.Nullspace)
                continue;

            if (!TryPickMoteTarget(uid, xform.MapID, out var target))
                continue;

            var coords = _transform.GetMapCoordinates(uid, xform);
            var mote = Spawn(MoteProto, coords);
            if (!TryComp<MageManaMoteComponent>(mote, out var moteComp))
                continue;
            moteComp.Target = target;
            if (HasComp<MageOfAscensionComponent>(target))
                moteComp.StaticHomingWorld = null;
            else
                moteComp.StaticHomingWorld = _transform.GetWorldPosition(target);
        }
    }

    private bool TryPickMoteTarget(EntityUid source, MapId sourceMap, out EntityUid target)
    {
        target = default;

        var mageCount = 0;
        var mq = EntityQueryEnumerator<MageOfAscensionComponent, TransformComponent>();
        while (mq.MoveNext(out _, out _, out var xf))
        {
            if (xf.MapID == sourceMap)
                mageCount++;
        }

        var leyCount = 0;
        var lq = EntityQueryEnumerator<ConfluenceComponent, TransformComponent>();
        while (lq.MoveNext(out var leyUid, out _, out var xf))
        {
            if (leyUid == source || xf.MapID != sourceMap)
                continue;
            leyCount++;
        }

        var total = mageCount + leyCount;
        if (total == 0)
            return false;

        var pick = _random.Next(total);
        if (pick < mageCount)
        {
            var i = 0;
            mq = EntityQueryEnumerator<MageOfAscensionComponent, TransformComponent>();
            while (mq.MoveNext(out var mageUid, out _, out var xf))
            {
                if (xf.MapID != sourceMap)
                    continue;
                if (i++ == pick)
                {
                    target = mageUid;
                    return true;
                }
            }

            return false;
        }

        pick -= mageCount;
        var j = 0;
        lq = EntityQueryEnumerator<ConfluenceComponent, TransformComponent>();
        while (lq.MoveNext(out var leyUid, out _, out var xf))
        {
            if (leyUid == source || xf.MapID != sourceMap)
                continue;
            if (j++ == pick)
            {
                target = leyUid;
                return true;
            }
        }

        return false;
    }

    private void ProcessMoteMovement(float frameTime)
    {
        var moteMove = EntityQueryEnumerator<MageManaMoteComponent, TransformComponent>();
        while (moteMove.MoveNext(out var moteUid, out var mote, out var moteXform))
        {
            if (!Exists(mote.Target) || moteXform.MapID == MapId.Nullspace)
            {
                QueueDel(moteUid);
                continue;
            }

            Vector2 targetPos;
            if (mote.StaticHomingWorld is { } fixedGoal)
                targetPos = fixedGoal;
            else
                targetPos = _transform.GetWorldPosition(mote.Target);

            var selfPos = _transform.GetWorldPosition(moteUid);
            var delta = targetPos - selfPos;
            var len = delta.Length();
            if (len < 0.15f)
            {
                QueueDel(moteUid);
                continue;
            }

            var step = mote.Speed * frameTime;
            var dir = delta / len;
            var newPos = selfPos + dir * MathF.Min(step, len);
            _transform.SetWorldPosition((moteUid, moteXform), newPos);
        }
    }
}
