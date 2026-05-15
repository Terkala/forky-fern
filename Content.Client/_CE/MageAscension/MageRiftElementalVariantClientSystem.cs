using Content.Shared._CE.MageAscension.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client._CE.MageAscension;

/// <summary>
/// Client-side tint for the elemental rift horror (sprite lives on client).
/// </summary>
public sealed class MageRiftElementalVariantClientSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MageRiftElementalVariantComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MageRiftElementalVariantComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    private void OnStartup(Entity<MageRiftElementalVariantComponent> ent, ref ComponentStartup args)
    {
        ApplyTint(ent);
    }

    private void OnAfterState(Entity<MageRiftElementalVariantComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ApplyTint(ent);
    }

    private void ApplyTint(Entity<MageRiftElementalVariantComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var color = ent.Comp.Current switch
        {
            MageRiftElementKind.Fire => Color.FromHex("#ff6633"),
            MageRiftElementKind.Air => Color.FromHex("#ddeeff"),
            MageRiftElementKind.Earth => Color.FromHex("#8b5a2b"),
            MageRiftElementKind.Water => Color.FromHex("#3399ff"),
            _ => Color.White
        };

        sprite.Color = color;
    }
}
