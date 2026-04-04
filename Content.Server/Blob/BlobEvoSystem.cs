using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Blob;
namespace Content.Server.Blob;

/// <summary>
/// Handles blob Evo shop purchases via InstantActions carrying <see cref="BlobEvoActionComponent"/>.
/// </summary>
public sealed class BlobEvoSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlobEvoPurchaseActionEvent>(OnPurchase);
    }

    private void OnPurchase(BlobEvoPurchaseActionEvent ev)
    {
        if (ev.Handled || !TryComp<BlobHiveComponent>(ev.Performer, out var hive) || !hive.Deployed)
            return;

        if (!TryComp<BlobEvoActionComponent>(ev.Action, out var evo))
            return;

        var hiveUid = ev.Performer;
        var cost = GetEvoCost(evo.Kind, hive);
        if (hive.EvoPoints < cost)
        {
            _popup.PopupEntity(Loc.GetString("blob-not-enough-evo"), ev.Performer, ev.Performer);
            return;
        }

        if (!TryApply(evo.Kind, hiveUid, hive))
        {
            _popup.PopupEntity(Loc.GetString("blob-evo-failed"), ev.Performer, ev.Performer);
            return;
        }

        ev.Handled = true;
        hive.EvoPoints -= cost;
        Dirty(hiveUid, hive);
    }

    private static int GetEvoCost(BlobEvoKind kind, BlobHiveComponent hive)
    {
        return kind switch
        {
            BlobEvoKind.GenRate => 1 + hive.GenRatePurchases,
            BlobEvoKind.QuickSpread => 3 + 4 * hive.QuickSpreadPurchases,
            BlobEvoKind.SpreadChance => 1 + hive.SpreadChancePurchases,
            BlobEvoKind.Attack => 1 + hive.AttackPurchases,
            BlobEvoKind.FireResist => 2,
            BlobEvoKind.PoisonResist => 2,
            BlobEvoKind.UnlockDevour => 1,
            BlobEvoKind.UnlockBridge => 1,
            BlobEvoKind.UnlockLauncher => 1,
            BlobEvoKind.UnlockPlasmaphyll => 1,
            BlobEvoKind.UnlockEctothermid => 2,
            BlobEvoKind.UnlockReflective => 1,
            _ => 9999,
        };
    }

    private bool TryApply(BlobEvoKind kind, EntityUid hiveUid, BlobHiveComponent hive)
    {
        switch (kind)
        {
            case BlobEvoKind.GenRate:
                hive.GenRatePurchases++;
                hive.GenerationPerSecond += 2f;
                return true;
            case BlobEvoKind.QuickSpread:
                hive.QuickSpreadPurchases++;
                return true;
            case BlobEvoKind.SpreadChance:
                hive.SpreadChancePurchases++;
                return true;
            case BlobEvoKind.Attack:
                hive.AttackPurchases++;
                return true;
            case BlobEvoKind.FireResist:
                if (hive.HasFireResist)
                    return false;
                hive.HasFireResist = true;
                return true;
            case BlobEvoKind.PoisonResist:
                if (hive.HasPoisonResist)
                    return false;
                hive.HasPoisonResist = true;
                return true;
            case BlobEvoKind.UnlockDevour:
                if (hive.UnlockDevour)
                    return false;
                hive.UnlockDevour = true;
                return true;
            case BlobEvoKind.UnlockBridge:
                if (hive.UnlockBridge)
                    return false;
                hive.UnlockBridge = true;
                return true;
            case BlobEvoKind.UnlockLauncher:
                if (hive.UnlockLauncher)
                    return false;
                hive.UnlockLauncher = true;
                return true;
            case BlobEvoKind.UnlockPlasmaphyll:
                if (hive.UnlockPlasmaphyll)
                    return false;
                hive.UnlockPlasmaphyll = true;
                return true;
            case BlobEvoKind.UnlockEctothermid:
                if (hive.UnlockEctothermid)
                    return false;
                hive.UnlockEctothermid = true;
                return true;
            case BlobEvoKind.UnlockReflective:
                if (hive.UnlockReflective)
                    return false;
                hive.UnlockReflective = true;
                return true;
            default:
                return false;
        }
    }
}
