using Content.Shared._Funkystation.LiquidBlob.Components;

namespace Content.Server._Funkystation.LiquidBlob;

public sealed class LiquidBlobProductionSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<LiquidBlobTileComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.LiquidLevel = Math.Min(comp.LiquidLevel + comp.ProductionPerSecond * frameTime, comp.MaxCapacity);
            Dirty(uid, comp);
        }
    }
}
