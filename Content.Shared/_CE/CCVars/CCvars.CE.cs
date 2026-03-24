using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> CESkillTimers =
        CVarDef.Create("game.skill_timers", true, CVar.SERVER | CVar.REPLICATED);
}
