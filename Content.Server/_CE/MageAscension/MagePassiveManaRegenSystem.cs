using Content.Shared._CE.MageAscension;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared._CE.MagicEnergy.Systems;
using Content.Shared.Inventory;
using Content.Shared.Power.Components;
using Robust.Shared.Timing;

namespace Content.Server._CE.MageAscension;

/// <summary>
/// Flat mana regen per design doc: 1/s + 0.1/s per confluence opened (replaces radiation + proximity ley for mages).
/// </summary>
public sealed class MagePassiveManaRegenSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MagicBatterySystem _magicBattery = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private TimeSpan _nextTick;
    private const float TickSeconds = 1f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextTick)
            return;
        _nextTick = _timing.CurTime + TimeSpan.FromSeconds(TickSeconds);

        var query = EntityQueryEnumerator<MageOfAscensionComponent, BatteryComponent>();
        while (query.MoveNext(out var uid, out var mage, out var battery))
        {
            if (_inventory.TryGetSlotEntity(uid, "gloves", out var gloves) && HasComp<LeadGauntletsComponent>(gloves))
                continue;

            var rate = 1f + 0.1f * mage.ConfluencesOpened;
            _magicBattery.ChangeMagicCharge((uid, battery), rate * TickSeconds);
        }
    }
}
