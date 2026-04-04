using System.Numerics;
using Content.Shared.Blob;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Client.Blob;

/// <summary>
/// Plays a short wiggle on a blob tile when it participates in primary-attack lash.
/// </summary>
public sealed class BlobLashWiggleSystem : EntitySystem
{
    [Dependency] private readonly AnimationPlayerSystem _animationPlayer = default!;

    private const string AnimKey = "blob-lash-wiggle";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PlayBlobLashWiggleEvent>(OnWiggle);
    }

    private void OnWiggle(PlayBlobLashWiggleEvent msg)
    {
        var tile = GetEntity(msg.Tile);
        if (tile == EntityUid.Invalid || !TryComp(tile, out SpriteComponent? sprite))
            return;

        var player = EnsureComp<AnimationPlayerComponent>(tile);
        _animationPlayer.Stop(tile, player, AnimKey);

        var start = sprite.Offset;
        const float dur = 0.24f;
        var nudge = new Vector2(0.07f, 0.06f);

        var anim = new Animation
        {
            Length = TimeSpan.FromSeconds(dur),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(start, 0f),
                        new AnimationTrackProperty.KeyFrame(start + nudge, dur * 0.28f),
                        new AnimationTrackProperty.KeyFrame(start - nudge * 0.85f, dur * 0.55f),
                        new AnimationTrackProperty.KeyFrame(start, dur),
                    }
                }
            }
        };

        _animationPlayer.Play((tile, player), anim, AnimKey);
    }
}
