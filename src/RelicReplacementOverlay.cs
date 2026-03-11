using Godot;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;

namespace AllRelicsBecomeOneRelic;

internal sealed partial class RelicReplacementOverlay : CanvasLayer
{
    private static RelicReplacementOverlay? _instance;

    private readonly List<RelicModel> _allRelics = new();

    private readonly Dictionary<int, string> _idByItemIndex = new();

    private ColorRect _backdrop = null!;

    private PanelContainer _panel = null!;

    private LineEdit _searchInput = null!;

    private ItemList _relicList = null!;

    private TextureRect _previewIcon = null!;

    private Label _previewTitle = null!;

    private Label _previewRarity = null!;

    private RichTextLabel _previewDescription = null!;

    private Label _currentSelectionLabel = null!;

    private Button _preserveRelicProducersCheck = null!;

    private Label _statusLabel = null!;

    private string? _selectedRelicId;

    private bool _isBuilt;

    internal static void Toggle(NGame host)
    {
        EnsureAttached(host);
        if (_instance == null)
        {
            return;
        }

        if (_instance.Visible)
        {
            _instance.HideOverlay();
        }
        else
        {
            _instance.ShowOverlay();
        }
    }

    internal static bool ToggleEscape(NGame host)
    {
        EnsureAttached(host);
        if (_instance == null || !_instance.Visible)
        {
            return false;
        }

        _instance.HideOverlay();
        return true;
    }

    internal static void EnsureAttached(NGame host)
    {
        if (_instance != null && IsInstanceValid(_instance))
        {
            if (_instance.GetParent() == null)
            {
                host.AddChild(_instance);
            }

            _instance.InitializeUi();
            return;
        }

        _instance = new RelicReplacementOverlay
        {
            Name = "AllRelicsBecomeOneRelicOverlay"
        };
        _instance.InitializeUi();
        host.AddChild(_instance);
    }

    public override void _ExitTree()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    internal void InitializeUi()
    {
        if (_isBuilt)
        {
            return;
        }

        _isBuilt = true;
        Layer = 50;
        Visible = false;
        ProcessMode = ProcessModeEnum.Always;
        BuildUi();
        LoadRelics();
        RefreshFromConfig();
    }

    private void BuildUi()
    {
        _backdrop = new ColorRect
        {
            Name = "Backdrop",
            Color = new Color(0f, 0f, 0f, 0.72f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        _backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_backdrop);

        _panel = new PanelContainer
        {
            Name = "Panel",
            CustomMinimumSize = new Vector2(980f, 720f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        _panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _panel.Position = new Vector2(-490f, -360f);

        StyleBoxFlat style = new()
        {
            BgColor = new Color("15202B"),
            BorderColor = new Color("E2C15A"),
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            ContentMarginBottom = 18,
            ContentMarginLeft = 18,
            ContentMarginRight = 18,
            ContentMarginTop = 18
        };
        _panel.AddThemeStyleboxOverride("panel", style);
        AddChild(_panel);

        VBoxContainer root = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _panel.AddChild(root);

        Label title = new()
        {
            Text = "遗物替换设置",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", new Color("F5DE7A"));
        root.AddChild(title);

        Label subtitle = new()
        {
            Text = "按 F8 打开或关闭。保存后立即生效。",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        subtitle.AddThemeColorOverride("font_color", new Color("B8C7D9"));
        root.AddChild(subtitle);

        HSeparator separator = new();
        root.AddChild(separator);

        _searchInput = new LineEdit
        {
            PlaceholderText = "搜索遗物名称",
            ClearButtonEnabled = true
        };
        _searchInput.TextChanged += OnSearchChanged;
        root.AddChild(_searchInput);

        _currentSelectionLabel = new Label
        {
            Text = "当前目标：-",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _currentSelectionLabel.AddThemeColorOverride("font_color", new Color("D9E4F0"));
        root.AddChild(_currentSelectionLabel);

        _preserveRelicProducersCheck = new NoTooltipButton
        {
            Text = $"不替换后续还会产出遗物的遗物（{RelicReplacementService.PreservedRelicProducerCount} 个）",
            TooltipText = "开启后，这批遗物会保持原样，不会被统一替换。"
        };
        _preserveRelicProducersCheck.ToggleMode = true;
        _preserveRelicProducersCheck.TooltipText = "开启后，WONGOS_MYSTERY_TICKET 不会被统一替换。";
        _preserveRelicProducersCheck.CustomMinimumSize = new Vector2(0f, 42f);
        _preserveRelicProducersCheck.TooltipText = "开启后，当前只会保留 WONGOS_MYSTERY_TICKET，不对它做统一替换。";
        _preserveRelicProducersCheck.Text = "保留特殊遗物：关闭";
        _preserveRelicProducersCheck.TooltipText = "开启后，当前只会保留 WONGOS_MYSTERY_TICKET，不对它做统一替换。";
        _preserveRelicProducersCheck.Pressed += UpdateSpecialRelicToggleText;
        _preserveRelicProducersCheck.AddThemeColorOverride("font_color", new Color("D9E4F0"));
        _preserveRelicProducersCheck.TooltipText = "";
        root.AddChild(_preserveRelicProducersCheck);

        HBoxContainer contentRow = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddChild(contentRow);

        _relicList = new ItemList
        {
            SelectMode = ItemList.SelectModeEnum.Single,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(420f, 420f)
        };
        _relicList.FixedIconSize = new Vector2I(48, 48);
        _relicList.ItemSelected += OnRelicSelected;
        contentRow.AddChild(_relicList);

        VBoxContainer previewColumn = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(480f, 420f)
        };
        contentRow.AddChild(previewColumn);

        PanelContainer previewPanel = new();
        StyleBoxFlat previewStyle = new()
        {
            BgColor = new Color("0F1720"),
            BorderColor = new Color("53708E"),
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            ContentMarginBottom = 16,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 16
        };
        previewPanel.AddThemeStyleboxOverride("panel", previewStyle);
        previewColumn.AddChild(previewPanel);

        VBoxContainer previewRoot = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        previewPanel.AddChild(previewRoot);

        _previewIcon = new TextureRect
        {
            CustomMinimumSize = new Vector2(192f, 192f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        previewRoot.AddChild(_previewIcon);

        _previewTitle = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _previewTitle.AddThemeFontSizeOverride("font_size", 24);
        _previewTitle.AddThemeColorOverride("font_color", new Color("F5DE7A"));
        previewRoot.AddChild(_previewTitle);

        _previewRarity = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _previewRarity.AddThemeColorOverride("font_color", new Color("C9D7E5"));
        previewRoot.AddChild(_previewRarity);

        _previewDescription = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            ScrollActive = true,
            SelectionEnabled = true
        };
        previewRoot.AddChild(_previewDescription);

        HBoxContainer buttonRow = new()
        {
            Alignment = BoxContainer.AlignmentMode.End
        };
        root.AddChild(buttonRow);

        Button saveButton = CreateButton("保存");
        saveButton.Pressed += SaveConfig;
        buttonRow.AddChild(saveButton);

        Button closeButton = CreateButton("关闭");
        closeButton.Pressed += HideOverlay;
        buttonRow.AddChild(closeButton);

        _statusLabel = new Label
        {
            Text = "",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _statusLabel.AddThemeColorOverride("font_color", new Color("8FD3A7"));
        root.AddChild(_statusLabel);
    }

    private static Button CreateButton(string text)
    {
        return new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(120f, 42f)
        };
    }

    private void LoadRelics()
    {
        _allRelics.Clear();
        _allRelics.AddRange(ModelDb.AllRelics.OrderBy(static relic => SafeGetTitle(relic)));
    }

    private void RefreshFromConfig()
    {
        _selectedRelicId = ModEntry.Config.TargetRelicId;
        _preserveRelicProducersCheck.ButtonPressed = ModEntry.Config.PreserveRelicProducers;
        UpdateSpecialRelicToggleText();
        _searchInput.Text = "";
        RebuildRelicList();
        UpdateCurrentSelectionLabel();
        _statusLabel.Text = "已读取当前配置。";
    }

    private void UpdatePreserveRelicProducersToggleText()
    {
        _preserveRelicProducersCheck.Text = _preserveRelicProducersCheck.ButtonPressed
            ? "保留 WONGOS_MYSTERY_TICKET：开启"
            : "保留 WONGOS_MYSTERY_TICKET：关闭";
    }

    private void UpdateSpecialRelicToggleText()
    {
        _preserveRelicProducersCheck.Text = _preserveRelicProducersCheck.ButtonPressed
            ? "保留特殊遗物：开启"
            : "保留特殊遗物：关闭";
    }

    private void OnSearchChanged(string _)
    {
        RebuildRelicList();
    }

    private void RebuildRelicList()
    {
        _relicList.Clear();
        _idByItemIndex.Clear();

        string filter = _searchInput.Text.Trim();
        IEnumerable<RelicModel> filtered = _allRelics.Where(relic => MatchesFilter(relic, filter));

        int index = 0;
        int? selectedIndex = null;
        foreach (RelicModel relic in filtered)
        {
            _relicList.AddItem(GetDisplayName(relic), relic.Icon);
            _idByItemIndex[index] = relic.Id.Entry;
            if (string.Equals(relic.Id.Entry, _selectedRelicId, StringComparison.OrdinalIgnoreCase))
            {
                selectedIndex = index;
            }

            index++;
        }

        if (selectedIndex.HasValue)
        {
            _relicList.Select(selectedIndex.Value);
            _relicList.EnsureCurrentIsVisible();
        }

        UpdatePreview();
    }

    private static bool MatchesFilter(RelicModel relic, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return relic.Id.Entry.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || relic.Title.GetFormattedText().Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDisplayName(RelicModel relic)
    {
        return SafeGetTitle(relic);
    }

    private void OnRelicSelected(long index)
    {
        if (_idByItemIndex.TryGetValue((int)index, out string? relicId))
        {
            _selectedRelicId = relicId;
            UpdateCurrentSelectionLabel();
            UpdatePreview();
        }
    }

    private void UpdateCurrentSelectionLabel()
    {
        string relicId = _selectedRelicId ?? string.Empty;
        RelicModel? resolved = RelicReplacementService.DebugResolveConfiguredTarget(relicId);
        string title = resolved?.Title.GetFormattedText() ?? "未选择";
        _currentSelectionLabel.Text = $"当前目标：{title}";
    }

    private void UpdatePreview()
    {
        RelicModel? relic = RelicReplacementService.DebugResolveConfiguredTarget(_selectedRelicId ?? string.Empty);
        if (relic == null)
        {
            _previewIcon.Texture = null;
            _previewTitle.Text = "未选择遗物";
            _previewRarity.Text = "";
            _previewDescription.Text = "";
            return;
        }

        _previewIcon.Texture = relic.BigIcon;
        _previewTitle.Text = relic.Title.GetFormattedText();
        _previewRarity.Text = $"稀有度：{GetRarityText(relic.Rarity)}";
        _previewDescription.Text = relic.DynamicDescription.GetFormattedText();
    }

    private void SaveConfig()
    {
        if (string.IsNullOrWhiteSpace(_selectedRelicId))
        {
            _statusLabel.Text = "请先选择一个遗物。";
            _statusLabel.AddThemeColorOverride("font_color", new Color("F08B8B"));
            return;
        }

        RelicReplacementConfig config = new()
        {
            TargetRelicId = _selectedRelicId,
            ReplaceStarterRelics = true,
            LogEveryReplacement = ModEntry.Config.LogEveryReplacement,
            PreserveRelicProducers = _preserveRelicProducersCheck.ButtonPressed
        };

        ModEntry.UpdateConfig(config);
        RelicModel? selectedRelic = RelicReplacementService.DebugResolveConfiguredTarget(_selectedRelicId);
        _statusLabel.Text = $"已保存，当前目标为“{SafeGetTitle(selectedRelic!)}”。";
        _statusLabel.AddThemeColorOverride("font_color", new Color("8FD3A7"));
        UpdateCurrentSelectionLabel();
        UpdatePreview();
    }

    private void ShowOverlay()
    {
        RefreshFromConfig();
        Visible = true;
        _searchInput.GrabFocus();
        _searchInput.CaretColumn = _searchInput.Text.Length;
    }

    private void HideOverlay()
    {
        Visible = false;
        GetViewport().GuiReleaseFocus();
    }

    private static string SafeGetTitle(RelicModel relic)
    {
        try
        {
            return relic.Title.GetFormattedText();
        }
        catch
        {
            return "未知遗物";
        }
    }

    private static string GetRarityText(RelicRarity rarity)
    {
        return rarity switch
        {
            RelicRarity.Starter => "初始",
            RelicRarity.Common => "普通",
            RelicRarity.Uncommon => "罕见",
            RelicRarity.Rare => "稀有",
            RelicRarity.Shop => "商店",
            RelicRarity.Ancient => "远古",
            RelicRarity.Event => "事件",
            RelicRarity.None => "无",
            _ => rarity.ToString()
        };
    }
}

internal sealed partial class NoTooltipButton : Button
{
    public override string _GetTooltip(Vector2 atPosition)
    {
        return string.Empty;
    }
}
