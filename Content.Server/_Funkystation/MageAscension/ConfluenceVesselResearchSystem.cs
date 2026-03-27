using Content.Server.Anomaly.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Research.Systems;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared.Research.Components;

namespace Content.Server._Funkystation.MageAscension;

/// <summary>
/// Passively grants research from a linked opened ley confluence through a containment vessel.
/// </summary>
public sealed class ConfluenceVesselResearchSystem : EntitySystem
{
    [Dependency] private readonly ResearchSystem _research = default!;

    public override void Update(float frameTime)
    {
        // AllEntityQuery: EntityQueryEnumerator skips EntityPaused entities; test maps / map init can pause grid children.
        var query = AllEntityQuery<AnomalyVesselComponent, ResearchClientComponent>();
        while (query.MoveNext(out var uid, out var vessel, out var client))
        {
            if (vessel.Confluence is not { } confluence)
                continue;

            if (!this.IsPowered(uid, EntityManager))
                continue;

            if (client.Server is not { } server || !this.IsPowered(server, EntityManager))
                continue;

            if (!TryComp<ConfluenceResearchHarvestComponent>(confluence, out var harvest) || harvest.PointsRemaining <= 0)
                continue;

            var rate = 5f * vessel.PointMultiplier;
            vessel.ConfluenceResearchAccumulator += rate * frameTime;
            var give = (int)float.Floor(vessel.ConfluenceResearchAccumulator);
            if (give <= 0)
                continue;

            give = int.Min(give, harvest.PointsRemaining);
            _research.ModifyServerPoints(server, give);
            harvest.PointsRemaining -= give;
            vessel.ConfluenceResearchAccumulator -= give;

            Dirty(confluence, harvest);

            if (harvest.PointsRemaining <= 0)
                QueueDel(confluence);
        }
    }
}
