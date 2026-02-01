using Content.Shared.Nutrition.Components;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Cybernetics.Modules;

/// <summary>
/// Module component for Bio-Battery that converts hunger into battery charge.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BioBatteryModuleComponent : SpecialModuleComponent
{
    public BioBatteryModuleComponent()
    {
        ModuleType = SpecialModuleType.BioBattery;
    }

    /// <summary>
    /// Hunger units drained per second.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HungerDrainRate = 5.0f;

    /// <summary>
    /// Joules of battery charge gained per hunger unit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ChargeRate = 200.0f;

    /// <summary>
    /// Minimum hunger threshold below which the module will not drain hunger.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HungerThreshold MinimumHungerThreshold = HungerThreshold.Peckish;
}
