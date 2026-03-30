using Robust.Shared.GameStates;

namespace Content.Shared._Funkystation.Projectile;

public enum BouncingSpellProjectileMode : byte
{
    Random = 0,
    NearbyTarget = 1,
}

/// <summary>
/// Marks a projectile that ricochets with CE spell rules until bounce or time caps.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BouncingSpellProjectileComponent : Component
{
    [DataField, AutoNetworkedField]
    public BouncingSpellProjectileMode Mode = BouncingSpellProjectileMode.NearbyTarget;

    [DataField, AutoNetworkedField]
    public int MaxBounces = 10;

    /// <summary>
    /// Number of bounces completed (incremented after each ricochet).
    /// </summary>
    [DataField, AutoNetworkedField]
    public int BounceCount;

    /// <summary>
    /// Absolute game time when the projectile despawns regardless of bounces.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ExpireAt;

    [DataField, AutoNetworkedField]
    public float ProjectileSpeed = 20f;

    [DataField, AutoNetworkedField]
    public float TargetSearchRadius = 10f;

    /// <summary>
    /// Per-bounce multiplier applied exponentially: damage scaled by <c>Pow(DamageFalloffPerBounce, BounceCount)</c>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DamageFalloffPerBounce = 1f;

    [DataField, AutoNetworkedField]
    public bool UseLosFilter;

    [DataField, AutoNetworkedField]
    public NetEntity Owner;
}
