using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using GameClient.Networking;
using GameClient.Scenes.GameCore;
using GameClient.Scenes.GameOverlays;
using GameLogic;
using GameLogic.Networking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoRivUI;

namespace GameClient.Scenes;

/// <summary>
/// Represents the game scene.
/// </summary>
[AutoInitialize]
internal class Game : Scene
{
    private readonly Dictionary<string, Player> players = [];
    private readonly GameComponents components;
    private readonly GameUpdater updater;

    private bool isContentLoading;
    private bool isContentUpdatedAfterLoad;

    private ReplaySceneDisplayEventArgs? replayArgs;
    private TimeSpan replayTime = TimeSpan.Zero;
    private int replayTick;
    private bool isReplayLoaded;
    private bool isReplayRunning;
    private bool isReplay;

    private GameStatePayload[] replayGameStates = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="Game"/> class.
    /// </summary>
    public Game()
        : base(Color.Transparent)
    {
        var initializer = new GameInitializer(this);
        this.components = new GameComponents(initializer);
        this.updater = new GameUpdater(this.components, this.players);
    }

    /// <summary>
    /// Gets or sets the server settings.
    /// </summary>
    public static ServerSettings? Settings { get; set; }

    /// <summary>
    /// Gets the server broadcast interval in milliseconds.
    /// </summary>
    /// <value>
    /// The server broadcast interval in seconds.
    /// When the value is -1, the server broadcast interval is not received yet.
    /// </value>
    public static int ServerBroadcastInterval => Settings?.BroadcastInterval ?? -1;

    /// <summary>
    /// Gets or sets the player ID.
    /// </summary>
    /// <remarks>
    /// If client is a spectator, this property is <see langword="null"/>.
    /// </remarks>
    public static string? PlayerId { get; set; }

    /// <inheritdoc/>
    public override void Update(GameTime gameTime)
    {
        UpdateMainMenuBackgroundEffectRotation(gameTime);

        if (this.isContentUpdatedAfterLoad && KeyboardController.IsKeyHit(Keys.Escape))
        {
            if (ScreenController.DisplayedOverlays.Any(x => x.Value is GameMenu or Scenes.Settings))
            {
                HideOverlay<GameMenu>();
                HideOverlay<Settings>();
            }
            else if (ScreenController.DisplayedOverlays.Any(x => x.Value is GameQuitConfirm))
            {
                HideOverlay<GameQuitConfirm>();
                HideOverlay<GameMenu>();
            }
            else
            {
                ShowOverlay<GameMenu>();
            }
        }

        if (!ScreenController.DisplayedOverlays.Any())
        {
            HandleInput();
        }

        if (this.IsContentLoaded)
        {
            this.isContentUpdatedAfterLoad = true;
        }

        if (this.isReplay && this.isReplayLoaded)
        {
            this.UpdateReplay(gameTime);
        }

        base.Update(gameTime);
    }

    /// <inheritdoc/>
    public override void Draw(GameTime gameTime)
    {
        ScreenController.GraphicsDevice.Clear(Color.Black);
        MainEffect.Draw();

        if (this.isContentLoading || !this.isContentUpdatedAfterLoad)
        {
            return;
        }

        if (this.isReplay && !this.isReplayLoaded)
        {
            return;
        }

        base.Draw(gameTime);
    }

    /// <inheritdoc/>
    protected override void Initialize(Component baseComponent)
    {
        this.Showing += this.Game_Showing;
        this.Hiding += this.Game_Hiding;
        this.Hid += this.Game_Hid;
    }

    /// <inheritdoc/>
    protected override void LoadSceneContent()
    {
        var textures = this.BaseComponent.GetAllDescendants<TextureComponent>();
        textures.ToList().ForEach(x => x.Load());

        GameSceneComponents.PlayerBarComponent.LoadContent();

        Sprites.Bullet.LoadContent();
        Sprites.DoubleBullet.LoadContent();
        Sprites.FogOfWar.LoadContent();
        Sprites.Mine.LoadContent();
        Sprites.SecondaryItem.LoadContent();
        Sprites.Tank.LoadContent();
        Sprites.Wall.LoadContent();
        Sprites.Zone.LoadContent();
    }

    private static void UpdateMainMenuBackgroundEffectRotation(GameTime gameTime)
    {
        if (MainEffect.Rotation != 0.0f)
        {
            int sign = MainEffect.Rotation is > MathHelper.Pi or < 0 and > -MathHelper.Pi ? 1 : -1;
            var value = 0.25f * (float)gameTime.ElapsedGameTime.TotalSeconds;
            MainEffect.Rotation += Math.Min(Math.Abs(MainEffect.Rotation), value) * sign;
            MainEffect.Rotation %= MathHelper.TwoPi;
        }
    }

    private static async void HandleInput()
    {
        var payload = GameInputHandler.HandleInputPayload();

        if (ServerConnection.Data.IsSpectator && payload is IActionPayload)
        {
            return;
        }

        if (payload is not null)
        {
            var message = PacketSerializer.Serialize(payload);
            await ServerConnection.SendAsync(message);
        }
    }

    private void Connection_MessageReceived(WebSocketReceiveResult result, string message)
    {
        if (result.MessageType == WebSocketMessageType.Close)
        {
            GameServerMessageHandler.HandleCloseMessage(result);
            return;
        }

        if (result.MessageType == WebSocketMessageType.Text)
        {
            Packet packet = PacketSerializer.Deserialize(message);
            switch (packet.Type)
            {
                case PacketType.Ping:
                    GameServerMessageHandler.HandlePingPacket();
                    break;

                case PacketType.GameState:
                    GameServerMessageHandler.HandleGameStatePacket(packet, this.updater);
                    break;

                case PacketType.GameEnded:
                    GameServerMessageHandler.HandleGameEndPacket(packet);
                    break;

                case PacketType.LobbyData:
                    GameServerMessageHandler.HandleLobbyDataPacket(packet, this.updater);
                    break;

                case PacketType.GameStarted:
                    break;

                default:
                    if (!packet.Type.IsGroup(PacketType.ErrorGroup))
                    {
                        DebugConsole.SendMessage(
                        $"Unknown packet type in Game scene: {packet.Type}",
                        Color.Yellow);
                    }

                    break;
            }
        }
    }

    private void UpdateReplay(GameTime gameTime)
    {
        if (KeyboardController.IsKeyHit(Keys.Left))
        {
            this.replayTime -= 100 * gameTime.ElapsedGameTime;
            if (this.replayTime < TimeSpan.Zero)
            {
                this.replayTime = TimeSpan.Zero;
            }
        }
        else if (KeyboardController.IsKeyHit(Keys.Right))
        {
            this.replayTime += 100 * gameTime.ElapsedGameTime;
        }

        if (this.isReplayRunning ^= KeyboardController.IsKeyHit(Keys.Space))
        {
            this.replayTime += gameTime.ElapsedGameTime;
            var lastReplayIndex = this.replayTick;
            this.replayTick = Math.Max((int)(this.replayTime.TotalMilliseconds / Settings!.BroadcastInterval), 0);

            if (this.replayTick >= this.replayGameStates.Length)
            {
                var args = new GameEndDisplayEventArgs(this.replayArgs!.GameEnd.Players)
                {
#if HACKATHON
                    ReplayArgs = this.replayArgs,
#endif
                };
                ChangeWithoutStack<GameEnd>(args);
            }
            else if (this.replayTick != lastReplayIndex)
            {
                this.UpdateReplayTick(this.replayTick);
            }
        }
    }

    private void UpdateReplayTick(int tick)
    {
        var gameState = this.replayGameStates[tick];
        this.updater.UpdateTimer(gameState.Tick);
        this.updater.UpdateGridLogic(gameState);
        this.updater.UpdatePlayers(gameState.Players);
        this.updater.RefreshPlayerBarPanels();
        this.updater.UpdatePlayersFogOfWar();
    }

    private async void Game_Showing(object? sender, SceneDisplayEventArgs? e)
    {
        this.isReplay = e is ReplaySceneDisplayEventArgs;
        this.replayArgs = e as ReplaySceneDisplayEventArgs;

        if (!this.isReplay)
        {
            ServerConnection.BufferSize = 1024 * 32;
            ServerConnection.MessageReceived += this.Connection_MessageReceived;
        }

        this.components.MenuButton.IsEnabled = !this.isReplay
#if HACKATHON
            || (!this.replayArgs?.ShowMode ?? true);
#endif
        ;

        if (!this.IsContentLoaded)
        {
            this.isContentLoading = true;
            ShowOverlay<Loading>();
            await Task.Run(this.LoadContent);
            HideOverlay<Loading>();
            this.isContentLoading = false;
        }

        if (this.isReplay)
        {
            try
            {
                ShowOverlay<Loading>();

                await Task.Run(() =>
                {
                    var replay = (e as ReplaySceneDisplayEventArgs)!;
                    this.replayGameStates = replay.GameStates;
                    this.updater.EnableGridComponent();
                    this.UpdateReplayTick(0);
                    this.isReplayLoaded = true;
                });
            }
            catch (Exception ex)
            {
                DebugConsole.ThrowError("Failed to load replay data.");
                DebugConsole.ThrowError(ex);
                ChangeWithoutStack<Replay.ChooseReplay>();
                return;
            }
            finally
            {
                HideOverlay<Loading>();
            }
        }

        if (!this.isReplay)
        {
            var gameSceneLoadedPayload = new EmptyPayload() { Type = PacketType.ReadyToReceiveGameState };
            var packet = PacketSerializer.Serialize(gameSceneLoadedPayload);
            await ServerConnection.SendAsync(packet);
        }

        if (!this.isReplay && Settings is null)
        {
            var lobbyDataRequest = new EmptyPayload() { Type = PacketType.LobbyDataRequest };
            var packet = PacketSerializer.Serialize(lobbyDataRequest);
            await ServerConnection.SendAsync(packet);
        }
#if HACKATHON
        else
        {
            string? matchName = Settings?.MatchName;
            if (Settings is not null && Settings.SandboxMode)
            {
                if (matchName is null)
                {
                    matchName = "sandbox";
                }
                else
                {
                    matchName += " [sandbox]";
                }
            }

            this.updater.UpdateMatchName(matchName);
        }
#endif
    }

    private async void Game_Hiding(object? sender, EventArgs e)
    {
        this.components.Grid.ResetAllFogsOfWar();
        this.updater.DisableGridComponent();

        /* TODO: Unload content */

        this.components.Grid.ClearSprites();
        this.components.PlayerIdentityBarPanel.Clear();
        this.components.PlayerStatsBarPanel.Clear();

#if HACKATHON
        this.updater.UpdateMatchName(null);
#endif
        this.updater.UpdateTimer(0);

        if (ServerConnection.IsConnected)
        {
            await ServerConnection.CloseAsync();
        }

        ServerConnection.MessageReceived -= this.Connection_MessageReceived;
    }

    private void Game_Hid(object? sender, EventArgs e)
    {
        this.players.Clear();
        this.replayGameStates = [];
        this.replayTick = -1;
        this.isReplay = false;
        this.isReplayLoaded = false;
        this.isReplayRunning = false;
        this.replayTime = TimeSpan.Zero;
    }
}
