using Content.IntegrationTests.Pair;
using Content.Server._Funkystation.MageAscension;
using Content.Server.Anomaly;
using Content.Server.Power.Components;
using Content.Server.Research.Systems;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared.Research.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Funkystation;

[TestFixture]
public sealed class ConfluenceVesselResearchTest
{
    [Test]
    public async Task LinkedConfluenceGeneratesResearchAndDrainsHarvest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();
        var map = await pair.CreateTestMap();

        var ent = server.EntMan;
        var research = ent.System<ResearchSystem>();
        EntityUid confluence = default;
        EntityUid serverUid = default;

        await server.WaitPost(() =>
        {
            var coords = map.MapCoords;

            confluence = ent.SpawnEntity("MageLeyConfluence", coords);
            var confComp = ent.GetComponent<ConfluenceComponent>(confluence);
            confComp.Opened = true;
            ent.Dirty(confluence, confComp);

            var harvest = ent.EnsureComponent<ConfluenceResearchHarvestComponent>(confluence);
            harvest.PointsRemaining = 1000;
            ent.Dirty(confluence, harvest);

            ent.EnsureComponent<ConfluenceVesselLinkComponent>(confluence);

            var vessel = ent.SpawnEntity("MachineAnomalyVessel", coords);
            serverUid = ent.SpawnEntity("ResearchAndDevelopmentServer", coords);

            // Without a power net, ApcPowerReceiver.Powered is cleared each tick; NeedsPower false matches "always powered" for tests.
            ent.GetComponent<ApcPowerReceiverComponent>(vessel).NeedsPower = false;
            ent.GetComponent<ApcPowerReceiverComponent>(serverUid).NeedsPower = false;

            research.RegisterClient(vessel, serverUid);

            ent.System<AnomalySystem>().SetVesselConfluenceForTesting(vessel, confluence);
        });

        await pair.RunSeconds(1.2f);

        await server.WaitAssertion(() =>
        {
            Assert.That(ent.TryGetComponent<ConfluenceResearchHarvestComponent>(confluence, out var harvest), Is.True);
            var srvComp = ent.GetComponent<ResearchServerComponent>(serverUid);
            Assert.That(srvComp.Points, Is.GreaterThan(0));
            Assert.That(harvest!.PointsRemaining, Is.LessThan(1000));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LinkedConfluenceDeletionWhenHarvestDepleted()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();
        var map = await pair.CreateTestMap();

        var ent = server.EntMan;
        var research = ent.System<ResearchSystem>();
        EntityUid confluence = default;

        await server.WaitPost(() =>
        {
            var coords = map.MapCoords;

            confluence = ent.SpawnEntity("MageLeyConfluence", coords);
            var confComp = ent.GetComponent<ConfluenceComponent>(confluence);
            confComp.Opened = true;
            ent.Dirty(confluence, confComp);

            var harvest = ent.EnsureComponent<ConfluenceResearchHarvestComponent>(confluence);
            harvest.PointsRemaining = 5;
            ent.Dirty(confluence, harvest);

            ent.EnsureComponent<ConfluenceVesselLinkComponent>(confluence);

            var vessel = ent.SpawnEntity("MachineAnomalyVessel", coords);
            var serverUid = ent.SpawnEntity("ResearchAndDevelopmentServer", coords);

            ent.GetComponent<ApcPowerReceiverComponent>(vessel).NeedsPower = false;
            ent.GetComponent<ApcPowerReceiverComponent>(serverUid).NeedsPower = false;

            research.RegisterClient(vessel, serverUid);

            ent.System<AnomalySystem>().SetVesselConfluenceForTesting(vessel, confluence);
        });

        await pair.RunSeconds(2f);

        await server.WaitAssertion(() =>
        {
            Assert.That(ent.Deleted(confluence), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
