using Content.Shared.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._CE.Actions.Spells;

/// <summary>
/// Spawns entities at the caster's feet and bonds them so they will not attack the caster (<see cref="NpcFactionSystem.IgnoreEntity"/>).
/// </summary>
public sealed partial class CESpellSpawnBondedEntitiesAtUser : CESpellEffect
{
    [DataField]
    public List<EntProtoId> Spawns = new();

    /// <summary>
    /// When true, rolls one random prototype from <see cref="Spawns"/> instead of spawning every entry.
    /// </summary>
    [DataField]
    public bool PickRandomSingle;

    public override void Effect(EntityManager entManager, CESpellEffectBaseArgs args)
    {
        if (args.User is null ||
            !entManager.TryGetComponent<TransformComponent>(args.User.Value, out var xform))
            return;

        var netMan = IoCManager.Resolve<INetManager>();
        if (netMan.IsClient)
            return;

        var coords = xform.Coordinates;
        var factions = entManager.System<NpcFactionSystem>();
        var random = IoCManager.Resolve<IRobustRandom>();

        IEnumerable<EntProtoId> iteration = Spawns;
        if (PickRandomSingle && Spawns.Count > 0)
            iteration = new[] { random.Pick(Spawns) };

        foreach (var proto in iteration)
        {
            var spawned = entManager.SpawnAtPosition(proto, coords);
            factions.IgnoreEntities(spawned, new[] { args.User.Value });
        }
    }
}
