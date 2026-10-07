using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace HarmonicaPlayer;

// UI 美化：集中管理配色、字体、圆角与控件模板。
// 只改变外观（颜色、圆角、间距、卡片），不改变解析、演奏、MIDI 或文件逻辑。
// 所有颜色都来自 UiPalette（浅色/深色两套），界面代码里不再出现颜色字面量。
public static class UiTheme
{
    private static readonly List<WeakReference<Window>> windows = new();
    private static ThemeMode mode = ThemeMode.System;

    public static ThemeMode Mode => mode;

    // 跟随系统时读注册表当前值与 SystemEvents 的实时通知，都不需要重启。
    public static UiPalette Current =>
        mode == ThemeMode.Light || (mode == ThemeMode.System && !SystemTheme.IsDark) ? UiPalettes.Light : UiPalettes.Dark;

    // 切换主题：立刻重刷所有已登记窗口；记住选择由调用方写进设置。
    public static void SetMode(ThemeMode value)
    {
        SystemTheme.StartWatching(RefreshAll);
        if (mode == value) return;
        mode = value;
        RefreshAll();
    }

    // 重刷所有存活窗口。SystemEvents 的回调在别的线程上，必须按窗口切回界面线程。
    public static void RefreshAll()
    {
        for (int i = windows.Count - 1; i >= 0; i--)
        {
            if (!windows[i].TryGetTarget(out var window)) { windows.RemoveAt(i); continue; }
            if (window.Dispatcher.HasShutdownStarted || window.Dispatcher.HasShutdownFinished) { windows.RemoveAt(i); continue; }
            if (!window.Dispatcher.CheckAccess()) { window.Dispatcher.BeginInvoke(new Action(() => Refresh(window))); continue; }
            Refresh(window);
        }
    }

    private static void Refresh(Window window)
    {
        ApplyTo(window);
        // 自绘样式是逐个控件赋上去的，仅仅换资源不会更新它们，必须让窗口重新套一遍。
        if (window is IThemeAware aware) aware.OnThemeChanged();
    }

    // ---- 语义 token：沿用原有名字，调用点（UiTheme.Brush(UiTheme.Accent)）无需改动 ----
    public static bool IsDark => Current.IsDark;
    public static string Page => Current.Page;
    public static string Panel => Current.Panel;
    public static string PanelAlt => Current.PanelAlt;
    public static string ConsoleBackground => Current.PanelAlt;
    public static string ControlHover => Current.Hover;
    public static string ControlPressed => Current.Pressed;
    public static string CardBorder => Current.Border;
    public static string ControlBorder => Current.ControlBorder;
    public static string PrimaryText => Current.TextPrimary;
    public static string SecondaryText => Current.TextSecondary;
    public static string MutedText => Current.TextMuted;
    public static string DisabledText => Current.TextDisabled;
    public static string DisabledFill => Current.PanelAlt;
    public static string HeaderHint => Current.TextSecondary;
    public static string Accent => Current.Accent;
    public static string AccentHover => Current.AccentHover;
    public static string AccentPressed => Current.AccentPressed;
    public static string AccentSoft => Current.AccentSoft;
    public static string AccentSoftHover => Current.AccentSoftHover;
    public static string AccentSoftPressed => Current.AccentSoftPressed;
    public static string OnAccent => Current.OnAccent;
    public static string Danger => Current.Danger;
    public static string DangerHover => Current.DangerHover;
    public static string DangerPressed => Current.DangerPressed;
    public static string DangerSoft => Current.DangerSoft;
    public static string DangerTint => Current.DangerTint;
    public static string DangerBorder => Current.DangerBorder;
    public static string Warning => Current.Warning;
    public static string WarningSoft => Current.WarningSoft;
    public static string Success => Current.Success;
    public static string Focus => Current.Focus;
    public static string TrackFill => Current.TrackFill;
    public static string TrackBorder => Current.TrackBorder;
    public static string Selection => Current.Selection;
    public static double ShadowOpacity => Current.ShadowOpacity;

    public static readonly FontFamily UiFont = new("Microsoft YaHei UI, Segoe UI, Arial");
    public static readonly FontFamily MonoFont = new("Cascadia Mono, Consolas, Microsoft YaHei UI");

    public static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }

    // 给窗口套用统一主题：背景、字体、控件样式资源。所有窗口都在这里登记，主题切换时统一重刷。
    public static void Apply(Window window)
    {
        if (!windows.Any(w => w.TryGetTarget(out var existing) && ReferenceEquals(existing, window)))
        {
            windows.Add(new WeakReference<Window>(window));
            // 静态列表不能长期持有已关闭窗口；引用本身是弱引用，这里只是及时清理。
            window.Closed += (_, _) => Forget(window);
        }
        ApplyTo(window);
    }

    private static void Forget(Window window)
    {
        for (int i = windows.Count - 1; i >= 0; i--)
            if (!windows[i].TryGetTarget(out var target) || ReferenceEquals(target, window)) windows.RemoveAt(i);
    }

    private static void ApplyTo(Window window)
    {
        window.Background = Brush(Page);
        window.Foreground = Brush(PrimaryText);
        window.FontFamily = UiFont;
        window.FontSize = 13;
        window.UseLayoutRounding = true;
        window.SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(window, TextFormattingMode.Display);

        var resources = window.Resources;
        // 默认模板（下拉弹层、滚动条、右键菜单、提示框）用的是系统画刷；在窗口资源里覆盖这些键，
        // 它们就会一起跟随深浅主题，不必重写整套模板。
        resources[SystemColors.WindowBrushKey] = Brush(Panel);
        resources[SystemColors.WindowTextBrushKey] = Brush(PrimaryText);
        resources[SystemColors.ControlBrushKey] = Brush(PanelAlt);
        resources[SystemColors.ControlTextBrushKey] = Brush(PrimaryText);
        resources[SystemColors.HighlightBrushKey] = Brush(Accent);
        resources[SystemColors.HighlightTextBrushKey] = Brush(OnAccent);
        resources[SystemColors.InactiveSelectionHighlightBrushKey] = Brush(AccentSoft);
        resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = Brush(Accent);
        resources[SystemColors.GrayTextBrushKey] = Brush(DisabledText);
        resources[SystemColors.MenuBrushKey] = Brush(Panel);
        resources[SystemColors.MenuTextBrushKey] = Brush(PrimaryText);
        resources[typeof(TextBlock)] = TextBlockStyle();
        resources["Card"] = CardStyle();
        resources["NoteText"] = NoteTextStyle();
        resources["HeaderBar"] = BarStyle(new Thickness(0, 0, 0, 1));
        resources["FooterBar"] = BarStyle(new Thickness(0, 1, 0, 0));
        resources["Alert"] = AlertStyle();
        resources["BtnPrimary"] = ButtonStyle(Accent, AccentHover, AccentPressed, OnAccent, Accent, 13, FontWeights.SemiBold, new Thickness(16, 8, 16, 8));
        resources["BtnSecondary"] = ButtonStyle(AccentSoft, AccentSoftHover, AccentSoftPressed, Accent, Accent, 13, FontWeights.SemiBold, new Thickness(16, 8, 16, 8));
        resources["BtnGhost"] = ButtonStyle(Panel, ControlHover, ControlPressed, SecondaryText, ControlBorder, 13, FontWeights.Normal, new Thickness(14, 8, 14, 8));
        resources["BtnDanger"] = ButtonStyle(DangerSoft, DangerTint, DangerTint, Danger, DangerBorder, 12, FontWeights.Normal, new Thickness(10, 6, 10, 6));
        resources["BtnSmall"] = ButtonStyle(Panel, ControlHover, ControlPressed, SecondaryText, ControlBorder, 12, FontWeights.Normal, new Thickness(10, 6, 10, 6));
        resources["Input"] = InputStyle(Panel, PrimaryText, 13, UiFont, false);
        resources["Editor"] = InputStyle(Panel, PrimaryText, 13, UiFont, true);
        resources["Console"] = InputStyle(ConsoleBackground, SecondaryText, 12, MonoFont, true);
        resources["Combo"] = ComboStyle();
        var comboItem = ComboItemStyle();
        resources["ComboItem"] = comboItem;
        resources["Check"] = CheckStyle();
        resources["Slider"] = SliderStyle();
        resources["Progress"] = ProgressStyle();
        resources["LibraryItem"] = LibraryItemStyle();
        // 浮窗外壳：自绘标题栏、标题栏图标按钮与选项卡样式（同一套圆角/边框/间距）。
        resources["TitleBar"] = TitleBarStyle();
        resources["IconButton"] = IconButtonStyle();
        var tabItem = TabItemStyle();
        resources["Tab"] = TabControlStyle();
        resources["TabItem"] = tabItem;
        // 兜底隐式样式：对话框以及后续新增的控件（曲谱库卡片按钮、快捷键录入框……）默认也走同一套外观，
        // 已有的显式样式（Input/Editor/Console/Btn*）优先级更高，不受影响。
        resources[typeof(Button)] = ButtonStyle(Panel, ControlHover, ControlPressed, SecondaryText, ControlBorder, 13, FontWeights.Normal, new Thickness(14, 8, 14, 8));
        resources[typeof(TextBox)] = InputStyle(Panel, PrimaryText, 13, UiFont, false);
        resources[typeof(CheckBox)] = CheckStyle();
        resources[typeof(ComboBox)] = ComboStyle();
        resources[typeof(ComboBoxItem)] = comboItem;
        resources[typeof(TabItem)] = tabItem;
    }

    public static Style Style(Window window, string key) => (Style)window.Resources[key];

    // ---------- 样式工厂 ----------

    private static Style TextBlockStyle()
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush(SecondaryText)));
        style.Setters.Add(new Setter(TextBlock.FontSizeProperty, 13d));
        return style;
    }

    // 说明/提示条（TextBlock 专用）：用于警告与导入提示文字，风格比警示框更轻。
    private static Style NoteTextStyle()
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.BackgroundProperty, Brush(WarningSoft)));
        style.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush(Warning)));
        style.Setters.Add(new Setter(TextBlock.FontSizeProperty, 12d));
        style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 8, 10, 8)));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 6, 0, 2)));
        return style;
    }

    // 卡片：一律用 1px 边框划分层级，不用投影（大阴影在深色下会发灰发脏）。
    private static Style CardStyle()
    {
        var style = new Style(typeof(Border));
        style.Setters.Add(new Setter(Border.BackgroundProperty, Brush(Panel)));
        style.Setters.Add(new Setter(Border.BorderBrushProperty, Brush(CardBorder)));
        style.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(10)));
        style.Setters.Add(new Setter(Border.PaddingProperty, new Thickness(12)));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 12)));
        return style;
    }

    private static Style BarStyle(Thickness border)
    {
        var style = new Style(typeof(Border));
        style.Setters.Add(new Setter(Border.BackgroundProperty, Brush(Panel)));
        style.Setters.Add(new Setter(Border.BorderBrushProperty, Brush(CardBorder)));
        style.Setters.Add(new Setter(Border.BorderThicknessProperty, border));
        style.Setters.Add(new Setter(Border.PaddingProperty, new Thickness(0)));
        return style;
    }

    private static Style AlertStyle()
    {
        var style = new Style(typeof(Border));
        style.Setters.Add(new Setter(Border.BackgroundProperty, Brush(DangerSoft)));
        style.Setters.Add(new Setter(Border.BorderBrushProperty, Brush(DangerBorder)));
        style.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(10)));
        style.Setters.Add(new Setter(Border.PaddingProperty, new Thickness(12, 10, 12, 8)));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 8, 0, 6)));
        return style;
    }

    // 曲谱库列表项：白底卡片、圆角，选中用主题浅蓝而不是系统默认灰蓝。
    private static Style LibraryItemStyle() => new(typeof(ListBoxItem))
    {
        Setters =
        {
            new Setter(Control.PaddingProperty, new Thickness(10, 6, 10, 6)),
            new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 4)),
            new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch),
            new Setter(Control.CursorProperty, Cursors.Hand),
            new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Parse(
                $$"""
                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ListBoxItem">
                  <Border x:Name="Bd" Background="{{Panel}}" BorderBrush="{{CardBorder}}" BorderThickness="1" CornerRadius="8"
                          Padding="{TemplateBinding Padding}" SnapsToDevicePixels="True">
                    <ContentPresenter HorizontalAlignment="Stretch" VerticalAlignment="Center" />
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                      <Setter TargetName="Bd" Property="Background" Value="{{ControlHover}}" />
                    </Trigger>
                    <Trigger Property="IsSelected" Value="True">
                      <Setter TargetName="Bd" Property="Background" Value="{{AccentSoft}}" />
                      <Setter TargetName="Bd" Property="BorderBrush" Value="{{Accent}}" />
                    </Trigger>
                    <Trigger Property="IsEnabled" Value="False">
                      <Setter TargetName="Bd" Property="Background" Value="{{PanelAlt}}" />
                    </Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
                """))
        }
    };

    // 浮窗标题栏：比 HeaderBar 更紧凑，面板底色 + 底部 1px 分隔线。
    private static Style TitleBarStyle()
    {
        var style = new Style(typeof(Border));
        style.Setters.Add(new Setter(Border.BackgroundProperty, Brush(Panel)));
        style.Setters.Add(new Setter(Border.BorderBrushProperty, Brush(CardBorder)));
        style.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
        return style;
    }

    // 标题栏图标按钮（最小化/关闭）：30×30 无边框方形，悬停浅底，键盘焦点有环。
    private static Style IconButtonStyle()
    {
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brush(SecondaryText)));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 13d));
        style.Setters.Add(new Setter(FrameworkElement.WidthProperty, 30d));
        style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 30d));
        style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 0d));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(4, 0, 0, 0)));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(Control.TemplateProperty, IconButtonTemplate()));
        style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
        style.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
        return style;
    }

    // 选项卡容器：扁平、无边框、无内边距，只保留选项卡头。
    private static Style TabControlStyle()
    {
        var style = new Style(typeof(TabControl));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        return style;
    }

    // 选项卡头：未选中灰字，选中为浅主色底 + 主色文字 + 底部主色短下划线。
    private static Style TabItemStyle()
    {
        var style = new Style(typeof(TabItem));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brush(MutedText)));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 13d));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 7, 12, 7)));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0)));
        style.Setters.Add(new Setter(Control.TemplateProperty, TabItemTemplate()));
        style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
        style.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
        return style;
    }

    private static ControlTemplate IconButtonTemplate() => (ControlTemplate)XamlReader.Parse(
        $$"""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button">
          <Grid>
            <Border x:Name="Bd" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="7" SnapsToDevicePixels="True">
              <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="{TemplateBinding VerticalContentAlignment}" RecognizesAccessKey="True" />
            </Border>
            <Border x:Name="Ring" BorderBrush="{{Focus}}" BorderThickness="2" CornerRadius="9" Margin="-3"
                    Visibility="Collapsed" IsHitTestVisible="False" SnapsToDevicePixels="True" />
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Bd" Property="Background" Value="{{ControlHover}}" />
            </Trigger>
            <Trigger Property="IsPressed" Value="True">
              <Setter TargetName="Bd" Property="Background" Value="{{ControlPressed}}" />
            </Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True">
              <Setter TargetName="Ring" Property="Visibility" Value="Visible" />
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Foreground" Value="{{DisabledText}}" />
              <Setter Property="Cursor" Value="Arrow" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
        """);

    private static ControlTemplate TabItemTemplate() => (ControlTemplate)XamlReader.Parse(
        $$"""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="TabItem">
          <Grid>
            <Border x:Name="Bd" Background="{TemplateBinding Background}" CornerRadius="8" Padding="{TemplateBinding Padding}" SnapsToDevicePixels="True">
              <ContentPresenter ContentSource="Header" HorizontalAlignment="Center" VerticalAlignment="Center" RecognizesAccessKey="True" />
            </Border>
            <Border x:Name="Line" Height="2" Width="20" CornerRadius="1" Margin="0,0,0,1"
                    HorizontalAlignment="Center" VerticalAlignment="Bottom" Background="Transparent" IsHitTestVisible="False" />
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Bd" Property="Background" Value="{{ControlHover}}" />
            </Trigger>
            <Trigger Property="IsSelected" Value="True">
              <Setter TargetName="Bd" Property="Background" Value="{{AccentSoft}}" />
              <Setter TargetName="Line" Property="Background" Value="{{Accent}}" />
              <Setter Property="Foreground" Value="{{Accent}}" />
              <Setter Property="FontWeight" Value="SemiBold" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
        """);

    private static Style ButtonStyle(string background, string hover, string pressed, string foreground,
        string border, double fontSize, FontWeight weight, Thickness padding)
    {
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brush(background)));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brush(foreground)));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brush(border)));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Control.PaddingProperty, padding));
        style.Setters.Add(new Setter(Control.FontSizeProperty, fontSize));
        style.Setters.Add(new Setter(Control.FontWeightProperty, weight));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(Control.TemplateProperty, ButtonTemplate(hover, pressed)));
        // 焦点环画在模板内部（见 ButtonTemplate 的 Ring），这里关掉系统虚线框，避免两种焦点样式打架。
        style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 6)));
        style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 30d));
        style.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
        return style;
    }

    private static Style InputStyle(string background, string foreground, double fontSize, FontFamily font, bool multiline)
    {
        var style = new Style(typeof(TextBox));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brush(background)));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brush(foreground)));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brush(ControlBorder)));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 6, 10, 6)));
        style.Setters.Add(new Setter(Control.FontSizeProperty, fontSize));
        style.Setters.Add(new Setter(Control.FontFamilyProperty, font));
        style.Setters.Add(new Setter(TextBoxBase.CaretBrushProperty, Brush(Accent)));
        style.Setters.Add(new Setter(TextBox.SelectionBrushProperty, Brush(Selection)));
        style.Setters.Add(new Setter(Control.TemplateProperty, TextBoxTemplate()));
        style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, multiline ? 0d : 30d));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, multiline ? VerticalAlignment.Top : VerticalAlignment.Center));
        return style;
    }

    // 勾选框：自绘 16px 方框 + 勾线，深浅色一致；文字用 ContentPresenter 保留原有 Content。
    private static Style CheckStyle()
    {
        var style = new Style(typeof(CheckBox));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brush(PrimaryText)));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 13d));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
        style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
        style.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Parse(
            $$"""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="CheckBox">
              <StackPanel Orientation="Horizontal" Background="Transparent">
                <Border x:Name="Box" Width="16" Height="16" CornerRadius="4" VerticalAlignment="Center"
                        Background="{{Panel}}" BorderBrush="{{ControlBorder}}" BorderThickness="1" SnapsToDevicePixels="True">
                  <Path x:Name="Check" Data="M 2,6 L 5.5,9.5 L 11,3.5" Stroke="{{OnAccent}}" StrokeThickness="1.8"
                        StrokeStartLineCap="Round" StrokeEndLineCap="Round" Visibility="Collapsed" />
                </Border>
                <ContentPresenter Margin="8,0,0,0" VerticalAlignment="Center" RecognizesAccessKey="True" />
              </StackPanel>
              <ControlTemplate.Triggers>
                <Trigger Property="IsChecked" Value="True">
                  <Setter TargetName="Box" Property="Background" Value="{{Accent}}" />
                  <Setter TargetName="Box" Property="BorderBrush" Value="{{Accent}}" />
                  <Setter TargetName="Check" Property="Visibility" Value="Visible" />
                </Trigger>
                <Trigger Property="IsMouseOver" Value="True">
                  <Setter TargetName="Box" Property="BorderBrush" Value="{{Accent}}" />
                </Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True">
                  <Setter TargetName="Box" Property="BorderBrush" Value="{{Focus}}" />
                </Trigger>
                <Trigger Property="IsEnabled" Value="False">
                  <Setter Property="Foreground" Value="{{DisabledText}}" />
                  <Setter TargetName="Box" Property="Background" Value="{{DisabledFill}}" />
                </Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
            """)));
        return style;
    }

    // 滑杆：只做水平方向（本程序仅有一处试听音量）。轨道用主题色，滑块=主色圆环。
    private static Style SliderStyle()
    {
        var style = new Style(typeof(Slider));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brush(Accent)));
        style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
        style.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Parse(
            $$"""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Slider">
              <Grid VerticalAlignment="Center" Height="20" Background="Transparent">
                <Border x:Name="TrackBar" Height="4" CornerRadius="2" VerticalAlignment="Center"
                        Background="{{TrackFill}}" BorderBrush="{{TrackBorder}}" BorderThickness="1" SnapsToDevicePixels="True" />
                <Track x:Name="PART_Track" VerticalAlignment="Center">
                  <Track.DecreaseRepeatButton>
                    <RepeatButton Command="Slider.DecreaseLarge" Focusable="False" Opacity="0" />
                  </Track.DecreaseRepeatButton>
                  <Track.IncreaseRepeatButton>
                    <RepeatButton Command="Slider.IncreaseLarge" Focusable="False" Opacity="0" />
                  </Track.IncreaseRepeatButton>
                  <Track.Thumb>
                    <Thumb Width="14" Height="14" Focusable="False">
                      <Thumb.Template>
                        <ControlTemplate TargetType="Thumb">
                          <Grid>
                            <Ellipse Fill="{{Accent}}" />
                            <Ellipse Margin="3" Fill="{{Panel}}" />
                          </Grid>
                        </ControlTemplate>
                      </Thumb.Template>
                    </Thumb>
                  </Track.Thumb>
                </Track>
              </Grid>
              <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                  <Setter TargetName="TrackBar" Property="BorderBrush" Value="{{Accent}}" />
                </Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True">
                  <Setter TargetName="TrackBar" Property="BorderBrush" Value="{{Focus}}" />
                </Trigger>
                <Trigger Property="IsEnabled" Value="False">
                  <Setter TargetName="TrackBar" Property="Background" Value="{{DisabledFill}}" />
                </Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
            """)));
        return style;
    }

    // 下拉框：默认模板会读窗口里的系统画刷（见 ApplyTo 的 SystemColors 覆盖），这里只统一尺寸与边框。
    private static Style ComboStyle()
    {
        var style = new Style(typeof(ComboBox));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brush(PrimaryText)));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brush(Panel)));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brush(ControlBorder)));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 13d));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 5, 8, 5)));
        style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 30d));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 6)));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        return style;
    }

    // 下拉项：白底改主题面板色，选中用主色浅底 + 主色文字。
    private static Style ComboItemStyle() => new(typeof(ComboBoxItem))
    {
        Setters =
        {
            new Setter(Control.PaddingProperty, new Thickness(10, 6, 10, 6)),
            new Setter(Control.ForegroundProperty, Brush(PrimaryText)),
            new Setter(Control.CursorProperty, Cursors.Hand),
            new Setter(FrameworkElement.FocusVisualStyleProperty, null),
            new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Parse(
                $$"""
                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ComboBoxItem">
                  <Border x:Name="Bd" Background="{{Panel}}" Padding="{TemplateBinding Padding}" SnapsToDevicePixels="True">
                    <ContentPresenter />
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                      <Setter TargetName="Bd" Property="Background" Value="{{ControlHover}}" />
                    </Trigger>
                    <Trigger Property="IsHighlighted" Value="True">
                      <Setter TargetName="Bd" Property="Background" Value="{{AccentSoft}}" />
                    </Trigger>
                    <Trigger Property="IsSelected" Value="True">
                      <Setter TargetName="Bd" Property="Background" Value="{{AccentSoft}}" />
                      <Setter Property="Foreground" Value="{{Accent}}" />
                    </Trigger>
                    <Trigger Property="IsEnabled" Value="False">
                      <Setter Property="Foreground" Value="{{DisabledText}}" />
                    </Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
                """))
        }
    };

    private static Style ProgressStyle()
    {
        var style = new Style(typeof(ProgressBar));
        style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 6d));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 4, 0, 4)));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brush(Accent)));
        style.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Parse(
            $$"""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ProgressBar">
              <Border x:Name="PART_Track" Background="{{TrackFill}}" BorderBrush="{{TrackBorder}}" BorderThickness="1" CornerRadius="3" SnapsToDevicePixels="True">
                <Border x:Name="PART_Indicator" HorizontalAlignment="Left" CornerRadius="2" Background="{{Accent}}" />
              </Border>
            </ControlTemplate>
            """)));
        return style;
    }

    private static ControlTemplate ButtonTemplate(string hover, string pressed) => (ControlTemplate)XamlReader.Parse(
        $$"""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button">
          <Grid>
            <Border x:Name="Bd" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="8" SnapsToDevicePixels="True">
              <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="{TemplateBinding VerticalContentAlignment}" RecognizesAccessKey="True" />
            </Border>
            <Border x:Name="Ring" BorderBrush="{{Focus}}" BorderThickness="2" CornerRadius="10" Margin="-3"
                    Visibility="Collapsed" IsHitTestVisible="False" SnapsToDevicePixels="True" />
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Bd" Property="Background" Value="{{hover}}" />
            </Trigger>
            <Trigger Property="IsPressed" Value="True">
              <Setter TargetName="Bd" Property="Background" Value="{{pressed}}" />
            </Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True">
              <Setter TargetName="Ring" Property="Visibility" Value="Visible" />
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter TargetName="Bd" Property="Background" Value="{{DisabledFill}}" />
              <Setter TargetName="Bd" Property="BorderBrush" Value="{{CardBorder}}" />
              <Setter Property="Foreground" Value="{{DisabledText}}" />
              <Setter Property="Cursor" Value="Arrow" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
        """);

    private static ControlTemplate TextBoxTemplate() => (ControlTemplate)XamlReader.Parse(
        $$"""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="TextBox">
          <Border x:Name="Bd" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="8" SnapsToDevicePixels="True">
            <ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}" Focusable="False"
                          HorizontalScrollBarVisibility="{TemplateBinding HorizontalScrollBarVisibility}"
                          VerticalScrollBarVisibility="{TemplateBinding VerticalScrollBarVisibility}" />
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsKeyboardFocusWithin" Value="True">
              <Setter TargetName="Bd" Property="BorderBrush" Value="{{Accent}}" />
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter TargetName="Bd" Property="Background" Value="{{PanelAlt}}" />
              <Setter Property="Foreground" Value="{{DisabledText}}" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
        """);
}

public sealed partial class PlayerWindow : IThemeAware
{
    private Style Ui(string key) => (Style)Resources[key];

    private bool noteHookInstalled;

    private void BuildThemeResources() => UiTheme.Apply(this);

    // 主题变化时重刷自绘样式与状态色；控件实例、字段名和事件都不变，业务状态不受影响。
    public void OnThemeChanged()
    {
        ApplyControlStyles();
        UpdateMode();
    }

    // 统一给已存在的控件套上主题样式；控件实例、名称与事件都不改变。
    private void ApplyControlStyles()
    {
        start.Style = Ui("BtnPrimary");
        stop.Style = Ui("BtnSecondary");
        import.Style = Ui("BtnGhost");
        refresh.Style = Ui("BtnGhost");
        newScore.Style = Ui("BtnGhost");
        saveScore.Style = Ui("BtnPrimary");
        saveAs.Style = Ui("BtnGhost");
        importMidi.Style = Ui("BtnGhost");
        listen.Style = Ui("BtnSecondary");
        listenFromCursor.Style = Ui("BtnGhost");
        exportMidi.Style = Ui("BtnGhost");
        configure.Style = Ui("BtnSmall");
        retry.Style = Ui("BtnSmall");
        locateError.Style = Ui("BtnDanger");
        songTitle.Style = Ui("Input");
        score.Style = Ui("Editor");
        preview.Style = Ui("Console");
        log.Style = Ui("Console");
        bpm.Style = Ui("Input");
        gap.Style = Ui("Input");
        volumeSlider.Style = Ui("Slider");
        dry.Style = Ui("Check");
        audioProgress.Style = Ui("Progress");
        playbackProgress.Style = Ui("Progress");
        alertBox.Style = Ui("Alert");
        documentWarning.Style = Ui("NoteText");
        // 字号阶梯统一为 12 / 13 / 14 / 16 / 20，去掉 12.5、13.5、17、19、23 这些零散值。
        alert.FontSize = 13;
        alert.FontWeight = FontWeights.SemiBold;
        alert.Foreground = UiTheme.Brush(UiTheme.Danger);
        status.FontSize = 16;
        status.FontWeight = FontWeights.SemiBold;
        status.Foreground = UiTheme.Brush(UiTheme.PrimaryText);
        modeStatus.FontSize = 12;
        modeStatus.Foreground = UiTheme.Brush(UiTheme.HeaderHint);
        startReason.FontSize = 12;
        startReason.Foreground = UiTheme.Brush(UiTheme.Danger);
        documentInfo.FontSize = 12;
        documentWarning.FontSize = 12;
        documentWarning.Foreground = UiTheme.Brush(UiTheme.Warning);
        hotkeyStatus.FontSize = 12;
        settingsStatus.FontSize = 12;
        volumeLabel.VerticalAlignment = VerticalAlignment.Center;
        volumeLabel.FontSize = 12;
        audioPosition.FontSize = 12;
        playbackPosition.FontSize = 12;
        // 说明条为空时不再占据版面；文本变化时自动显示/隐藏。
        // 主题切换会重跑本方法，因此监听只能挂一次，否则会不断叠加。
        if (!noteHookInstalled)
        {
            noteHookInstalled = true;
            System.ComponentModel.DependencyPropertyDescriptor
                .FromProperty(TextBlock.TextProperty, typeof(TextBlock))
                .AddValueChanged(documentWarning, (_, _) => UpdateNoteVisibility());
        }
        UpdateNoteVisibility();
        // 内边距由样式统一给出，这里只固定数值框宽度与各区块高度。
        bpm.Width = gap.Width = 78;
        // 编辑区、解析预览与日志都在各自选项卡/列里占据剩余高度（清掉字段初始化的固定高度）。
        score.ClearValue(FrameworkElement.HeightProperty);
        score.MinHeight = 180;
        preview.ClearValue(FrameworkElement.HeightProperty);
        preview.MinHeight = 120;
        log.ClearValue(FrameworkElement.HeightProperty);
        log.MinHeight = 96;
    }

    private void UpdateNoteVisibility() =>
        documentWarning.Visibility = documentWarning.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

    // 分区卡片：统一圆角、1px 边框、内边距与标题，不用投影。
    private Border Card(string title, params UIElement[] children)
    {
        var body = new StackPanel();
        if (title.Length != 0) body.Children.Add(SectionTitle(title));
        foreach (var child in children) body.Children.Add(child);
        return new Border { Style = Ui("Card"), Child = body };
    }

    private static TextBlock SectionTitle(string text) => new()
    {
        Text = text,
        FontSize = 14,
        FontWeight = FontWeights.SemiBold,
        Foreground = UiTheme.Brush(UiTheme.PrimaryText),
        Margin = new Thickness(0, 0, 0, 8)
    };

    private static TextBlock Hint(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Foreground = UiTheme.Brush(UiTheme.MutedText),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 2, 0, 6)
    };

    private static TextBlock FieldLabel(string text) => new()
    {
        Text = text,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 8, 6),
        Foreground = UiTheme.Brush(UiTheme.SecondaryText)
    };

    // 横向一行：按钮或“标签 + 输入框”，窄窗口自动换行。
    private static WrapPanel Row(params UIElement[] children)
    {
        var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 2) };
        foreach (var child in children)
        {
            if (child is FrameworkElement element) element.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(child);
        }
        return row;
    }

    // 纵向区块。
    private static StackPanel Block(params UIElement[] children)
    {
        var block = new StackPanel();
        foreach (var child in children) block.Children.Add(child);
        return block;
    }
}
