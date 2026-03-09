using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client.Weapons.Misc;

/// <summary>
/// Draws the grappling hook rope from the gun to the hook, supporting bent paths and rendering when the hook is PVS-culled.
/// </summary>
public sealed class GrapplingRopeOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private readonly IEntityManager _entManager;

    public GrapplingRopeOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var worldHandle = args.WorldHandle;
        var spriteSystem = _entManager.System<SpriteSystem>();
        var xformSystem = _entManager.System<SharedTransformSystem>();
        var xformQuery = _entManager.GetEntityQuery<TransformComponent>();

        args.DrawingHandle.SetTransform(Matrix3x2.Identity);

        var query = _entManager.EntityQueryEnumerator<GrapplingGunComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var grappling, out var gunXform))
        {
            if (gunXform.MapID != args.MapId)
                continue;

            if (grappling.Projectile is not { } projectile)
                continue;

            // Use local gun position for smooth anchoring - avoids rope "jumping" when server state updates
            var gunPos = xformSystem.GetWorldPosition(gunXform);

            Vector2 hookPos;
            List<Vector2>? path = null;

            if (grappling.RopePath.Count >= 2)
            {
                path = grappling.RopePath;
                hookPos = path[^1];
            }
            else if (grappling.RopeEndPosition is { } ropeEnd)
            {
                hookPos = ropeEnd;
            }
            else if (xformQuery.TryGetComponent(projectile, out var hookXform))
            {
                hookPos = xformSystem.GetWorldPosition(hookXform);
            }
            else
            {
                continue;
            }

            var texture = spriteSystem.Frame0(grappling.RopeSprite);
            var width = texture.Width / (float) EyeManager.PixelsPerMeter;

            if (path != null && path.Count >= 2)
            {
                // First segment: use local gun position for smooth visuals, then follow server path
                DrawSegment(worldHandle, texture, width, gunPos, path[1]);
                for (var i = 1; i < path.Count - 1; i++)
                {
                    DrawSegment(worldHandle, texture, width, path[i], path[i + 1]);
                }
            }
            else
            {
                DrawSegment(worldHandle, texture, width, gunPos, hookPos);
            }
        }
    }

    private static void DrawSegment(
        DrawingHandleWorld worldHandle,
        Texture texture,
        float width,
        Vector2 posA,
        Vector2 posB)
    {
        var diff = posB - posA;
        var length = diff.Length();

        if (length < 0.001f)
            return;

        var midPoint = diff / 2f + posA;
        var angle = (posB - posA).ToWorldAngle();
        var box = new Box2(-width / 2f, -length / 2f, width / 2f, length / 2f);
        var rotate = new Box2Rotated(box.Translated(midPoint), angle, midPoint);

        worldHandle.DrawTextureRect(texture, rotate);
    }
}
