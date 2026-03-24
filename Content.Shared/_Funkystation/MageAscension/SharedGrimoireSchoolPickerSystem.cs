using Content.Shared._CE.MageAscension;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared.UserInterface;

namespace Content.Shared._Funkystation.MageAscension;

/// <summary>
/// Only mages may open the grimoire school picker UI.
/// </summary>
public sealed class SharedGrimoireSchoolPickerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MageGrimoireComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
    }

    private void OnOpenAttempt(Entity<MageGrimoireComponent> _, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!HasComp<MageOfAscensionComponent>(args.User))
            args.Cancel();
    }
}
