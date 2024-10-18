﻿using System;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
using MonoRivUI;

namespace GameClient;

/// <summary>
/// Represents the game settings.
/// </summary>
internal static class GameSettings
{
    private const string SettingsFilePath = "settings.json";

    private static SettingsData data;

    /// <summary>
    /// Occurs when the language is changing.
    /// </summary>
    public static event EventHandler? LanguageChanging;

    /// <summary>
    /// Occurs when the language has changed.
    /// </summary>
    public static event EventHandler? LanguageChanged;

    /// <summary>
    /// Occurs when the resolution is changing.
    /// </summary>
    public static event EventHandler? ResolutionChanging;

    /// <summary>
    /// Occurs when the resolution has changed.
    /// </summary>
    public static event EventHandler? ResolutionChanged;

    /// <summary>
    /// Occurs when the screen type is changing.
    /// </summary>
    public static event EventHandler? ScreenTypeChanging;

    /// <summary>
    /// Occurs when the screen type has changed.
    /// </summary>
    public static event EventHandler? ScreenTypeChanged;

    /// <summary>
    /// Gets or sets the language of the game.
    /// </summary>
    public static Language Language
    {
        get => data.Language;
        set
        {
            if (data.Language == value)
            {
                return;
            }

            LanguageChanging?.Invoke(null, EventArgs.Empty);
            data.Language = value;
            LanguageChanged?.Invoke(null, EventArgs.Empty);
            ILocalizable.RefreshAll();
        }
    }

    /// <summary>
    /// Gets or sets the server address.
    /// </summary>
    public static string ServerAddress { get; set; } = "localhost:5000";

    /// <summary>
    /// Sets the resolution of the game.
    /// </summary>
    /// <param name="width">The width of the resolution.</param>
    /// <param name="height">The height of the resolution.</param>
    /// <exception cref="ArgumentOutOfRangeException">Width and height must be greater than 0.</exception>
    public static void SetResolution(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        if (ScreenController.Width == width && ScreenController.Height == height)
        {
            return;
        }

        ResolutionChanging?.Invoke(null, EventArgs.Empty);
        data.ResolutionWidth = width;
        data.ResolutionHeight = height;
        ScreenController.Change(width, height);
        ScreenController.ApplyChanges();
        ResolutionChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Sets the screen type of the game.
    /// </summary>
    /// <param name="screenType">The screen type to set.</param>
    public static void SetScreenType(ScreenType screenType)
    {
        if (ScreenController.ScreenType == screenType)
        {
            return;
        }

        ScreenTypeChanging?.Invoke(null, EventArgs.Empty);
        data.ScreenType = screenType;
        ScreenController.Change(screenType: screenType);
        ScreenController.ApplyChanges();
        ScreenTypeChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Discards the changes made to the settings.
    /// </summary>
    public static void DiscardChanges()
    {
        LoadSettings();
    }

    /// <summary>
    /// Saves the settings.
    /// </summary>
    public static void SaveSettings()
    {
        string json = JsonSerializer.Serialize(data);
        File.WriteAllText(SettingsFilePath, json);
    }

    /// <summary>
    /// Loads settings from file.
    /// </summary>
    public static async void LoadSettings()
    {
        if (!File.Exists(SettingsFilePath))
        {
            SetDefaultSettings();
            SaveSettings();
            return;
        }

        try
        {
            string json = await MonoTanks.InvokeOnMainThreadAsync(() => File.ReadAllText(SettingsFilePath));
            var settings = JsonSerializer.Deserialize<SettingsData>(json);

            Language = settings.Language;
            SetResolution(settings.ResolutionWidth, settings.ResolutionHeight);
            SetScreenType(settings.ScreenType);
        }
        catch (Exception ex)
        {
            DebugConsole.SendMessage($"Failed to load settings. Default settings will be used.\n{ex.Message}", Color.Red);
            SetDefaultSettings();
        }
    }

    private static void SetDefaultSettings()
    {
        Language = Language.English;
        SetResolution(1366, 768);
        SetScreenType(ScreenType.Windowed);
    }

    private record struct SettingsData(
        Language Language,
        int ResolutionWidth,
        int ResolutionHeight,
        ScreenType ScreenType);
}
