using System;
using System.Text;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.addons.mega_text;
using EasyProfileTransfer.Data;

namespace EasyProfileTransfer.UI;

/// <summary>
/// Main-menu utility for copying vanilla profile saves into the modded save tree.
/// </summary>
public sealed partial class EasyProfileTransferOverlay : CanvasLayer
{
    private const int DefaultCanvasLayer = 158;
    private const int MenuButtonMarginTop = 108;
    private const int ModalMarginLeft = 40;
    private const int ModalMarginTop = 150;
    private const int ProfileTitleFontSize = 16;
    private const int MenuButtonRowSeparation = 4;
    private static readonly Vector2 FallbackMenuButtonSize = new(180, 32);
    private static readonly Vector2 FallbackReticleSize = new(20, 26);

    private static EasyProfileTransferOverlay? _instance;

    private readonly Control _menuButtonDock;
    private readonly MarginContainer _menuButtonWrap;
    private readonly HBoxContainer _menuButtonRow;
    private readonly NMainMenuTextButton _toggleMenuBtn;
    private readonly MegaLabel _toggleMenuLabel;
    private readonly TextureRect _reticleLeft;
    private readonly TextureRect _reticleRight;
    private Tween? _reticleTween;

    private readonly Control _modalLayer;
    private readonly ColorRect _dimmer;
    private readonly PanelContainer _modal;
    private readonly VBoxContainer _tableBody;
    private readonly Label _statusLabel;
    private readonly Button _transferBtn;
    private readonly Button _closeBtn;

    private readonly Control _confirmLayer;
    private readonly PanelContainer _confirmPanel;
    private readonly Button _confirmYesBtn;
    private readonly Button _confirmNoBtn;

    private ProfileTransferComparison _comparison = new();
    private bool _lastMenuContext;
    private bool _uiReady;
    private bool _transferInProgress;
    private Theme? _lastMenuThemeAppliedToButton;

    private static readonly Color HeaderGold = new(0.95f, 0.85f, 0.48f, 1f);
    private static readonly Color MutedText = new(0.72f, 0.76f, 0.84f, 1f);
    private static readonly Color SummaryText = new(0.90f, 0.92f, 0.96f, 1f);
    private static readonly Color SuccessText = new(0.62f, 0.92f, 0.68f, 1f);
    private static readonly Color ErrorText = new(0.95f, 0.55f, 0.50f, 1f);
    private static readonly Color SourceAccent = new(0.72f, 0.74f, 0.78f, 1f);
    private static readonly Color TargetAccent = new(0.95f, 0.42f, 0.38f, 1f);
    private static readonly Color SourcePanelBg = new(0.085f, 0.085f, 0.090f, 1f);
    private static readonly Color TargetPanelBg = new(0.120f, 0.055f, 0.050f, 1f);
    private static readonly Color SkippedAccent = new(0.48f, 0.50f, 0.54f, 1f);
    private static readonly Color SkippedPanelBg = new(0.065f, 0.065f, 0.070f, 1f);
    private static readonly Color SkippedPanelModulate = new(0.78f, 0.78f, 0.82f, 1f);

    public static void Initialize(SceneTree tree)
    {
        try
        {
            if (_instance != null && GodotObject.IsInstanceValid(_instance))
            {
                return;
            }

            _instance = new EasyProfileTransferOverlay();
            _instance.ProcessMode = ProcessModeEnum.Always;
            _instance.SetProcess(false);
            tree.Root.CallDeferred(Node.MethodName.AddChild, _instance);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasyProfileTransfer] Initialize: {ex.Message}");
        }
    }

    private EasyProfileTransferOverlay()
    {
        Layer = DefaultCanvasLayer;

        _menuButtonDock = new Control
        {
            Name = "EasyProfileTransferMenuButtonDock",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = true,
        };
        _menuButtonDock.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        _menuButtonWrap = new MarginContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        _menuButtonWrap.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _menuButtonWrap.GrowHorizontal = Control.GrowDirection.End;
        _menuButtonWrap.GrowVertical = Control.GrowDirection.End;
        _menuButtonWrap.AddThemeConstantOverride("margin_top", MenuButtonMarginTop);

        _menuButtonRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Begin,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
        };
        _menuButtonRow.AddThemeConstantOverride("separation", MenuButtonRowSeparation);

        _reticleLeft = CreateReticleTextureRect(FallbackReticleSize);
        _reticleRight = CreateReticleTextureRect(FallbackReticleSize);
        Theme menuTheme = EasyProfileTransferThemeHelper.EnsureNonNullTheme(tree: null);
        (_toggleMenuBtn, _toggleMenuLabel) = CreateMainMenuTextButton(
            menuTheme,
            "Transfer Profile",
            ToggleModal,
            FallbackMenuButtonSize,
            ProfileTitleFontSize);
        _toggleMenuBtn.Focused += _ => OnMenuButtonFocused();
        _toggleMenuBtn.Unfocused += _ => OnMenuButtonUnfocused();

        _menuButtonRow.AddChild(_reticleLeft);
        _menuButtonRow.AddChild(_toggleMenuBtn);
        _menuButtonRow.AddChild(_reticleRight);
        _menuButtonWrap.AddChild(_menuButtonRow);
        _menuButtonDock.AddChild(_menuButtonWrap);

        _modalLayer = new Control
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _modalLayer.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        _dimmer = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.62f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _dimmer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _dimmer.GuiInput += OnDimmerGuiInput;

        _modal = new PanelContainer();
        _modal.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _modal.Position = new Vector2(ModalMarginLeft, ModalMarginTop);
        _modal.CustomMinimumSize = new Vector2(640, 0);
        _modal.AddThemeStyleboxOverride("panel", CreateOpaquePanelStyle(new Color(0.075f, 0.065f, 0.055f, 1f)));

        var modalRoot = new MarginContainer();
        modalRoot.AddThemeConstantOverride("margin_left", 20);
        modalRoot.AddThemeConstantOverride("margin_right", 20);
        modalRoot.AddThemeConstantOverride("margin_top", 18);
        modalRoot.AddThemeConstantOverride("margin_bottom", 18);
        _modal.AddChild(modalRoot);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 12);
        modalRoot.AddChild(content);

        var title = new Label
        {
            Text = "Transfer Vanilla Profiles to Modded Saves",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", HeaderGold);
        content.AddChild(title);

        _tableBody = new VBoxContainer();
        _tableBody.AddThemeConstantOverride("separation", 8);
        content.AddChild(_tableBody);

        _statusLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", 14);
        _statusLabel.AddThemeColorOverride("font_color", SummaryText);
        content.AddChild(_statusLabel);

        var actionRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        actionRow.AddThemeConstantOverride("separation", 16);
        content.AddChild(actionRow);

        _transferBtn = new Button
        {
            Text = "Transfer",
            CustomMinimumSize = new Vector2(160, 42),
        };
        _transferBtn.AddThemeFontSizeOverride("font_size", ProfileTitleFontSize);
        _transferBtn.Pressed += OnTransferPressed;
        actionRow.AddChild(_transferBtn);

        _closeBtn = new Button
        {
            Text = "Close",
            CustomMinimumSize = new Vector2(120, 42),
        };
        _closeBtn.AddThemeFontSizeOverride("font_size", ProfileTitleFontSize);
        _closeBtn.Pressed += CloseModal;
        actionRow.AddChild(_closeBtn);

        _confirmLayer = new Control
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };

        var confirmDimmer = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.45f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        confirmDimmer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        confirmDimmer.SetOffsetsPreset(Control.LayoutPreset.FullRect);

        var confirmCenter = new CenterContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        confirmCenter.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        confirmCenter.SetOffsetsPreset(Control.LayoutPreset.FullRect);

        _confirmPanel = new PanelContainer();
        _confirmPanel.CustomMinimumSize = new Vector2(520, 220);
        _confirmPanel.AddThemeStyleboxOverride("panel", CreateOpaquePanelStyle(new Color(0.075f, 0.065f, 0.055f, 1f)));
        confirmCenter.AddChild(_confirmPanel);

        var confirmRoot = new MarginContainer();
        confirmRoot.AddThemeConstantOverride("margin_left", 20);
        confirmRoot.AddThemeConstantOverride("margin_right", 20);
        confirmRoot.AddThemeConstantOverride("margin_top", 18);
        confirmRoot.AddThemeConstantOverride("margin_bottom", 18);
        _confirmPanel.AddChild(confirmRoot);

        var confirmVBox = new VBoxContainer();
        confirmVBox.AddThemeConstantOverride("separation", 14);
        confirmRoot.AddChild(confirmVBox);

        var confirmTitle = new Label
        {
            Text = "Confirm Transfer",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        confirmTitle.AddThemeFontSizeOverride("font_size", 22);
        confirmTitle.AddThemeColorOverride("font_color", HeaderGold);
        confirmVBox.AddChild(confirmTitle);

        var confirmBody = new Label
        {
            Text = "This will overwrite existing modded profiles with your vanilla savegames. Are you sure?",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        confirmBody.AddThemeFontSizeOverride("font_size", 16);
        confirmBody.AddThemeColorOverride("font_color", SummaryText);
        confirmVBox.AddChild(confirmBody);

        var confirmButtons = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        confirmButtons.AddThemeConstantOverride("separation", 16);
        confirmVBox.AddChild(confirmButtons);

        _confirmYesBtn = new Button
        {
            Text = "Yes",
            CustomMinimumSize = new Vector2(120, 40),
        };
        _confirmYesBtn.Pressed += OnConfirmYesPressed;
        confirmButtons.AddChild(_confirmYesBtn);

        _confirmNoBtn = new Button
        {
            Text = "No",
            CustomMinimumSize = new Vector2(120, 40),
        };
        _confirmNoBtn.Pressed += CloseConfirmation;
        confirmButtons.AddChild(_confirmNoBtn);

        _confirmLayer.AddChild(confirmDimmer);
        _confirmLayer.AddChild(confirmCenter);

        _modalLayer.AddChild(_dimmer);
        _modalLayer.AddChild(_modal);
        _modalLayer.AddChild(_confirmLayer);

        AddChild(_menuButtonDock);
        AddChild(_modalLayer);
    }

    public override void _EnterTree()
    {
        base._EnterTree();
        Theme theme = EasyProfileTransferThemeHelper.EnsureNonNullTheme(GetTree());
        _modal.Theme = theme;
        _modalLayer.Theme = theme;
        _confirmPanel.Theme = theme;
        _toggleMenuBtn.Theme = theme;
        _lastMenuThemeAppliedToButton = theme;

        Viewport? vp = GetViewport();
        if (vp != null)
        {
            vp.SizeChanged += OnMainViewportSizeChanged;
        }

        Callable.From(TryBindMainMenuReticleTextures).CallDeferred();
    }

    public override void _Ready()
    {
        _uiReady = true;
        SetProcess(true);
    }

    public override void _ExitTree()
    {
        Viewport? vp = GetViewport();
        if (vp != null)
        {
            vp.SizeChanged -= OnMainViewportSizeChanged;
        }

        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        _ = delta;
        try
        {
            bool modalOpen = _modalLayer.Visible;
            bool showButton = IsMainMenuContext(out NMainMenu? activeMainMenu)
                              && !IsMainMenuTransitionActive()
                              && !modalOpen;
            if (showButton != _lastMenuContext)
            {
                _lastMenuContext = showButton;
                _menuButtonWrap.Visible = showButton;
                _toggleMenuBtn.Visible = showButton;
            }

            if (showButton)
            {
                ApplyMainMenuButtonStyleIfNeeded();
                if (activeMainMenu != null)
                {
                    AlignMenuButtonToProfileSelector(activeMainMenu);
                    _menuButtonWrap.Modulate = new Color(1f, 1f, 1f, activeMainMenu.Modulate.A);
                }
            }
            else
            {
                _menuButtonWrap.Modulate = Colors.White;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasyProfileTransfer] _Process: {ex.Message}");
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.Escape)
        {
            if (_confirmLayer.Visible)
            {
                CloseConfirmation();
                GetViewport()?.SetInputAsHandled();
                return;
            }

            if (_modalLayer.Visible)
            {
                CloseModal();
                GetViewport()?.SetInputAsHandled();
            }
        }
    }

    private void OnDimmerGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left && !_confirmLayer.Visible)
        {
            CloseModal();
        }
    }

    private void ToggleModal()
    {
        if (_modalLayer.Visible)
        {
            CloseModal();
            return;
        }

        OpenModal();
    }

    private void OpenModal()
    {
        RefreshComparisonTable();
        ResizeModalToContent();
        _statusLabel.Text = "";
        _confirmLayer.Visible = false;
        _modalLayer.Visible = true;
        _modalLayer.Modulate = new Color(1f, 1f, 1f, 0f);
        Callable.From(ResizeModalToContent).CallDeferred();
        Callable.From(AlignConfirmLayerToModal).CallDeferred();
        Tween tween = CreateTween();
        tween.TweenProperty(_modalLayer, "modulate", Colors.White, 0.18)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Sine);
        SetActionButtonsEnabled(true);
    }

    private void CloseModal()
    {
        if (!_modalLayer.Visible)
        {
            return;
        }

        CloseConfirmation();
        Tween tween = CreateTween();
        tween.TweenProperty(_modalLayer, "modulate", new Color(1f, 1f, 1f, 0f), 0.16)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Sine);
        tween.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(_modalLayer))
            {
                _modalLayer.Visible = false;
                _modalLayer.Modulate = Colors.White;
                ResetMenuButtonLayout();
            }
        };
    }

    private void OnTransferPressed()
    {
        if (_transferInProgress)
        {
            return;
        }

        AlignConfirmLayerToModal();
        _confirmLayer.Visible = true;
    }

    private void CloseConfirmation()
    {
        _confirmLayer.Visible = false;
    }

    private async void OnConfirmYesPressed()
    {
        if (_transferInProgress)
        {
            return;
        }

        _transferInProgress = true;
        CloseConfirmation();
        SetActionButtonsEnabled(false);
        _statusLabel.Text = "Transferring saves...";
        _statusLabel.AddThemeColorOverride("font_color", SummaryText);

        ProfileTransferResult result = ProfileTransferService.TransferAll();
        if (!GodotObject.IsInstanceValid(this))
        {
            return;
        }

        if (result.Success)
        {
            string removalSummary = result.FilesRemoved > 0 ? $", removed {result.FilesRemoved} stale file(s)" : string.Empty;
            _statusLabel.Text = $"Transfer complete. Copied {result.FilesCopied} file(s){removalSummary}.";
            _statusLabel.AddThemeColorOverride("font_color", SuccessText);
            await ProfileTransferService.TryOverwriteCloudWithLocalAsync();
            if (!GodotObject.IsInstanceValid(this))
            {
                return;
            }

            ReloadMainMenuAfterTransfer();
        }
        else
        {
            var builder = new StringBuilder();
            builder.Append($"Transfer finished with {result.FilesFailed} failure(s). Copied {result.FilesCopied} file(s).");
            if (result.FilesRemoved > 0)
            {
                builder.Append($" Removed {result.FilesRemoved} stale file(s).");
            }
            if (result.Errors.Count > 0)
            {
                builder.Append(' ');
                builder.Append(string.Join(" | ", result.Errors));
            }

            _statusLabel.Text = builder.ToString();
            _statusLabel.AddThemeColorOverride("font_color", ErrorText);
        }

        if (!GodotObject.IsInstanceValid(this))
        {
            return;
        }

        RefreshComparisonTable();

        _transferInProgress = false;
        SetActionButtonsEnabled(true);
    }

    private static void ReloadMainMenuAfterTransfer()
    {
        if (!ProfileTransferService.TryReloadActiveProfileSaveData(
                out ReadSaveResult<PrefsSave> prefsReadResult,
                out ReadSaveResult<SerializableProgress> progressReadResult))
        {
            return;
        }

        NGame? game = NGame.Instance;
        if (game?.MainMenu == null)
        {
            return;
        }

        game.ReloadMainMenu();
        game.CheckShowSaveFileError(progressReadResult, prefsReadResult, settingsReadResult: null);
    }

    private void SetActionButtonsEnabled(bool enabled)
    {
        _transferBtn.Disabled = !enabled;
        _closeBtn.Disabled = !enabled;
        _confirmYesBtn.Disabled = !enabled;
        _confirmNoBtn.Disabled = !enabled;
    }

    private void RefreshComparisonTable()
    {
        if (!_uiReady)
        {
            return;
        }

        _comparison = ProfileTransferSnapshotProvider.LoadComparison();
        foreach (Node child in _tableBody.GetChildren())
        {
            child.QueueFree();
        }

        var headerRow = new HBoxContainer();
        headerRow.AddThemeConstantOverride("separation", 12);
        _tableBody.AddChild(headerRow);
        headerRow.AddChild(CreateColumnHeader("Vanilla", SourceAccent, 0.43f));
        headerRow.AddChild(CreateDirectionLabel("->"));
        headerRow.AddChild(CreateColumnHeader("Modded (overwritten)", TargetAccent, 0.43f));

        for (int i = 0; i < ProfileTransferPaths.MaxProfileCount; i++)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);
            _tableBody.AddChild(row);
            bool vanillaSlotEmpty = _comparison.Vanilla[i].IsEmptyProfileSlot;
            row.AddChild(CreateProfileCard(_comparison.Vanilla[i], SourceAccent, SourcePanelBg, 0.43f));
            row.AddChild(CreateDirectionSpacer(vanillaSlotEmpty));
            row.AddChild(CreateProfileCard(
                _comparison.Modded[i],
                vanillaSlotEmpty ? SkippedAccent : TargetAccent,
                vanillaSlotEmpty ? SkippedPanelBg : TargetPanelBg,
                0.43f,
                transferSkipped: vanillaSlotEmpty));
        }
    }

    private void ResizeModalToContent()
    {
        if (!GodotObject.IsInstanceValid(_modal))
        {
            return;
        }

        Vector2 minimumSize = _modal.GetCombinedMinimumSize();
        if (minimumSize.X <= 0f || minimumSize.Y <= 0f)
        {
            return;
        }

        _modal.Size = minimumSize;
        AlignConfirmLayerToModal();
    }

    private static Control CreateColumnHeader(string text, Color color, float stretchRatio)
    {
        var label = new Label
        {
            Text = text,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = stretchRatio,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", 18);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Control CreateDirectionLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            CustomMinimumSize = new Vector2(74, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        label.AddThemeFontSizeOverride("font_size", 18);
        label.AddThemeColorOverride("font_color", HeaderGold);
        return label;
    }

    private static Control CreateDirectionSpacer(bool transferSkipped = false)
    {
        var spacer = new Control
        {
            CustomMinimumSize = new Vector2(74, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (transferSkipped)
        {
            spacer.SelfModulate = SkippedPanelModulate;
        }

        return spacer;
    }

    private static Control CreateProfileCard(
        ProfileTransferRow row,
        Color accentColor,
        Color panelColor,
        float stretchRatio,
        bool transferSkipped = false)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = stretchRatio,
            CustomMinimumSize = new Vector2(0, 96),
        };
        panel.AddThemeStyleboxOverride("panel", CreateOpaquePanelStyle(panelColor, accentColor));
        if (transferSkipped)
        {
            panel.SelfModulate = SkippedPanelModulate;
        }

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        panel.AddChild(margin);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 4);
        margin.AddChild(body);

        var title = new Label
        {
            Text = $"Profile {row.ProfileId}",
        };
        title.AddThemeFontSizeOverride("font_size", ProfileTitleFontSize);
        title.AddThemeColorOverride("font_color", accentColor);
        body.AddChild(title);

        if (row.IsEmptyProfileSlot)
        {
            string missingText = transferSkipped ? "No save data found (not overwritten)" : "No save data found";
            var missing = new Label { Text = missingText };
            missing.AddThemeFontSizeOverride("font_size", 14);
            missing.AddThemeColorOverride("font_color", MutedText);
            body.AddChild(missing);
            return panel;
        }

        Color statColor = transferSkipped ? MutedText : SummaryText;
        body.AddChild(CreateStatLabel($"Score: {FormatScore(row.CurrentScore)}", statColor));
        body.AddChild(CreateStatLabel($"Best ascension: {FormatAscension(row.BestAscension)}", statColor));
        body.AddChild(CreateStatLabel($"Run history: {row.RunHistoryCount}", statColor));
        body.AddChild(CreateStatLabel(FormatRunInProgress(row), statColor));
        if (transferSkipped)
        {
            var skipped = new Label { Text = "Will not be overwritten" };
            skipped.AddThemeFontSizeOverride("font_size", 14);
            skipped.AddThemeColorOverride("font_color", MutedText);
            body.AddChild(skipped);
        }

        return panel;
    }

    private static Label CreateStatLabel(string text, Color? fontColor = null)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", fontColor ?? SummaryText);
        return label;
    }

    private static string FormatScore(int score) => score >= 0 ? score.ToString() : "—";
    private static string FormatAscension(int ascension) => ascension >= 0 ? ascension.ToString() : "—";

    private static string FormatRunInProgress(ProfileTransferRow row)
    {
        if (row.HasSinglePlayerRun && row.HasMultiplayerRun)
        {
            return "In-progress run: SP + MP";
        }

        if (row.HasSinglePlayerRun)
        {
            return "In-progress run: Singleplayer";
        }

        if (row.HasMultiplayerRun)
        {
            return "In-progress run: Multiplayer";
        }

        return "In-progress run: None";
    }

    private void OnMainViewportSizeChanged()
    {
        PositionTopLeftPanel(_modal, ModalMarginLeft, ModalMarginTop);
        AlignConfirmLayerToModal();
        ResetMenuButtonLayout();
    }

    private void AlignConfirmLayerToModal()
    {
        if (!GodotObject.IsInstanceValid(_confirmLayer) || !GodotObject.IsInstanceValid(_modal))
        {
            return;
        }

        Vector2 modalSize = _modal.Size;
        if (modalSize.X <= 0f || modalSize.Y <= 0f)
        {
            modalSize = _modal.GetCombinedMinimumSize();
        }

        if (modalSize.X <= 0f || modalSize.Y <= 0f)
        {
            return;
        }

        _confirmLayer.Position = _modal.Position;
        _confirmLayer.Size = modalSize;
    }

    private static void PositionTopLeftPanel(Control panel, int left, int top)
    {
        panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        panel.Position = new Vector2(left, top);
    }

    private static StyleBoxFlat CreateOpaquePanelStyle(Color bg)
    {
        return CreateOpaquePanelStyle(bg, new Color(0.55f, 0.46f, 0.27f, 1f));
    }

    private static StyleBoxFlat CreateOpaquePanelStyle(Color bg, Color borderColor)
    {
        var style = new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = borderColor,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
        };
        style.SetContentMarginAll(0);
        return style;
    }

    private void AlignMenuButtonToProfileSelector(NMainMenu mainMenu)
    {
        if (!_menuButtonDock.IsInsideTree())
        {
            return;
        }

        Control? profileBtn = mainMenu.GetNodeOrNull<Control>("%OpenProfileScreenButton")
                              ?? mainMenu.FindChild("OpenProfileScreenButton", true, false) as Control;
        if (profileBtn == null || !profileBtn.IsInsideTree())
        {
            return;
        }

        float profileLeft = profileBtn.GetGlobalRect().Position.X;
        float dockLeft = _menuButtonDock.GetGlobalRect().Position.X;
        float rowLeadWidth = _reticleLeft.CustomMinimumSize.X + MenuButtonRowSeparation;
        int marginLeft = Math.Max(0, (int)MathF.Round(profileLeft - dockLeft - rowLeadWidth));
        _menuButtonWrap.AddThemeConstantOverride("margin_left", marginLeft);
        ApplyMenuButtonSizeFromProfile(profileBtn);
    }

    private void ApplyMenuButtonSizeFromProfile(Control profileBtn)
    {
        Control? profileTitle = profileBtn.GetNodeOrNull<Control>("Title");
        float buttonHeight = FallbackMenuButtonSize.Y;
        float buttonWidth = FallbackMenuButtonSize.X;

        if (profileTitle != null)
        {
            Vector2 titleSize = profileTitle.Size;
            if (titleSize.Y > 0f)
            {
                buttonHeight = titleSize.Y + 6f;
            }

            if (titleSize.X > 0f)
            {
                buttonWidth = Math.Max(titleSize.X + 28f, FallbackMenuButtonSize.X);
            }

            int titleFontSize = profileTitle.GetThemeFontSize("font_size");
            if (titleFontSize > 0)
            {
                _toggleMenuLabel.AddThemeFontSizeOverride("font_size", titleFontSize);
            }
        }

        float reticleHeight = Math.Clamp(buttonHeight + 4f, 22f, 34f);
        float reticleWidth = Math.Clamp(reticleHeight * 0.72f, 16f, 24f);
        var reticleSize = new Vector2(reticleWidth, reticleHeight);
        _reticleLeft.CustomMinimumSize = reticleSize;
        _reticleRight.CustomMinimumSize = reticleSize;
        _toggleMenuBtn.CustomMinimumSize = new Vector2(buttonWidth, buttonHeight);
    }

    private void ResetMenuButtonLayout()
    {
        _reticleTween?.Kill();
        _reticleTween = null;
        _reticleLeft.Modulate = StsColors.transparentWhite;
        _reticleRight.Modulate = StsColors.transparentWhite;
        _menuButtonWrap.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _menuButtonWrap.Position = Vector2.Zero;
        _menuButtonRow.Position = Vector2.Zero;
        _toggleMenuBtn.Position = Vector2.Zero;
        _toggleMenuBtn.Scale = Vector2.One;
    }

    private void ApplyMainMenuButtonStyleIfNeeded()
    {
        Theme? menuTheme = EasyProfileTransferThemeHelper.ResolveMenuTheme(GetTree());
        if (menuTheme == null || ReferenceEquals(menuTheme, _lastMenuThemeAppliedToButton))
        {
            return;
        }

        _lastMenuThemeAppliedToButton = menuTheme;
        _toggleMenuBtn.Theme = menuTheme;
    }

    private static bool IsMainMenuTransitionActive()
    {
        NTransition? transition = NGame.Instance?.Transition;
        return transition != null && transition.InTransition;
    }

    private static bool IsMainMenuContext(out NMainMenu? activeMainMenu)
    {
        activeMainMenu = null;
        try
        {
            if (NMapScreen.Instance?.IsOpen == true)
            {
                return false;
            }

            var currentScreen = ActiveScreenContext.Instance?.GetCurrentScreen();
            if (currentScreen is NMainMenu mm && mm.IsVisibleInTree())
            {
                activeMainMenu = mm;
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static (NMainMenuTextButton Button, MegaLabel Label) CreateMainMenuTextButton(
        Theme theme,
        string text,
        Action onReleased,
        Vector2 minSize,
        int fontSize)
    {
        var btn = new NMainMenuTextButton
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = minSize,
            Theme = theme,
        };

        var label = new MegaLabel
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        label.OffsetRight = 0;
        label.OffsetBottom = 0;
        EasyProfileTransferThemeHelper.ApplyLabelThemeItemsFromTheme(label, theme);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeConstantOverride("outline_size", 11);
        label.AddThemeColorOverride("font_outline_color", new Color(0.12f, 0.1f, 0.09f, 1f));
        label.AddThemeColorOverride("font_shadow_color", new Color(0.02f, 0.02f, 0.02f, 1f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        label.SelfModulate = StsColors.cream;
        btn.AddChild(label);
        btn.Released += _ => onReleased();
        Callable.From(() =>
        {
            if (btn.label != null)
            {
                btn.label.PivotOffset = btn.label.Size * 0.5f;
            }
        }).CallDeferred();

        return (btn, label);
    }

    private static TextureRect CreateReticleTextureRect(Vector2 minSize)
    {
        return new TextureRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = StsColors.transparentWhite,
            CustomMinimumSize = minSize,
            ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
            StretchMode = TextureRect.StretchModeEnum.Keep,
        };
    }

    private void OnMenuButtonFocused()
    {
        _reticleTween?.Kill();
        _reticleTween = CreateTween().SetParallel();
        Color subtleGold = new Color(StsColors.gold.R, StsColors.gold.G, StsColors.gold.B, 0.75f);
        _reticleTween.TweenProperty(_reticleLeft, "modulate", subtleGold, 0.14).From(StsColors.transparentWhite);
        _reticleTween.TweenProperty(_reticleRight, "modulate", subtleGold, 0.14).From(StsColors.transparentWhite);
    }

    private void OnMenuButtonUnfocused()
    {
        _reticleTween?.Kill();
        _reticleTween = CreateTween().SetParallel();
        _reticleTween.TweenProperty(_reticleLeft, "modulate", StsColors.transparentWhite, 0.22);
        _reticleTween.TweenProperty(_reticleRight, "modulate", StsColors.transparentWhite, 0.22);
    }

    private void TryBindMainMenuReticleTextures()
    {
        Node? root = GetTree()?.Root;
        Node? mm = root?.FindChild("NMainMenu", true, false) ?? root?.FindChild("MainMenu", true, false);
        if (mm == null)
        {
            return;
        }

        TextureRect? left = mm.GetNodeOrNull<TextureRect>("%ReticleLeft");
        TextureRect? right = mm.GetNodeOrNull<TextureRect>("%ReticleRight");
        if (left?.Texture != null)
        {
            _reticleLeft.Texture = left.Texture;
        }

        if (right?.Texture != null)
        {
            _reticleRight.Texture = right.Texture;
        }
    }
}
