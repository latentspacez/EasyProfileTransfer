using System;
using Godot;
using MegaCrit.Sts2.addons.mega_text;

namespace EasyProfileTransfer.UI;

/// <summary>
/// Local menu theme resolution for EasyProfileTransfer (no DataModel UI dependency).
/// </summary>
internal static class EasyProfileTransferThemeHelper
{
    private const string LabelType = "Label";
    private const string LabelFontKey = "font";
    private const string LabelFontSizeKey = "font_size";
    private const string LabelFontColorKey = "font_color";
    private const string LabelFontOutlineColorKey = "font_outline_color";
    private const string LabelFontShadowColorKey = "font_shadow_color";
    private const string LabelLineSpacingKey = "line_spacing";

    private static readonly string[] MenuThemeLoadPaths =
    {
        "res://themes/kreon_bold_shared.tres",
        "res://themes/kreon_regular_shared.tres",
    };

    public static Theme? ResolveMenuTheme(SceneTree? tree)
    {
        foreach (string path in MenuThemeLoadPaths)
        {
            Theme? theme = TryLoadTheme(path);
            if (theme != null)
            {
                return theme;
            }
        }

        Theme? project = ThemeDB.GetProjectTheme();
        if (project != null)
        {
            return project;
        }

        Theme? def = ThemeDB.GetDefaultTheme();
        if (def != null)
        {
            return def;
        }

        return FindThemeOnControlSubtree(tree?.Root);
    }

    public static Theme EnsureNonNullTheme(SceneTree? tree)
    {
        Theme? theme = ResolveMenuTheme(tree) ?? ThemeDB.GetDefaultTheme();
        if (theme != null)
        {
            return theme;
        }

        var fallback = new Theme();
        Font? ff = ThemeDB.FallbackFont;
        if (ff != null)
        {
            fallback.DefaultFont = ff;
            fallback.SetFont(LabelFontKey, LabelType, ff);
            fallback.SetFontSize(LabelFontSizeKey, LabelType, 18);
        }

        return fallback;
    }

    public static void ApplyLabelThemeItemsFromTheme(Control label, Theme theme)
    {
        if (theme.HasFont(LabelFontKey, LabelType))
        {
            label.AddThemeFontOverride(LabelFontKey, theme.GetFont(LabelFontKey, LabelType));
        }
        else if (theme.DefaultFont != null)
        {
            label.AddThemeFontOverride(LabelFontKey, theme.DefaultFont);
        }

        if (theme.HasFontSize(LabelFontSizeKey, LabelType))
        {
            label.AddThemeFontSizeOverride(LabelFontSizeKey, theme.GetFontSize(LabelFontSizeKey, LabelType));
        }
        else if (theme.DefaultFontSize > 0)
        {
            label.AddThemeFontSizeOverride(LabelFontSizeKey, theme.DefaultFontSize);
        }

        if (theme.HasColor(LabelFontColorKey, LabelType))
        {
            label.AddThemeColorOverride(LabelFontColorKey, theme.GetColor(LabelFontColorKey, LabelType));
        }

        if (theme.HasColor(LabelFontOutlineColorKey, LabelType))
        {
            label.AddThemeColorOverride(LabelFontOutlineColorKey, theme.GetColor(LabelFontOutlineColorKey, LabelType));
        }

        if (theme.HasColor(LabelFontShadowColorKey, LabelType))
        {
            label.AddThemeColorOverride(LabelFontShadowColorKey, theme.GetColor(LabelFontShadowColorKey, LabelType));
        }

        if (theme.HasConstant(LabelLineSpacingKey, LabelType))
        {
            label.AddThemeConstantOverride(LabelLineSpacingKey, theme.GetConstant(LabelLineSpacingKey, LabelType));
        }

        const string outlineSizeKey = "outline_size";
        if (theme.HasConstant(outlineSizeKey, LabelType))
        {
            label.AddThemeConstantOverride(outlineSizeKey, theme.GetConstant(outlineSizeKey, LabelType));
        }
    }

    private static Theme? TryLoadTheme(string path)
    {
        try
        {
            Resource? res = ResourceLoader.Load(path);
            if (res is Theme theme)
            {
                return theme;
            }

            if (res is Font font)
            {
                return BuildLabelThemeFromFont(font);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasyProfileTransfer] Theme load failed for {path}: {ex.Message}");
        }

        return null;
    }

    private static Theme BuildLabelThemeFromFont(Font font)
    {
        var theme = new Theme();
        theme.DefaultFont = font;
        theme.SetFont(LabelFontKey, LabelType, font);
        int size = theme.DefaultFontSize > 0 ? theme.DefaultFontSize : 22;
        theme.SetFontSize(LabelFontSizeKey, LabelType, size);
        return theme;
    }

    private static Theme? FindThemeOnControlSubtree(Node? node)
    {
        if (node == null)
        {
            return null;
        }

        if (node is Control c && c.Theme != null)
        {
            return c.Theme;
        }

        foreach (Node child in node.GetChildren())
        {
            Theme? theme = FindThemeOnControlSubtree(child);
            if (theme != null)
            {
                return theme;
            }
        }

        return null;
    }
}
