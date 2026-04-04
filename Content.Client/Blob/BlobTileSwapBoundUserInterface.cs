using Content.Client.UserInterface.Controls;
using Content.Shared.Blob;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.Blob;

[UsedImplicitly]
public sealed class BlobTileSwapBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;

    private SimpleRadialMenu? _menu;

    public BlobTileSwapBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<BlobTileComponent>(Owner, out var tile) || tile.Kind != BlobTileKind.Normal)
            return;

        var session = _playerManager.LocalSession;
        if (session?.AttachedEntity is not { } performer ||
            !EntMan.TryGetComponent<BlobHiveComponent>(performer, out var clientHive))
        {
            return;
        }

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.SetButtons(BuildButtons(clientHive));
        _menu.OpenOverMouseScreenPosition();
    }

    private IEnumerable<RadialMenuOptionBase> BuildButtons(BlobHiveComponent hive)
    {
        var buttons = new List<RadialMenuActionOptionBase>();

        foreach (var kind in BlobSpecialistRecipes.MenuOrder)
        {
            if (BlobSpecialistRecipes.IsMenuOptionLocked(hive, kind))
                continue;

            if (!BlobSpecialistRecipes.TryGetRecipe(kind, out var recipe))
                continue;

            if (!_prototypeManager.TryIndex(recipe.EntityPrototype, out var entProto))
                continue;

            var tooltip = Loc.GetString("blob-specialist-radial-option",
                ("name", Loc.GetString(entProto.Name)),
                ("cost", recipe.BioCost));

            var option = new RadialMenuActionOption<BlobTileKind>(SendChoice, kind)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(recipe.EntityPrototype),
                ToolTip = tooltip
            };
            buttons.Add(option);
        }

        return buttons;
    }

    private void SendChoice(BlobTileKind kind)
    {
        SendMessage(new BlobTileSwapChoiceMessage(kind));
    }
}
