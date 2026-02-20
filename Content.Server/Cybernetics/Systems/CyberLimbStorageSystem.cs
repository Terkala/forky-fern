using Content.Server.Stack;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server.Cybernetics.Systems;

public sealed class CyberLimbStorageSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberLimbComponent, StorageInteractAttemptEvent>(OnStorageInteractAttempt);
        SubscribeLocalEvent<StorageComponent, EntGotInsertedIntoContainerMessage>(OnStorageInserted);
        SubscribeLocalEvent<CyberLimbComponent, ContainerIsInsertingAttemptEvent>(OnContainerInsertAttempt, before: [typeof(SharedStorageSystem)]);
    }

    private void OnStorageInteractAttempt(Entity<CyberLimbComponent> ent, ref StorageInteractAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_container.TryGetContainingContainer(ent.Owner, out var container))
            return;

        if (!HasComp<BodyComponent>(container.Owner) || container.ID != BodyComponent.ContainerID)
            return;

        args.Cancelled = true;
    }

    private void OnStorageInserted(Entity<StorageComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (!HasComp<CyberLimbComponent>(ent.Owner))
            return;

        if (args.Container.ID != BodyComponent.ContainerID)
            return;

        if (!HasComp<BodyComponent>(args.Container.Owner))
            return;

        _ui.CloseUi(ent.Owner, StorageComponent.StorageUiKey.Key);
    }

    private void OnContainerInsertAttempt(Entity<CyberLimbComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled || args.Container.ID != StorageComponent.ContainerId)
            return;

        if (!TryComp<StackComponent>(args.EntityUid, out var stack) || stack.Count <= 1)
            return;

        args.Cancel();

        var spawnPos = Transform(ent.Owner).Coordinates;
        var split = _stack.Split((args.EntityUid, stack), 1, spawnPos);
        if (split == null)
            return;

        _container.Insert(split.Value, args.Container);
    }
}
