using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Server.Body.Part;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Body.Systems;

/// <summary>
/// System that handles limb regeneration for slime species.
/// Slimes automatically regenerate lost limbs after 1 minute, spawning at 5% health and healing to 100% over 4 minutes.
/// </summary>
public sealed class SlimeLimbRegenerationSystem : EntitySystem
{
    [Dependency] private readonly BodyPartSystem _bodyPartSystem = default!;
    [Dependency] private readonly BodySystem _bodySystem = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedBodyPartSystem _sharedBodyPartSystem = default!;

    private const float RegenerationDelaySeconds = 60f; // 1 minute
    private const float HealingDurationSeconds = 240f; // 4 minutes

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, BodyPartFullyDetachedEvent>(OnBodyPartFullyDetached);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<SlimeLimbRegenerationTrackerComponent, BodyComponent>();

        while (query.MoveNext(out var bodyUid, out var tracker, out var body))
        {
            // Check if body is still a slime
            if (!IsSlimeSpecies(bodyUid))
            {
                RemComp<SlimeLimbRegenerationTrackerComponent>(bodyUid);
                continue;
            }

            // Iterate over each tracked limb
            var limbsToRemove = new List<SlimeLimbKey>();

            foreach (var (key, regenData) in tracker.RegeneratingLimbs)
            {
                var partType = key.PartType;
                var symmetry = key.Symmetry;

                // Check if regeneration should start (1 minute after limb loss)
                if (!regenData.IsHealing && curTime >= regenData.RegenerationStartTime)
                {
                    SpawnRegeneratedLimb(bodyUid, partType, symmetry, regenData);
                }

                // Check if healing phase is active
                if (regenData.IsHealing && regenData.RegeneratedPart != null)
                {
                    var shouldRemove = HealRegeneratedLimb(bodyUid, partType, symmetry, regenData, curTime);
                    if (shouldRemove)
                    {
                        limbsToRemove.Add(key);
                    }
                }
            }

            // Remove completed or invalid limbs
            foreach (var key in limbsToRemove)
            {
                tracker.RegeneratingLimbs.Remove(key);
            }

            // Remove tracker if no limbs are regenerating
            if (tracker.RegeneratingLimbs.Count == 0)
            {
                RemComp<SlimeLimbRegenerationTrackerComponent>(bodyUid);
            }
        }
    }

    private void OnBodyPartFullyDetached(Entity<BodyComponent> body, ref BodyPartFullyDetachedEvent args)
    {
        // Only process slime species
        if (!IsSlimeSpecies(body))
            return;

        var part = args.BodyPart.Comp;

        // Only regenerate arms and legs (not head or torso)
        if (part.PartType != BodyPartType.Arm && part.PartType != BodyPartType.Leg)
            return;

        // Get or create tracker component
        var tracker = EnsureComp<SlimeLimbRegenerationTrackerComponent>(body);

        // Create key for this limb
        var limbKey = new SlimeLimbKey(part.PartType, part.Symmetry);

        // Only add if this limb is not already regenerating
        if (tracker.RegeneratingLimbs.ContainsKey(limbKey))
        {
            // Already regenerating this limb, don't overwrite
            return;
        }

        // Add new regeneration entry for this limb
        tracker.RegeneratingLimbs[limbKey] = new SlimeLimbRegenerationData
        {
            RegenerationStartTime = _timing.CurTime + TimeSpan.FromSeconds(RegenerationDelaySeconds),
            HealingStartTime = TimeSpan.Zero,
            RegeneratedPart = null,
            IsHealing = false
        };
    }

    private void SpawnRegeneratedLimb(EntityUid body, BodyPartType partType, BodyPartSymmetry symmetry, SlimeLimbRegenerationData regenData)
    {
        // Get the prototype ID for the limb based on type and symmetry
        var prototypeId = GetLimbPrototypeId(partType, symmetry);
        if (prototypeId == null)
            return;

        // Spawn the limb entity
        var limbEntity = Spawn(prototypeId, Transform(body).Coordinates);

        // Mark as regenerating limb so shared system allows attachment
        EnsureComp<Content.Shared.Body.Part.RegeneratingLimbComponent>(limbEntity);

        // Find parent part (torso for arms/legs)
        var torso = _bodySystem.GetBodyChildrenOfType(body, BodyPartType.Torso).FirstOrDefault();
        if (torso.Id == default)
        {
            Del(limbEntity);
            return;
        }

        // Get slot ID based on part type and symmetry
        var slotId = GetSlotId(partType, symmetry);

        // Attach the limb
        if (!_bodyPartSystem.AttachBodyPart(body, limbEntity, slotId, torso.Id))
        {
            Del(limbEntity);
            return;
        }

        // Set initial damage to 95% of max health
        if (TryComp<DamageableComponent>(limbEntity, out var damageable))
        {
            SetInitialDamage(limbEntity, damageable);
        }

        // Transition to healing phase
        regenData.RegeneratedPart = GetNetEntity(limbEntity);
        regenData.IsHealing = true;
        regenData.HealingStartTime = _timing.CurTime;
    }

    private void SetInitialDamage(EntityUid limb, DamageableComponent damageable)
    {
        // Calculate 95% damage by applying damage to all supported damage types
        // We'll apply a high damage value that should be sufficient for most body parts
        var damageSpec = new DamageSpecifier();

        if (damageable.DamageContainerID != null && _prototype.TryIndex<DamageContainerPrototype>(damageable.DamageContainerID, out var container))
        {
            // Apply damage to all supported types
            foreach (var typeId in container.SupportedTypes)
            {
                damageSpec.DamageDict[typeId] = FixedPoint2.New(1000); // High value to ensure we hit max
            }

            foreach (var groupId in container.SupportedGroups)
            {
                if (_prototype.TryIndex<DamageGroupPrototype>(groupId, out var group))
                {
                    foreach (var typeId in group.DamageTypes)
                    {
                        damageSpec.DamageDict[typeId] = FixedPoint2.New(1000);
                    }
                }
            }
        }
        else
        {
            // Fallback: apply to common damage types
            damageSpec.DamageDict["Blunt"] = FixedPoint2.New(1000);
            damageSpec.DamageDict["Slash"] = FixedPoint2.New(1000);
            damageSpec.DamageDict["Piercing"] = FixedPoint2.New(1000);
        }

        // Apply damage to max out the limb
        _damageable.ChangeDamage(limb, damageSpec, ignoreResistances: true);

        // Now heal it back to 5% health
        if (TryComp<DamageableComponent>(limb, out var updatedDamageable))
        {
            var totalDamage = updatedDamageable.TotalDamage;
            var maxDamage = totalDamage; // Current damage is at max
            var targetDamage = maxDamage * FixedPoint2.New(0.95f); // 95% of max
            var healAmount = totalDamage - targetDamage;

            // Heal by the calculated amount
            var healSpec = new DamageSpecifier();
            foreach (var (type, value) in updatedDamageable.Damage.DamageDict)
            {
                if (value > 0)
                {
                    // Distribute healing proportionally
                    var proportion = value / totalDamage;
                    var healForType = healAmount * proportion;
                    healSpec.DamageDict[type] = -healForType; // Negative for healing
                }
            }

            _damageable.ChangeDamage(limb, healSpec, ignoreResistances: true);
        }
    }

    /// <summary>
    /// Heals a regenerated limb over time.
    /// </summary>
    /// <returns>True if the limb should be removed from tracking (fully healed or destroyed)</returns>
    private bool HealRegeneratedLimb(EntityUid body, BodyPartType partType, BodyPartSymmetry symmetry, SlimeLimbRegenerationData regenData, TimeSpan curTime)
    {
        if (regenData.RegeneratedPart == null || !TryGetEntity(regenData.RegeneratedPart.Value, out var limb))
        {
            // Limb was destroyed, cancel regeneration for this limb
            return true;
        }

        if (!TryComp<DamageableComponent>(limb, out var damageable))
        {
            // Limb lost damageable component, remove from tracking
            return true;
        }

        // Check if already fully healed
        if (damageable.TotalDamage <= 0)
        {
            // Fully healed, remove from tracking
            return true;
        }

        // Calculate healing progress (0 to 1 over 4 minutes)
        var elapsed = curTime - regenData.HealingStartTime;
        var progress = (float)(elapsed.TotalSeconds / HealingDurationSeconds);

        if (progress >= 1.0f)
        {
            // Fully heal and remove from tracking
            var finalHeal = new DamageSpecifier();
            foreach (var (type, value) in damageable.Damage.DamageDict)
            {
                if (value > 0)
                {
                    finalHeal.DamageDict[type] = -value; // Negative for healing
                }
            }
            _damageable.ChangeDamage((limb.Value, damageable), finalHeal, ignoreResistances: true);
            return true;
        }

        // Calculate target damage (from 95% to 0% over 4 minutes)
        var maxDamage = GetMaxDamage(damageable);
        var targetDamage = maxDamage * FixedPoint2.New(0.95f * (1.0f - progress));

        // Heal proportionally across all damage types
        var currentTotal = damageable.TotalDamage;
        if (currentTotal > targetDamage)
        {
            var healAmount = currentTotal - targetDamage;
            var healSpec = new DamageSpecifier();

            foreach (var (type, value) in damageable.Damage.DamageDict)
            {
                if (value > 0)
                {
                    var proportion = value / currentTotal;
                    var healForType = healAmount * proportion;
                    healSpec.DamageDict[type] = -healForType; // Negative for healing
                }
            }

            _damageable.ChangeDamage((limb.Value, damageable), healSpec, ignoreResistances: true);
        }

        return false; // Continue tracking this limb
    }

    private FixedPoint2 GetMaxDamage(DamageableComponent damageable)
    {
        // Estimate max damage by summing all current damage types
        // This is an approximation - in practice, we'd need to know the actual max from the prototype
        var total = FixedPoint2.Zero;
        foreach (var value in damageable.Damage.DamageDict.Values)
        {
            total += value;
        }
        return total > 0 ? total : FixedPoint2.New(100); // Fallback estimate
    }

    private string? GetLimbPrototypeId(BodyPartType partType, BodyPartSymmetry symmetry)
    {
        return (partType, symmetry) switch
        {
            (BodyPartType.Arm, BodyPartSymmetry.Left) => "OrganSlimePersonArmLeft",
            (BodyPartType.Arm, BodyPartSymmetry.Right) => "OrganSlimePersonArmRight",
            (BodyPartType.Leg, BodyPartSymmetry.Left) => "OrganSlimePersonLegLeft",
            (BodyPartType.Leg, BodyPartSymmetry.Right) => "OrganSlimePersonLegRight",
            _ => null
        };
    }

    private string GetSlotId(BodyPartType partType, BodyPartSymmetry symmetry)
    {
        return (partType, symmetry) switch
        {
            (BodyPartType.Arm, BodyPartSymmetry.Left) => "left_arm",
            (BodyPartType.Arm, BodyPartSymmetry.Right) => "right_arm",
            (BodyPartType.Leg, BodyPartSymmetry.Left) => "left_leg",
            (BodyPartType.Leg, BodyPartSymmetry.Right) => "right_leg",
            _ => ""
        };
    }

    private bool IsSlimeSpecies(EntityUid entity)
    {
        return TryComp<HumanoidAppearanceComponent>(entity, out var appearance) &&
               appearance.Species == "SlimePerson";
    }
}
