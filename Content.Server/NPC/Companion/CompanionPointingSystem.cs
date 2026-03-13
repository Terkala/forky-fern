using Content.Server.NPC.Companion.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Pointing;

namespace Content.Server.NPC.Companion;

/// <summary>
/// When the owner points at an entity, if that entity is not in the same faction as the owner,
/// all companions mark them hostile. Owner without NpcFactionMemberComponent: treat as "no faction" — allow aggro on any pointed target.
/// </summary>
public sealed class CompanionPointingSystem : EntitySystem
{
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CompanionOwnerComponent, AfterPointedAtEvent>(OnOwnerPointed);
    }

    private void OnOwnerPointed(EntityUid uid, CompanionOwnerComponent component, ref AfterPointedAtEvent args)
    {
        var pointed = args.Pointed;

        if (!Exists(pointed) || TerminatingOrDeleted(pointed))
            return;

        var allowAggro = true;
        if (TryComp<NpcFactionMemberComponent>(uid, out var ownerFaction) &&
            TryComp<NpcFactionMemberComponent>(pointed, out var pointedFaction))
        {
            allowAggro = !_npcFaction.IsEntityFriendly((uid, ownerFaction), (pointed, pointedFaction));
        }

        if (!allowAggro)
            return;

        foreach (var companion in component.Companions)
        {
            if (!Exists(companion) || TerminatingOrDeleted(companion))
                continue;

            _npcFaction.AggroEntity(companion, pointed);
        }
    }
}
