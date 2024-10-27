#if HACKATHON

using GameClient.UI.MatchResultsSceneComponents;
using MonoRivUI;

namespace GameClient.Scenes.Replay.MatchResultsCore;

/// <summary>
/// Represents the match results components.
/// </summary>
/// <param name="initializer">
/// The match results initializer that will be used to create the game end components.
/// </param>
internal class MatchResultsComponents(MatchResultsInitializer initializer)
{
    /// <summary>
    /// Gets the match name text component.
    /// </summary>
    public Text MatchName { get; } = initializer.CreateMatchName();

    /// <summary>
    /// Gets the scoreboard component.
    /// </summary>
    public Scoreboard Scoreboard { get; } = initializer.InitializeScoreboard();
}

#endif
