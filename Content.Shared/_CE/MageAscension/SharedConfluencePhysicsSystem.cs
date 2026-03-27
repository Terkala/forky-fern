using Content.Shared._CE.MageAscension.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._CE.MageAscension;

/// <summary>
/// Ley confluences use no blocking collision until <see cref="ConfluenceComponent.Opened"/> is true.
/// </summary>
public sealed class SharedConfluencePhysicsSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedConfluenceVisualSystem _confluenceVisual = default!;

    private bool _subscriptionsRegistered;

    public override void Initialize()
    {
        base.Initialize();

        // AfterAutoHandleStateEvent is exclusive per component; duplicate Initialize on the same instance
        // (possible during client connect / pool edge cases) must not register twice.
        if (_subscriptionsRegistered)
            return;

        _subscriptionsRegistered = true;
        SubscribeLocalEvent<ConfluenceComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ConfluenceComponent, AfterAutoHandleStateEvent>(OnStateHandled);
    }

    public override void Shutdown()
    {
        _subscriptionsRegistered = false;
        base.Shutdown();
    }

    private void OnMapInit(Entity<ConfluenceComponent> ent, ref MapInitEvent args)
    {
        UpdateCollision(ent);
    }

    private void OnStateHandled(Entity<ConfluenceComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateCollision(ent);
        _confluenceVisual.UpdateLeyLineAppearance(ent);
        RaiseLocalEvent(ent.Owner, new ConfluenceStateHandledEvent());
    }

    public void UpdateCollision(Entity<ConfluenceComponent> ent)
    {
        if (!TryComp<PhysicsComponent>(ent.Owner, out var body))
            return;

        _physics.SetCanCollide(ent.Owner, ent.Comp.Opened, body: body);
    }
}
