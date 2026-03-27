using Content.Shared._CE.MageAscension.Components;
using Robust.Shared.GameObjects;

namespace Content.Shared._CE.MageAscension;

/// <summary>
/// Keeps ley confluence sprite in sync with <see cref="ConfluenceComponent.Opened"/>.
/// </summary>
public sealed class SharedConfluenceVisualSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConfluenceComponent, ComponentInit>(OnInit);
    }

    private void OnInit(Entity<ConfluenceComponent> ent, ref ComponentInit args)
    {
        UpdateLeyLineAppearance(ent);
    }

    public void UpdateLeyLineAppearance(Entity<ConfluenceComponent> ent)
    {
        var vis = ent.Comp.Opened
            ? ConfluenceLeyLineVisualState.Harvested
            : ConfluenceLeyLineVisualState.Dormant;

        _appearance.SetData(ent.Owner, ConfluenceVisuals.LeyLine, vis);
    }
}
