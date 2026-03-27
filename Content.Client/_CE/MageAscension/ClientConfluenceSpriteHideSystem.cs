using Content.Shared._CE.MageAscension;
using Content.Shared._CE.MageAscension.Components;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Client._CE.MageAscension;

/// <summary>
/// Hides dormant ley confluence sprites for clients whose local controlled entity is not a mage.
/// Opened confluences stay visible to everyone (matches <see cref="ConfluenceComponent.MageOnlyVisibility"/> intent).
/// </summary>
public sealed class ClientConfluenceSpriteHideSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        // ComponentInit is exclusive per component type; SharedConfluenceVisualSystem already subscribes for Confluence.
        SubscribeLocalEvent<ConfluenceComponent, ComponentStartup>(OnConfluenceStartup);
        SubscribeLocalEvent<ConfluenceComponent, ConfluenceStateHandledEvent>(OnConfluenceStateHandled);

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnLocalPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnLocalPlayerDetached);

        SubscribeLocalEvent<MageOfAscensionComponent, ComponentInit>(OnMageInit);
        SubscribeLocalEvent<MageOfAscensionComponent, ComponentShutdown>(OnMageShutdown);
    }

    private void OnConfluenceStateHandled(EntityUid uid, ConfluenceComponent comp, ConfluenceStateHandledEvent args)
    {
        UpdateVisibility((uid, comp));
    }

    private void OnConfluenceStartup(Entity<ConfluenceComponent> ent, ref ComponentStartup args)
    {
        UpdateVisibility(ent);
    }

    private void OnLocalPlayerAttached(LocalPlayerAttachedEvent ev)
    {
        RefreshAll();
    }

    private void OnLocalPlayerDetached(LocalPlayerDetachedEvent ev)
    {
        RefreshAll();
    }

    private void OnMageInit(Entity<MageOfAscensionComponent> ent, ref ComponentInit args)
    {
        if (IsLocalPlayer(ent.Owner))
            RefreshAll();
    }

    private void OnMageShutdown(Entity<MageOfAscensionComponent> ent, ref ComponentShutdown args)
    {
        if (IsLocalPlayer(ent.Owner))
            RefreshAll();
    }

    private bool IsLocalPlayer(EntityUid uid)
    {
        return _player.LocalSession?.AttachedEntity == uid;
    }

    private void RefreshAll()
    {
        var query = EntityQueryEnumerator<ConfluenceComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var conf, out _))
        {
            UpdateVisibility((uid, conf));
        }
    }

    private void UpdateVisibility(Entity<ConfluenceComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        var hide = ShouldHideDormantFromLocalViewer(ent.Comp);
        _sprite.SetVisible((ent.Owner, sprite), !hide);
    }

    private bool ShouldHideDormantFromLocalViewer(ConfluenceComponent comp)
    {
        if (comp.Opened || !comp.MageOnlyVisibility)
            return false;

        if (_player.LocalSession?.AttachedEntity is not { } viewer)
            return true;

        return !HasComp<MageOfAscensionComponent>(viewer);
    }
}
