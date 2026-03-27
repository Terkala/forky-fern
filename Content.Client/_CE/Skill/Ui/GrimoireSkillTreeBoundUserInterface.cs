using Content.Shared._CE.Skill;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.Client._CE.Skill.Ui;

[UsedImplicitly]
public sealed class GrimoireSkillTreeBoundUserInterface : BoundUserInterface
{
    public GrimoireSkillTreeBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        var uiMgr = IoCManager.Resolve<IUserInterfaceManager>();
        uiMgr.GetUIController<CESkillUIController>().OpenSkillTreeFromGrimoire();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            IoCManager.Resolve<IUserInterfaceManager>()
                .GetUIController<CESkillUIController>()
                .CloseSkillTreeFromGrimoire();
        }

        base.Dispose(disposing);
    }
}
