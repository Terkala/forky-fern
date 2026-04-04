using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Blob;
using Robust.Shared.Prototypes;

namespace Content.Server.Blob;

/// <summary>
/// Grants starting incorporeal actions to new blob overminds.
/// </summary>
public sealed class BlobOvermindSystem : EntitySystem
{
    [Dependency] private readonly ActionContainerSystem _actions = default!;
    [Dependency] private readonly SharedActionsSystem _sharedActions = default!;

    private static readonly EntProtoId[] PreDeployActions =
    [
        "ActionBlobDeploy",
        "ActionBlobChangeColor",
    ];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlobOvermindComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<BlobOvermindComponent> ent, ref MapInitEvent args)
    {
        foreach (var id in PreDeployActions)
            _actions.AddAction(ent, id.Id);

        // Body has no MindComponent on this entity; ActionContainerSystem won't auto-grant to the action bar.
        if (TryComp<ActionsComponent>(ent, out var actionsComp) &&
            TryComp<ActionsContainerComponent>(ent, out var containerComp))
            _sharedActions.GrantContainedActions((ent.Owner, actionsComp), (ent.Owner, containerComp));
    }
}
