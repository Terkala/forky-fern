using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Network;

namespace Content.Shared.Blob;

public sealed class SharedBlobOvermindMeleeSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobOvermindComponent, BeforeLightAttackEvent>(OnBeforeLightAttack);
        SubscribeLocalEvent<BlobOvermindComponent, BeforeHeavyAttackEvent>(OnBeforeHeavyAttack);
    }

    private void OnBeforeLightAttack(EntityUid uid, BlobOvermindComponent _, ref BeforeLightAttackEvent args)
    {
        if (!TryComp<BlobHiveComponent>(uid, out var hive) || !hive.Deployed)
            return;

        args.Handled = true;

        if (_net.IsServer)
            RaiseLocalEvent(uid, new BlobPrimaryStrikeAttemptEvent(args.Coordinates));
    }

    private void OnBeforeHeavyAttack(EntityUid uid, BlobOvermindComponent _, ref BeforeHeavyAttackEvent args)
    {
        if (!TryComp<BlobHiveComponent>(uid, out var hive) || !hive.Deployed)
            return;

        args.Handled = true;
    }
}
