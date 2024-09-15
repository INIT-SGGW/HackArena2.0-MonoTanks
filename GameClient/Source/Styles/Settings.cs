﻿using Microsoft.Xna.Framework;
using MonoRivUI;

namespace GameClient.Styles;

/// <summary>
/// Represents the setting styles.
/// </summary>
internal static class Settings
{
    /// <summary>
    /// The font used in the setting elements.
    /// </summary>
    private static readonly ScalableFont Font = new("Content\\Fonts\\Orbitron-SemiBold.ttf", 12);

    /// <summary>
    /// Gets the style of the selector scroll bar.
    /// </summary>
    public static Style<ScrollBar> SelectorScrollBar { get; } = new()
    {
        Action = (scrollBar) =>
        {
            var thumb = new RoundedSolidColor(Color.White * 0.8f, 5);
            scrollBar.Thumb = thumb;

            scrollBar.RelativeSize = 0.036f;
            scrollBar.IsPriority = true;
            scrollBar.Transform.RelativeOffset = new Vector2(0.07f, 0.0f);
            scrollBar.Transform.IgnoreParentPadding = true;
        },
    };

    /// <summary>
    /// Gets the style of the selector.
    /// </summary>
    public static Style<ISelector> SelectorStyle { get; } = new()
    {
        Action = (selector) =>
        {
            var activeColor = (MonoTanks.ThemeColor * 0.3f).WithAlpha(0xFF);
            var activeBackground = new RoundedSolidColor(activeColor, 20)
            {
                Parent = selector.ActiveContainer,
            };

            var inactiveBackground = new RoundedSolidColor(MonoTanks.ThemeColor, 20)
            {
                Parent = selector.InactiveContainer,
                Opacity = 0.35f,
            };

            // Info about the selected item (inactive)
            _ = new Text(Font, Color.White)
            {
                Parent = selector.InactiveContainer,
                TextAlignment = Alignment.Center,
                TextShrink = TextShrinkMode.Width,
                Case = TextCase.Upper,
            };

            var listBox = selector.ListBox;
            listBox.Orientation = Orientation.Vertical;
            listBox.Spacing = 10;
            listBox.Transform.RelativePadding = new Vector4(0.05f, 0.015f, 0.05f, 0.015f);

            selector.ItemSelected += (s, e) =>
            {
                var fontColor = SelectorButtonItem!.GetProperty<Color>("FontColor");
                foreach (var item in selector.Items)
                {
                    item.Button.Component.GetChild<Text>()!.Color = Color.White;
                }

                if (e is not null)
                {
                    e.Button.Component.GetChild<Text>()!.Color = MonoTanks.ThemeColor;
                }
            };

            if (listBox is ScrollableListBox scrollableListBox)
            {
                scrollableListBox.ScrollBar.ApplyStyle(SelectorScrollBar);
                scrollableListBox.DrawContentOnParentPadding = true;
            }
        },
    };

    /// <summary>
    /// Gets the style of the selector button item.
    /// </summary>
    public static IButton<Container>.Style SelectorButtonItem { get; } = new()
    {
        Action = (button) =>
        {
            button.Component.Transform.RelativePadding = new(0.05f, 0.1f, 0.05f, 0.1f);

            var background = new RoundedSolidColor(Color.Transparent, 20)
            {
                Parent = button.Component,
                Transform = { IgnoreParentPadding = true },
            };

            var hoverEffect = new RoundedSolidColor(Color.Transparent, 20)
            {
                Parent = background,
                Transform = { IgnoreParentPadding = true },
            };

            _ = new Text(Font, Color.White)
            {
                Parent = button.Component,
                TextAlignment = Alignment.Center,
                TextShrink = TextShrinkMode.Width,
                Case = TextCase.Upper,
            };
        },
        CustomProperties = [
            new Style.Property("FontColor", Color.White),
        ],
        HoverEntered = (s, e) =>
        {
            var background = e.GetChild<RoundedSolidColor>()!;
            background.GetChild<RoundedSolidColor>()!.Color = MonoTanks.ThemeColor * 0.7f;
        },
        HoverExited = (s, e) =>
        {
            var background = e.GetChild<RoundedSolidColor>()!;
            background.GetChild<RoundedSolidColor>()!.Color = Color.Transparent;
        },
    };
}
