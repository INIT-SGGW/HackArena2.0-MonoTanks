using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoRivUI;

namespace GameClient.Scenes.Replay;

/// <summary>
/// Represents the choose replay scene.
/// </summary>
[AutoInitialize]
[AutoLoadContent]
internal class ChooseReplay : Scene
{
    /// <summary>
    /// The replay directory.
    /// </summary>
    public const string ReplayDirectory = "Replays";

    private ScrollableListBox listBox = default!;
    private ScalableFont font = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChooseReplay"/> class.
    /// </summary>
    public ChooseReplay()
        : base()
    {
    }

    /// <inheritdoc/>
    public override void Draw(GameTime gameTime)
    {
        ScreenController.GraphicsDevice.Clear(Color.Black);
        MainEffect.Draw();
        base.Draw(gameTime);
    }

    /// <inheritdoc/>
    protected override void Initialize(Component baseComponent)
    {
        this.Showing += this.ChooseReplay_Showing;

        try
        {
            var directory = PathUtils.GetAbsolutePath(ReplayDirectory);
            if (!Directory.Exists(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }
        }
        catch (IOException ex)
        {
            DebugConsole.ThrowError("Failed to create replay directory.");
            DebugConsole.ThrowError(ex);
        }

        var titleFont = new ScalableFont(Styles.Fonts.Paths.Main, 22)
        {
            AutoResize = true,
            Spacing = 5,
        };

        var title = new LocalizedText(titleFont, Color.White)
        {
            Parent = baseComponent,
            Value = new LocalizedString("Buttons.WatchReplay"),
            TextAlignment = Alignment.Top,
            Case = TextCase.Upper,
            Transform =
            {
                Alignment = Alignment.Top,
                RelativeOffset = new Vector2(0.0f, 0.08f),
                RelativeSize = new Vector2(0.5f, 0.1f),
            },
        };

        this.font = new ScalableFont(Styles.Fonts.Paths.Main, 14)
        {
            AutoResize = true,
            Spacing = 5,
        };

        var background = new RoundedSolidColor(MonoTanks.ThemeColor * 0.33f, 30)
        {
            Parent = baseComponent,
            AutoAdjustRadius = true,
            Transform =
            {
                RelativeSize = new Vector2(0.6f),
                Alignment = Alignment.Center,
            },
        };

        var thumb = new RoundedSolidColor(Color.White * 0.8f, 8)
        {
            AutoAdjustRadius = true,
        };

        this.listBox = new ScrollableListBox(thumb)
        {
            Parent = background,
            Spacing = 10,
            DrawContentOnParentPadding = true,
            Transform =
            {
                IgnoreParentPadding = true,
                RelativePadding = new Vector4(0.05f),
            },
        };

        this.listBox.ScrollBar.RelativeSize = 0.015f;
        this.listBox.ScrollBar.IsPriority = true;
        this.listBox.ScrollBar.Transform.RelativeOffset = new Vector2(0.03f, 0.0f);
        this.listBox.ScrollBar.Transform.IgnoreParentPadding = true;

        var backButton = new Button<Container>(new Container())
        {
            Parent = baseComponent,
            Transform =
            {
                Alignment = Alignment.BottomLeft,
                RelativeOffset = new Vector2(0.08f, -0.08f),
                RelativeSize = new Vector2(0.2f, 0.07f),
            },
        };

        backButton.ApplyStyle(Styles.UI.BackButtonStyle);
        backButton.Clicked += (s, e) => ChangeToPreviousOrDefault<MainMenu>();
    }

    /// <inheritdoc/>
    protected override void LoadSceneContent()
    {
        var textures = this.BaseComponent.GetAllDescendants<TextureComponent>();
        textures.ToList().ForEach(x => x.Load());
    }

    private void ChooseReplay_Showing(object? sender, EventArgs? e)
    {
        var listBox = this.BaseComponent.GetDescendant<ScrollableListBox>();
        this.listBox.Clear();

        var directory = PathUtils.GetAbsolutePath(ReplayDirectory);
        if (!Directory.Exists(directory))
        {
            DebugConsole.ThrowError("Replay directory does not exist.");
            Change<MainMenu>();
            return;
        }

        foreach (var file in Directory.GetFiles(directory, "*.json"))
        {
#if HACKATHON
            if (file.EndsWith("_match_results.json"))
            {
                continue;
            }
#endif
            var button = new Button<RoundedSolidColor>(new RoundedSolidColor(MonoTanks.ThemeColor * 0.44f, 35) { AutoAdjustRadius = true })
            {
                Parent = this.listBox.ContentContainer,
                Transform =
                {
                    RelativeSize = new Vector2(1f, 0.11f),
                },
            };

            button.Component.Load();
            button.Clicked += (s, e) =>
            {
#if !HACKATHON
                var args = new GameCore.ReplaySceneDisplayEventArgs(file);
#else
                var showMode = KeyboardController.IsKeyDown(Keys.LeftControl);
                var args = new GameCore.ReplaySceneDisplayEventArgs(file)
                {
                    ShowMode = showMode,
                };
#endif
                Change<Lobby>(args);
            };

            button.HoverEntered += (s, e) => button.Component.Color = MonoTanks.ThemeColor * 0.66f;
            button.HoverExited += (s, e) => button.Component.Color = MonoTanks.ThemeColor * 0.44f;

            var text = new Text(this.font, Color.White)
            {
                Parent = button.Component,
                Value = Path.GetFileNameWithoutExtension(file),
                TextShrink = TextShrinkMode.HeightAndWidth,
                TextAlignment = Alignment.Center,
                Transform =
                {
                    RelativeSize = new Vector2(0.8f),
                    Alignment = Alignment.Center,
                },
            };
        }
    }
}
