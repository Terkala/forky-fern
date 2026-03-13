using Content.Server.NPC.Companion.Components;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Doors.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Tag;

namespace Content.Server.NPC.Companion;

/// <summary>
/// Handles companion binding when a companion implant is injected into an NPC.
/// Converts any NPC into a companion: adds HTN with companion behavior, door bypass, and sets nav flags based on hands.
/// </summary>
public sealed class CompanionImplantInjectorSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NPCCompanionSystem _companion = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImplanterComponent, ImplantSuccessfulEvent>(OnImplantSuccessful);
    }

    private void OnImplantSuccessful(EntityUid uid, ImplanterComponent component, ImplantSuccessfulEvent args)
    {
        if (component.Implant?.Id != "CompanionImplant")
            return;

        var user = args.User;
        var target = args.Target;

        EnsureCompanionSetup(target);
        _companion.BindCompanion(user, target);
        SetCompanionNavFlags(target);

        if (_mobState.IsAlive(target))
            return;

        if (_mobState.HasState(target, Shared.Mobs.MobState.Alive))
            _mobState.ChangeMobState(target, Shared.Mobs.MobState.Alive, origin: user);
    }

    private void EnsureCompanionSetup(EntityUid target)
    {
        var htn = EnsureComp<HTNComponent>(target);
        htn.RootTask = new HTNCompoundTask { Task = "CompanionRootCompound" };
        EnsureComp<NPCDoorBypassStateComponent>(target);
    }

    /// <summary>
    /// Sets nav flags based on whether the companion has hands.
    /// With hands: can interact, pry, and smash doors.
    /// Without hands: only bump-open doors.
    /// All companions get DoorBumpTag so they can open bump doors when walking into them,
    /// which is needed to reach access doors for prying/hacking.
    /// </summary>
    private void SetCompanionNavFlags(EntityUid uid)
    {
        var hasHands = HasComp<HandsComponent>(uid);
        _npc.SetBlackboard(uid, NPCBlackboard.NavInteract, hasHands);
        _npc.SetBlackboard(uid, NPCBlackboard.NavPry, hasHands);
        _npc.SetBlackboard(uid, NPCBlackboard.NavSmash, hasHands);

        _tag.TryAddTag(uid, SharedDoorSystem.DoorBumpTag);
    }
}
