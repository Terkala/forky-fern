using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using JetBrains.Annotations;

namespace Content.Shared._CE.MagicEnergy.Systems;

/// <summary>
/// CE mana mutations: raises overcharge/deficit events before delegating to <see cref="SharedBatterySystem"/>.
/// Use for all mage/modular-spell battery changes so Funky never patches <see cref="SharedBatterySystem.ChangeCharge"/> directly.
/// </summary>
public sealed class MagicBatterySystem : EntitySystem
{
    [Dependency] private readonly SharedBatterySystem _battery = default!;

    /// <inheritdoc cref="SharedBatterySystem.ChangeCharge"/>
    [PublicAPI]
    public float ChangeMagicCharge(Entity<BatteryComponent?> ent, float amount)
    {
        if (!Resolve(ent, ref ent.Comp))
            return 0;

        if (amount > 0 && ent.Comp.LastCharge + amount > ent.Comp.MaxCharge)
        {
            var overcharge = ent.Comp.LastCharge + amount - ent.Comp.MaxCharge;
            var overchargeEv = new CEEnergyOverchargeEvent(overcharge);
            RaiseLocalEvent(ent, ref overchargeEv);
        }

        if (amount < 0 && ent.Comp.LastCharge + amount < 0)
        {
            var deficit = -amount - ent.Comp.LastCharge;
            var deficitEv = new CEEnergyDeficitEvent(deficit);
            RaiseLocalEvent(ent, ref deficitEv);
        }

        return _battery.ChangeCharge(ent, amount);
    }
}
