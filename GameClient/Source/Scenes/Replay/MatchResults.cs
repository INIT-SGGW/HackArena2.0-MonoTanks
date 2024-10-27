#if HACKATHON

using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameClient.Scenes.GameCore;
using GameClient.Scenes.Replay.MatchResultsCore;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoRivUI;
using Newtonsoft.Json.Linq;

namespace GameClient.Scenes.Replay;

/// <summary>
/// Represents the match results scene.
/// </summary>
[AutoInitialize]
[AutoLoadContent]
internal class MatchResults : Scene
{
    private readonly MatchResultsComponents components;
    private ReplaySceneDisplayEventArgs replayArgs = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="MatchResults"/> class.
    /// </summary>
    public MatchResults()
        : base(Color.Transparent)
    {
        var initializer = new MatchResultsInitializer(this);
        this.components = new MatchResultsComponents(initializer);
    }

    /// <inheritdoc/>
    public override void Update(GameTime gameTime)
    {
        UpdateMainMenuBackgroundEffectRotation(gameTime);

        if (KeyboardController.IsKeyHit(Keys.Space))
        {
            this.NextReplay();
        }

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
        this.Showing += this.MatchResults_Showing;
    }

    /// <inheritdoc/>
    protected override void LoadSceneContent()
    {
        var textures = this.BaseComponent.GetAllDescendants<TextureComponent>();
        textures.ToList().ForEach(x => x.Load());
    }

    private static void UpdateMainMenuBackgroundEffectRotation(GameTime gameTime)
    {
        MainEffect.Rotation += -0.05f * (float)gameTime.ElapsedGameTime.TotalSeconds;
        MainEffect.Rotation %= MathHelper.TwoPi;
    }

    private void MatchResults_Showing(object? sender, SceneDisplayEventArgs? e)
    {
        if (e is not ReplaySceneDisplayEventArgs args)
        {
            DebugConsole.ThrowError(
                $"Game scene requires {nameof(ReplaySceneDisplayEventArgs)}.");
            ChangeToPreviousOrDefault<MainMenu>();
            return;
        }

        this.replayArgs = (ReplaySceneDisplayEventArgs)e;

        this.UpdateMatchName(args.LobbyData.ServerSettings.MatchName);
        this.UpdateScoreboard(args);

        this.components.Scoreboard
            .GetAllDescendants<TextureComponent>()
            .Where(x => !x.IsLoaded)
            .ToList()
            .ForEach(x => x.Load());
    }

    private void UpdateScoreboard(ReplaySceneDisplayEventArgs args)
    {
        var results = JArray.Parse(args.MatchResults!["match_results"]!.ToString())!;
        List<MatchResultsPlayer> players = [];
        foreach (var r in results)
        {
            players.Add(new MatchResultsPlayer(
                r["nickname"]!.ToString(),
                (uint)r["color"]!,
                (int)r["total_points"]!,
                (int)r["total_kills"]!));
        }

        this.components.Scoreboard.SetPlayers(players);
    }

    private void UpdateMatchName(string? matchName)
    {
        this.components.MatchName.IsEnabled = matchName is not null;
        this.components.MatchName.Value = matchName ?? "-";
    }

    private void NextReplay()
    {

        var directory = PathUtils.GetAbsolutePath(ChooseReplay.ReplayDirectory);
        bool currentFound = false;
        foreach (var file in Directory.GetFiles(directory, "*.json"))
        {
            if (file.EndsWith("_match_results.json"))
            {
                continue;
            }

            if (this.replayArgs.AbsPath == PathUtils.GetAbsolutePath(file))
            {
                currentFound = true;
                continue;
            }

            if (!currentFound)
            {
                continue;
            }

            var args = new ReplaySceneDisplayEventArgs(file)
            {
                ShowMode = true,
            };

            Change<Lobby>(args);
        }
    }
}

#endif
