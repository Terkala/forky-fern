using Content.Shared.Blob;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using System.Linq;

namespace Content.Server.Blob;

/// <summary>
/// Applies hive-wide fire/poison resist upgrades to individual blob tiles via <see cref="DamageModifyEvent"/>.
/// </summary>
public sealed class BlobTileDefenseSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlobTileComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(EntityUid uid, BlobTileComponent tile, ref DamageModifyEvent args)
    {
        if (!TryGetEntity(tile.Hive, out var hiveUid) ||
            !TryComp<BlobHiveComponent>(hiveUid, out var hive))
        {
            return;
        }

        if (hive.HasFireResist && args.Damage.DamageDict.TryGetValue("Heat", out var heat))
            args.Damage.DamageDict["Heat"] = heat * FixedPoint2.New(0.5f);

        if (!hive.HasPoisonResist)
            return;

        foreach (var key in args.Damage.DamageDict.Keys.ToArray())
        {
            if (key is "Poison" or "Cellular" or "Radiation")
                args.Damage.DamageDict[key] *= FixedPoint2.New(0.65f);
        }
    }
}
