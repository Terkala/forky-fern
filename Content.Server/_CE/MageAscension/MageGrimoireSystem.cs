using Content.Shared._CE.MageAscension;
using Content.Shared.Interaction.Events;

namespace Content.Server._CE.MageAscension;

public sealed class MageGrimoireSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MageGrimoireComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<MageGrimoireComponent> ent, ref UseInHandEvent args)
    {
        ent.Comp.IsOpen = !ent.Comp.IsOpen;
        Dirty(ent);
        args.Handled = true;
    }
}
