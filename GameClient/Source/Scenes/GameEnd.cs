using System;
using System.Linq;
using GameClient.Networking;
using GameClient.Scenes.GameCore;
using GameClient.Scenes.GameEndCore;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoRivUI;

namespace GameClient.Scenes;

/// <summary>
/// Represents the game end scene.
/// </summary>
[AutoInitialize]
[AutoLoadContent]
internal class GameEnd : Scene
{
    private readonly GameEndComponents components;
    private readonly GameEndUpdater updater;

#if HACKATHON
    private ReplaySceneDisplayEventArgs? replayArgs = default!;
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="GameEnd"/> class.
    /// </summary>
    public GameEnd()
        : base(Color.Transparent)
    {
        var initializer = new GameEndInitializer(this);
        this.components = new GameEndComponents(initializer);
        this.updater = new GameEndUpdater(this.components);
    }

    /// <inheritdoc/>
    public override void Update(GameTime gameTime)
    {
        UpdateMainMenuBackgroundEffectRotation(gameTime);

#if HACKATHON

        if (this.replayArgs is not null && KeyboardController.IsKeyHit(Keys.Space))
        {
            ChangeWithoutStack<Replay.MatchResults>(this.replayArgs);
        }

#endif

        base.Update(gameTime);
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
        this.Showing += this.GameEnd_Showing;
        this.Hiding += this.GameEnd_Hiding;
    }

    /// <inheritdoc/>
    protected override void LoadSceneContent()
    {
        var textures = this.BaseComponent.GetAllDescendants<TextureComponent>();
        textures.ToList().ForEach(x => x.Load());
    }

    private static void UpdateMainMenuBackgroundEffectRotation(GameTime gameTime)
    {
        MainEffect.Rotation += 0.3f * (float)gameTime.ElapsedGameTime.TotalSeconds;
        MainEffect.Rotation %= MathHelper.TwoPi;
    }

    private static void Connection_ErrorThrew(string error)
    {
        DebugConsole.ThrowError(error);
        ChangeToPreviousOrDefault<MainMenu>();
    }

    private void GameEnd_Showing(object? sender, SceneDisplayEventArgs? e)
    {
        if (e is not GameEndDisplayEventArgs args)
        {
            DebugConsole.ThrowError(
                $"Game scene requires {nameof(GameEndDisplayEventArgs)}.");
            ChangeToPreviousOrDefault<MainMenu>();
            return;
        }

#if HACKATHON
        this.replayArgs = args.ReplayArgs;
#endif

        this.updater.UpdateScoreboard(args.Players);

#if HACKATHON
        this.updater.UpdateMatchName(Game.Settings?.MatchName);

        this.components.ContinueButton.IsEnabled = this.replayArgs is null
            || !this.replayArgs.ShowMode;

#endif

        this.components.Scoreboard
            .GetAllDescendants<TextureComponent>()
            .Where(x => !x.IsLoaded)
            .ToList()
            .ForEach(x => x.Load());

        ServerConnection.ErrorThrew += Connection_ErrorThrew;
    }

    private void GameEnd_Hiding(object? sender, EventArgs e)
    {
        ServerConnection.ErrorThrew -= Connection_ErrorThrew;
    }
}
