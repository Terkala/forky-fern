using Content.Shared._CE.Skill.Prototypes;
using Content.Shared._Funkystation.MageAscension;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Funkystation.MageAscension;

[UsedImplicitly]
public sealed class GrimoireSchoolPickerBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private GrimoireSchoolPickerWindow? _window;

    [Dependency] private readonly IPrototypeManager _proto = default!;

    public GrimoireSchoolPickerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<GrimoireSchoolPickerWindow>();
        _window.Title = Loc.GetString("funky-grimoire-window-title");
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not GrimoireSchoolPickerBuiState bui)
            return;

        _window.SchoolButtons.RemoveAllChildren();

        if (bui.PendingSpellPickTier > 0 && bui.SpellPickOptionIds.Length > 0)
        {
            _window.StatusLabel.Text = Loc.GetString("funky-grimoire-pick-spell",
                ("tier", bui.PendingSpellPickTier));

            foreach (var skillId in bui.SpellPickOptionIds)
            {
                if (!_proto.TryIndex(skillId, out CESkillPrototype? skill))
                    continue;

                var label = skill.Name != null
                    ? Loc.GetString(skill.Name)
                    : skillId;
                var btn = new Button { Text = label, HorizontalExpand = true };
                var id = skillId;
                btn.OnPressed += _ => SendMessage(new GrimoireSpellPickSelectMessage(id));
                _window.SchoolButtons.AddChild(btn);
            }

            return;
        }

        if (bui.Committed && bui.ChosenSchoolId != null &&
            _proto.TryIndex(bui.ChosenSchoolId, out var chosen))
        {
            _window.StatusLabel.Text = Loc.GetString("funky-grimoire-committed",
                ("school", Loc.GetString(chosen.Name)));
        }
        else
        {
            _window.StatusLabel.Text = Loc.GetString("funky-grimoire-pick-school");
        }

        foreach (var school in _proto.EnumeratePrototypes<MageSchoolPrototype>())
        {
            var label = Loc.GetString(school.Name);
            var btn = new Button { Text = label, HorizontalExpand = true };
            var id = school.ID;
            btn.OnPressed += _ => SendMessage(new GrimoireSchoolPickerSelectMessage(id));
            btn.Disabled = bui.Committed;
            _window.SchoolButtons.AddChild(btn);
        }
    }
}
