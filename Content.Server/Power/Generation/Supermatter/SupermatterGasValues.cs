using Content.Shared.Atmos;

namespace Content.Server.Power.Generation.Supermatter;

/// <summary>
/// Gas characteristic values for Supermatter. Per-mol contribution to each characteristic.
/// Values from design doc; loaded from YAML in future. Formula: C_raw = sum(mols[g] * value[g][C]) / 100.
/// </summary>
public static class SupermatterGasValues
{
    public readonly struct GasCharacteristicValues
    {
        public readonly float Stability;
        public readonly float Growth;
        public readonly float Conductivity;
        public readonly float Enthalpy;

        public GasCharacteristicValues(float stability, float growth, float conductivity, float enthalpy)
        {
            Stability = stability;
            Growth = growth;
            Conductivity = conductivity;
            Enthalpy = enthalpy;
        }
    }

    private static readonly GasCharacteristicValues[] Values = new GasCharacteristicValues[Atmospherics.TotalNumberOfGases];

    static SupermatterGasValues()
    {
        Values[(int)Gas.Oxygen] = new(0, -0.6f, -1f, 0);
        Values[(int)Gas.Nitrogen] = new(1, 0, 0, 0);
        Values[(int)Gas.CarbonDioxide] = new(-0.3f, 1, 0, 0.3f);
        Values[(int)Gas.Plasma] = new(-0.3f, -0.3f, 0, 1);
        Values[(int)Gas.Tritium] = new(-1, -0.2f, 0.2f, 0.2f);
        Values[(int)Gas.WaterVapor] = new(0.2f, -0.2f, 1, 0.2f);
        Values[(int)Gas.Ammonia] = new(0, -1, 0, -0.6f);
        Values[(int)Gas.NitrousOxide] = new(0.8f, 0, 0, -0.8f);
        Values[(int)Gas.Frezon] = new(-0.2f, -0.2f, -0.2f, -1);
        Values[(int)Gas.BZ] = new(-0.8f, 0, 0.8f, 0);
        Values[(int)Gas.Healium] = new(0.2f, 0.2f, 0, 0);
        Values[(int)Gas.Pluoxium] = new(0, -1, -0.6f, 0);
        Values[(int)Gas.Nitrium] = new(-1, -1, 1, 1);
        Values[(int)Gas.Hydrogen] = new(-0.4f, -0.4f, 0.4f, 0.4f);
        Values[(int)Gas.HyperNoblium] = new(0.8f, 0.4f, -0.4f, -0.4f);
        Values[(int)Gas.ProtoNitrate] = new(-0.5f, -0.5f, -0.5f, -0.5f);
        Values[(int)Gas.Zauker] = new(-0.5f, 0.5f, 0.5f, 0.5f);
        Values[(int)Gas.Halon] = new(0.5f, 0, 1, -0.5f);
        Values[(int)Gas.Helium] = new(0, 0, 0, 0);
        Values[(int)Gas.AntiNoblium] = new(-0.8f, -0.4f, 0.4f, 0.4f);
    }

    public static GasCharacteristicValues Get(Gas gas)
    {
        var idx = (int)gas;
        if (idx < 0 || idx >= Atmospherics.TotalNumberOfGases)
            return default;
        return Values[idx];
    }

    /// <summary>
    /// Gases produced by negative Growth, ordered by most negative growth first.
    /// N = floor((Power + 3000) / 3000) gas types produced per tick.
    /// </summary>
    public static readonly Gas[] NegativeGrowthProductionOrder =
    {
        Gas.Ammonia,
        Gas.Pluoxium,
        Gas.Nitrium,
        Gas.Oxygen,
        Gas.ProtoNitrate,
        Gas.Hydrogen,
        Gas.AntiNoblium,
        Gas.Plasma,
        Gas.Frezon,
        Gas.Tritium,
        Gas.WaterVapor,
    };
}
