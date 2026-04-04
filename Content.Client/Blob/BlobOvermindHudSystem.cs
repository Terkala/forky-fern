using System;
using System.Text;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared.Blob;
using Content.Shared.GameTicking;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameStates;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using static Robust.Client.UserInterface.Control;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Blob;

/// <summary>
/// Shows bio / evolution / tile stats for the local blob overmind.
/// </summary>
public sealed class BlobOvermindHudSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private PanelContainer? _root;
    private RichTextLabel? _label;
    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        base.Initialize();

        var load = _ui.GetUIController<GameplayStateLoadController>();
        load.OnScreenLoad += OnScreenLoad;
        load.OnScreenUnload += OnScreenUnload;

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<BlobHiveComponent, AfterAutoHandleStateEvent>(OnHiveState);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_root is not { Visible: true } || _player.LocalEntity is not { } ent)
            return;

        if (!HasComp<BlobOvermindComponent>(ent))
            return;

        if (_timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + TimeSpan.FromMilliseconds(150);
        if (TryComp<BlobHiveComponent>(ent, out var hive))
            RefreshText(hive);
    }

    private void OnScreenLoad()
    {
        if (_player.LocalSession?.AttachedEntity is { } ent)
            TryShow(ent);
    }

    private void OnScreenUnload()
    {
        DestroyHud();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        DestroyHud();
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        TryShow(args.Entity);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        DestroyHud();
    }

    private void OnHiveState(EntityUid uid, BlobHiveComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (_player.LocalEntity != uid || _root is not { Visible: true })
            return;
        RefreshText(component);
    }

    private void TryShow(EntityUid ent)
    {
        if (!HasComp<BlobOvermindComponent>(ent) || !TryComp<BlobHiveComponent>(ent, out var hive))
        {
            DestroyHud();
            return;
        }

        EnsureHud();
        _root!.Visible = true;
        RefreshText(hive);
    }

    private void EnsureHud()
    {
        if (_root != null)
            return;

        _root = new PanelContainer
        {
            MouseFilter = Control.MouseFilterMode.Ignore,
            Visible = false,
        };

        var box = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(8),
        };

        _label = new RichTextLabel { MinWidth = 200 };
        box.AddChild(_label);
        _root.AddChild(box);

        LayoutContainer.SetAnchorAndMarginPreset(_root, LayoutContainer.LayoutPreset.TopRight, margin: 12);
        _ui.PopupRoot.AddChild(_root);
    }

    private void RefreshText(BlobHiveComponent hive)
    {
        if (_label == null)
            return;

        var capSuffix = hive.MaxBlobTilesPerNucleus <= 0
            ? Loc.GetString("blob-hud-cap-suffix-none")
            : Loc.GetString("blob-hud-cap-suffix",
                ("max", Math.Max(1, hive.LivingNuclei) * hive.MaxBlobTilesPerNucleus));

        var deploy = hive.Deployed
            ? string.Empty
            : Loc.GetString("blob-hud-need-deploy");

        var sb = new StringBuilder();
        sb.Append(Loc.GetString("blob-hud-title"));
        sb.Append('\n');
        sb.Append(Loc.GetString("blob-hud-line-bio-evo",
            ("bio", hive.BioPoints),
            ("biomax", hive.BioMax),
            ("evo", hive.EvoPoints)));
        sb.Append('\n');
        sb.Append(Loc.GetString("blob-hud-line-tiles",
            ("tiles", hive.TileCount),
            ("capsuffix", capSuffix)));
        sb.Append('\n');
        sb.Append(Loc.GetString("blob-hud-line-evo-progress",
            ("next", hive.NextEvoAtTiles),
            ("nuclei", hive.LivingNuclei)));
        if (deploy.Length > 0)
        {
            sb.Append('\n');
            sb.Append(deploy);
        }

        _label.SetMessage(sb.ToString());
    }

    private void DestroyHud()
    {
        _root?.Orphan();
        _root = null;
        _label = null;
    }
}
