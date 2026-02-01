using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Containers;
using Content.Shared.Examine;
using Content.Shared.Medical.Cybernetics.Modules;
using Content.Shared.Overlays;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Shared.Medical.Cybernetics;

/// <summary>
/// System that handles examination of cyber-limbs when the examiner has diagnostic goggles equipped.
/// </summary>
public abstract class CyberLimbExamineSystem : EntitySystem
{
    [Dependency] protected readonly ExamineSystemShared ExamineSystem = default!;
    [Dependency] protected readonly SharedBodyPartSystem BodyPartSystem = default!;
    [Dependency] protected readonly SharedContainerSystem ContainerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    private void OnGetExamineVerbs(Entity<BodyComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        // Check if examiner has ShowHealthBarsComponent with Silicon container
        if (!TryComp<ShowHealthBarsComponent>(args.User, out var healthBars))
            return;

        if (!healthBars.DamageContainers.Contains("Silicon"))
            return;

        // Check if in details range
        var detailsRange = ExamineSystem.IsInDetailsRange(args.User, ent);

        // Check if target has any cyber-limbs
        bool hasCyberLimbs = false;
        foreach (var (partId, _) in BodyPartSystem.GetBodyChildren(ent, ent.Comp))
        {
            if (HasComp<CyberLimbComponent>(partId))
            {
                hasCyberLimbs = true;
                break;
            }
        }

        if (!hasCyberLimbs)
            return;

        var verb = new ExamineVerb
        {
            Act = () =>
            {
                FormatAndSendExamineMessage(args.User, ent);
            },
            Text = Loc.GetString("cyber-limb-examine-verb-text"),
            Category = VerbCategory.Examine,
            Disabled = !detailsRange,
            Message = detailsRange ? null : Loc.GetString("cyber-limb-examine-verb-disabled"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/examine.svg.192dpi.png"))
        };

        args.Verbs.Add(verb);
    }

    private void FormatAndSendExamineMessage(EntityUid examiner, Entity<BodyComponent> target)
    {
        var message = new FormattedMessage();

        // Get aggregate stats from CyberLimbStatsComponent
        if (!TryComp<CyberLimbStatsComponent>(target, out var stats))
        {
            message.AddMarkupPermissive(Loc.GetString("cyber-limb-examine-no-cybernetics", ("target", MetaData(target).EntityName)));
            ExamineSystem.SendExamineTooltip(examiner, target, message, false, false);
            return;
        }

        // Header
        message.AddMarkupPermissive(Loc.GetString("cyber-limb-examine-header"));
        message.PushNewline();

        // Battery status
        if (stats.BatteryCapacity > 0)
        {
            var batteryPercent = stats.CurrentBatteryCharge / stats.BatteryCapacity * 100f;
            var batteryTimeRemaining = FormatBatteryTimeRemaining(stats.CurrentBatteryCharge, stats.BatteryCapacity);
            message.AddMarkupPermissive(Loc.GetString("cyber-limb-battery-status",
                ("percent", $"{batteryPercent:F0}"),
                ("timeRemaining", batteryTimeRemaining)));
        }
        else
        {
            message.AddMarkupPermissive(Loc.GetString("cyber-limb-battery-depleted"));
        }
        message.PushNewline();

        // Service time
        var serviceTimeRemaining = FormatServiceTimeRemaining(stats.ServiceTimeRemaining);
        message.AddMarkupPermissive(Loc.GetString("cyber-limb-service-time",
            ("timeRemaining", serviceTimeRemaining)));
        message.PushNewline();

        // Efficiency with color coding
        var efficiencyColor = stats.Efficiency < 50f ? "red" : stats.Efficiency < 100f ? "yellow" : "green";
        message.AddMarkupPermissive(Loc.GetString("cyber-limb-efficiency",
            ("efficiency", $"[color={efficiencyColor}]{stats.Efficiency:F0}[/color]")));
        message.PushNewline();

        // List cyber-limbs with panel states and modules
        var cyberLimbs = new List<(EntityUid Id, CyberLimbComponent Component)>();
        foreach (var (partId, _) in BodyPartSystem.GetBodyChildren(target, target.Comp))
        {
            if (TryComp<CyberLimbComponent>(partId, out var cyberLimb))
            {
                cyberLimbs.Add((partId, cyberLimb));
            }
        }

        if (cyberLimbs.Count > 0)
        {
            message.PushNewline();
            foreach (var (limbId, cyberLimb) in cyberLimbs)
            {
                var limbName = MetaData(limbId).EntityName;
                message.AddMarkupPermissive(Loc.GetString("cyber-limb-examine-limb", ("limb", limbName)));
                message.PushNewline();

                // Panel state
                if (cyberLimb.PanelOpen)
                {
                    message.AddMarkupPermissive(Loc.GetString("cyber-limb-examine-panel-open"));
                }
                else if (cyberLimb.PanelExposed)
                {
                    message.AddMarkupPermissive(Loc.GetString("cyber-limb-panel-exposed"));
                }
                else
                {
                    message.AddMarkupPermissive(Loc.GetString("cyber-limb-examine-panel-closed"));
                }
                message.PushNewline();

                // Modules
                var modules = GetCyberLimbModules(limbId);
                if (modules.Count > 0)
                {
                    message.AddMarkupPermissive(Loc.GetString("cyber-limb-modules-installed"));
                    foreach (var module in modules)
                    {
                        var moduleName = GetModuleDisplayName(module);
                        message.AddMarkupPermissive(Loc.GetString("cyber-limb-examine-module", ("module", moduleName)));
                        message.PushNewline();
                    }
                }
                else
                {
                    message.AddMarkupPermissive(Loc.GetString("cyber-limb-examine-no-modules"));
                    message.PushNewline();
                }
            }
        }

        ExamineSystem.SendExamineTooltip(examiner, target, message, false, false);
    }

    /// <summary>
    /// Formats battery time remaining as "Xh Ym" or "Depleted".
    /// </summary>
    private string FormatBatteryTimeRemaining(float currentCharge, float capacity)
    {
        if (currentCharge <= 0)
            return Loc.GetString("cyber-limb-battery-depleted");

        // Battery drain rate = capacity / 1200.0 per second
        // Time remaining = currentCharge / (capacity / 1200.0) = currentCharge * 1200.0 / capacity
        var secondsRemaining = currentCharge * 1200.0 / capacity;
        var timeSpan = TimeSpan.FromSeconds(secondsRemaining);

        return FormatTimeSpan(timeSpan);
    }

    /// <summary>
    /// Formats service time remaining as "Xh Ym" or "Expired".
    /// </summary>
    private string FormatServiceTimeRemaining(TimeSpan serviceTime)
    {
        if (serviceTime <= TimeSpan.Zero)
            return Loc.GetString("cyber-limb-service-expired");

        var hours = (int)serviceTime.TotalHours;
        var minutes = serviceTime.Minutes;

        return Loc.GetString("cyber-limb-examine-service-time",
            ("hours", hours),
            ("minutes", minutes));
    }

    /// <summary>
    /// Formats a TimeSpan as "Xh Ym" using localization for battery time.
    /// </summary>
    private string FormatTimeSpan(TimeSpan timeSpan)
    {
        var hours = (int)timeSpan.TotalHours;
        var minutes = timeSpan.Minutes;

        return Loc.GetString("cyber-limb-examine-battery-time",
            ("hours", hours),
            ("minutes", minutes));
    }

    /// <summary>
    /// Gets all module entities from a cyber-limb's storage container.
    /// </summary>
    private List<EntityUid> GetCyberLimbModules(EntityUid cyberLimb)
    {
        var modules = new List<EntityUid>();

        if (!TryComp<StorageComponent>(cyberLimb, out var storage))
            return modules;

        if (storage.Container == null)
            return modules;

        foreach (var entity in storage.Container.ContainedEntities)
        {
            modules.Add(entity);
        }

        return modules;
    }

    /// <summary>
    /// Gets the display name for a module, using localization if available.
    /// </summary>
    private string GetModuleDisplayName(EntityUid module)
    {
        var entityName = MetaData(module).EntityName;

        // Try to match module type and get localized name
        if (HasComp<BatteryModuleComponent>(module))
        {
            return Loc.GetString("cyber-module-battery");
        }
        else if (HasComp<MatterBinModuleComponent>(module))
        {
            return Loc.GetString("cyber-module-matter-bin");
        }
        else if (HasComp<ManipulatorModuleComponent>(module))
        {
            return Loc.GetString("cyber-module-manipulator");
        }
        else if (HasComp<CapacitorModuleComponent>(module))
        {
            return Loc.GetString("cyber-module-capacitor");
        }
        else if (TryComp<SpecialModuleComponent>(module, out var special))
        {
            switch (special.ModuleType)
            {
                case SpecialModuleType.Tool:
                    // Try to get tool-specific name
                    if (special.ToolQuality != null)
                    {
                        var toolQualityId = special.ToolQuality.Value;
                        if (toolQualityId == "Prying")
                            return Loc.GetString("cyber-module-jaws-of-life");
                    }
                    break;
                case SpecialModuleType.BioBattery:
                    return Loc.GetString("cyber-module-bio-battery");
            }
        }

        // Fall back to entity name if no localization match
        return entityName;
    }
}
