using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using GameClient.Networking;
using Microsoft.Xna.Framework;
using MonoRivUI;

namespace GameClient.Scenes;

/// <summary>
/// Represents the main menu scene.
/// </summary>
[AutoInitialize]
[AutoLoadContent]
internal class MainMenu : Scene
{
    private Text title = default!;
    private ScalableTexture2D logo = default!;
    private ScalableTexture2D element = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainMenu"/> class.
    /// </summary>
    public MainMenu()
        : base(Color.Transparent)
    {
    }

    /// <inheritdoc/>
    public override void Update(GameTime gameTime)
    {
        MainEffect.Rotation += 0.05f * (float)gameTime.ElapsedGameTime.TotalSeconds;
        MainEffect.Rotation %= MathHelper.TwoPi;

        base.Update(gameTime);
    }

    /// <inheritdoc/>
    public override void Draw(GameTime gameTime)
    {
        MainEffect.Draw();
        base.Draw(gameTime);
    }

    /// <inheritdoc/>
    protected override void Initialize(Component baseComponent)
    {
        this.element = new ScalableTexture2D("Images/MainMenu/background_element.svg")
        {
            Parent = baseComponent,
            Transform =
            {
                RelativeSize = new Vector2(0.45f),
                Alignment = Alignment.BottomRight,
                RelativeOffset = new Vector2(-0.08f, -0.14f),
            },
        };

        var titleFont = new ScalableFont(Styles.Fonts.Paths.Main, 53)
        {
            AutoResize = true,
            Spacing = 20,
        };

        this.title = new Text(titleFont, Color.White)
        {
            Parent = baseComponent,
            Value = "MONO TANKS",
#if DEBUG
            AdjustTransformSizeToText = AdjustSizeOption.HeightAndWidth,
#endif
            TextShrink = TextShrinkMode.HeightAndWidth,
            Transform =
            {
                Alignment = Alignment.TopLeft,
                RelativeOffset = new Vector2(0.06f, 0.28f),
            },
        };

#if DEBUG
        var debugInfoFont = new ScalableFont(Styles.Fonts.Paths.Main, 24)
        {
            AutoResize = true,
            Spacing = 52,
        };

        _ = new Text(debugInfoFont, Color.White * 0.9f)
        {
            Parent = this.title,
            Value = "[Debug]",
            TextAlignment = Alignment.Right,
            Transform =
            {
                RelativeSize = new Vector2(0.1f),
                Alignment = Alignment.BottomRight,
                RelativeOffset = new Vector2(0.0f, 0.5f),
            },
        };
#endif

        this.logo = new ScalableTexture2D("Images/logo.svg")
        {
            Parent = baseComponent,
            Transform =
            {
                RelativeSize = new Vector2(0.1f),
                Alignment = Alignment.TopRight,
                RelativeOffset = new Vector2(-0.08f, 0.08f),
            },
        };

        var listBox = new ListBox
        {
            Parent = baseComponent,
            Spacing = 15,
            Transform =
            {
                RelativeSize = new Vector2(0.32f, 0.32f),
                Alignment = Alignment.Left,
                RelativeOffset = new Vector2(0.06f, 0.08f),
            },
        };

        var joinRoomBtn = CreateButton(new LocalizedString("Buttons.JoinGame"), listBox, "join");
        joinRoomBtn.Clicked += (s, e) => Change<JoinRoom>();

        var watchReplayBtn = CreateButton(new LocalizedString("Buttons.WatchReplay"), listBox, "watch_replay");
        watchReplayBtn.Clicked += (s, e) => Change<Replay.ChooseReplay>();

        var settingsBtn = CreateButton(new LocalizedString("Buttons.Settings"), listBox, "settings");
        settingsBtn.Clicked += (s, e) => Change<Settings>();

        var exitBtn = CreateButton(new LocalizedString("Buttons.Exit"), listBox, "exit");
        exitBtn.Clicked += (s, e) => MonoTanks.Instance.Exit();

#if DEBUG
        var quickJoinFont = new ScalableFont(Styles.Fonts.Paths.Main, 9);

        async void Connect<T>(bool isSpectator)
            where T : Scene
        {
            ServerConnection.ErrorThrew += DebugConsole.ThrowError;

            string? joinCode = null;
            ConnectionData connectionData = isSpectator
                ? ConnectionData.ForSpectator("localhost:5000", joinCode, true)
                : ConnectionData.ForPlayer("localhost:5000", joinCode, "player", true);

            ConnectionStatus status = await ServerConnection.ConnectAsync(connectionData, CancellationToken.None);

            if (status is ConnectionStatus.Success)
            {
                Change<T>();
            }
            else if (status is ConnectionStatus.Failed failed && failed.Exception is not null)
            {
                DebugConsole.ThrowError("Connection failed!");
                DebugConsole.ThrowError(failed.Exception);
            }
            else if (status is ConnectionStatus.Rejected rejected)
            {
                DebugConsole.ThrowError($"Connection rejected: {rejected.Reason}");
            }
            else
            {
                DebugConsole.ThrowError("Failed to connect to the server.");
            }

            ServerConnection.ErrorThrew -= DebugConsole.ThrowError;
        }

        // Quick join to game
        {
            var quickJoinPlayerBtn = new Button<SolidColor>(new SolidColor(Color.DarkRed))
            {
                Parent = this.BaseComponent,
                Transform =
            {
                Alignment = Alignment.BottomRight,
                RelativeOffset = new Vector2(-0.12f, -0.04f),
                RelativeSize = new Vector2(0.15f, 0.06f),
            },
            };
            quickJoinPlayerBtn.HoverEntered += (s, e) => e.Color = Color.Red;
            quickJoinPlayerBtn.HoverExited += (s, e) => e.Color = Color.DarkRed;
            quickJoinPlayerBtn.Clicked += (s, e) => Connect<Game>(false);
            _ = new Text(quickJoinFont, Color.White)
            {
                Parent = quickJoinPlayerBtn.Component,
                Value = "Quick Join",
                TextAlignment = Alignment.Center,
                TextShrink = TextShrinkMode.Width,
                Transform =
            {
                RelativeSize = new Vector2(1f, 0.5f),
                Alignment = Alignment.Top,
            },
            };
            _ = new Text(quickJoinFont, Color.White)
            {
                Parent = quickJoinPlayerBtn.Component,
                Value = "(player)",
                TextAlignment = Alignment.Center,
                TextShrink = TextShrinkMode.Width,
                Transform =
                {
                    RelativeSize = new Vector2(1f, 0.5f),
                    Alignment = Alignment.Bottom,
                },
            };

            var quickJoinSpectatorBtn = new Button<SolidColor>(new SolidColor(Color.DarkRed))
            {
                Parent = this.BaseComponent,
                Transform =
                {
                    Alignment = Alignment.BottomRight,
                    RelativeOffset = new Vector2(-0.12f, -0.12f),
                    RelativeSize = new Vector2(0.15f, 0.06f),
                },
            };
            quickJoinSpectatorBtn.HoverEntered += (s, e) => e.Color = Color.Red;
            quickJoinSpectatorBtn.HoverExited += (s, e) => e.Color = Color.DarkRed;
            quickJoinSpectatorBtn.Clicked += (s, e) => Connect<Game>(true);
            _ = new Text(quickJoinFont, Color.White)
            {
                Parent = quickJoinSpectatorBtn.Component,
                Value = "Quick Join",
                TextAlignment = Alignment.Center,
                TextShrink = TextShrinkMode.Width,
                Transform =
                {
                    RelativeSize = new Vector2(1f, 0.5f),
                    Alignment = Alignment.Top,
                },
            };
            _ = new Text(quickJoinFont, Color.White)
            {
                Parent = quickJoinSpectatorBtn.Component,
                Value = "(spectator)",
                TextAlignment = Alignment.Center,
                TextShrink = TextShrinkMode.Width,
                Transform =
                {
                    RelativeSize = new Vector2(1f, 0.5f),
                    Alignment = Alignment.Bottom,
                },
            };
        }
#endif

        var versionFont = new ScalableFont(Styles.Fonts.Paths.Main, 6)
        {
            AutoResize = true,
            Spacing = 3,
            MinSize = 5,
        };

        var assembly = typeof(MonoTanks).Assembly;
        var version = assembly.GetName().Version!;
        var configuration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;

        var sb = new StringBuilder()
            .Append('v')
            .Append(version.Major)
            .Append('.')
            .Append(version.Minor)
            .Append('.')
            .Append(version.Build);

#if RELEASE
        string versionText = sb.ToString();
#endif

        sb.Append('.')
            .Append(version.Revision)
            .Append(" (")
            .Append(MonoTanks.Platform)
            .Append(')');

#if DEBUG
        string versionText = sb.ToString();
#endif

        _ = new Text(versionFont, Color.White * 0.8f)
        {
            Parent = this.BaseComponent,
            Value = versionText,
            AdjustTransformSizeToText = AdjustSizeOption.HeightAndWidth,
            TextAlignment = Alignment.BottomLeft,
            Transform =
            {
                Alignment = Alignment.BottomLeft,
                RelativeOffset = new Vector2(0.012f, -0.02f),
            },
        };

        sb.Append(" [")
            .Append(configuration)
            .Append(']');

        DebugConsole.SendMessage("Version: " + sb.ToString(), new Color(0xC5, 0x48, 0xFF));
    }

    /// <inheritdoc/>
    protected override void LoadSceneContent()
    {
        var textures = this.BaseComponent.GetAllDescendants<TextureComponent>();
        textures.ToList().ForEach(x => x.Load());
    }

    private static IButton<Container> CreateButton(
        LocalizedString text,
        ListBox listbox,
        string iconName)
    {
        var button = new Button<Container>(new Container())
        {
            Parent = listbox.ContentContainer,
            Transform =
            {
                RelativeSize = new Vector2(1, 0.20f),
            },
        };

        var iconPath = $"Images/Icons/{iconName}.svg";
        var style = Styles.UI.GetButtonStyleWithIcon(text, iconPath, Alignment.Left);
        button.ApplyStyle(style);

        return button;
    }
}
