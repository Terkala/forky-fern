using Content.Shared._CE.Actions.Spells;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Funkystation.Actions.Spells;

/// <summary>
/// Turns a dead mob target into an allied cluwne servant.
/// </summary>
public sealed partial class CESpellClownsurrection : CESpellEffect
{
    [DataField]
    public EntProtoId Spawn = "MobHonkCluwneMinion";

    public override void Effect(EntityManager entManager, CESpellEffectBaseArgs args)
    {
        if (args.User is null || args.Target is null)
            return;

        if (IoCManager.Resolve<INetManager>().IsClient)
            return;

        if (!entManager.TryGetComponent<MobStateComponent>(args.Target.Value, out var mobState) ||
            mobState.CurrentState != MobState.Dead)
            return;

        if (!entManager.TryGetComponent<TransformComponent>(args.Target.Value, out var transform))
            return;

        var cluwne = entManager.SpawnAtPosition(Spawn, transform.Coordinates);
        entManager.QueueDeleteEntity(args.Target.Value);

        var factions = entManager.System<NpcFactionSystem>();
        factions.IgnoreEntities(cluwne, new[] { args.User.Value });
    }
}
