using Content.Server.Radiation.Components;
using Content.Shared._CE.MagicEnergy.Components;
using Content.Shared._CE.MagicEnergy.Systems;
using Content.Shared.Power.Components;
using Robust.Shared.Timing;

namespace Content.Server._CE.MagicEnergy;

/// <summary>
/// Radiation → mana for entities with <see cref="CEEnergyRadiationRegenerationComponent"/> (e.g. CE elf).
/// Mages of Ascension use passive mana regen instead; do not give them radiation regen.
/// </summary>
public sealed partial class CEMagicEnergySystem : CESharedMagicEnergySystem
{
    [Dependency] private readonly MagicBatterySystem _magicBattery = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RadiationReceiverComponent, CEEnergyRadiationRegenerationComponent, BatteryComponent>();
        while (query.MoveNext(out var uid, out var radReceiver, out var energyRegen, out var battery))
        {
            if (_timing.CurTime < energyRegen.NextUpdate)
                continue;
            energyRegen.NextUpdate = _timing.CurTime + energyRegen.UpdateFrequency;

            var change = radReceiver.CurrentRadiation * energyRegen.Energy;
            if (change == 0)
                continue;

            _magicBattery.ChangeMagicCharge((uid, battery), change);
        }
    }
}
