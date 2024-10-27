using System.Collections.Generic;
using GameClient.Scenes.GameCore;
using GameLogic;
using MonoRivUI;

namespace GameClient.Scenes;

/// <summary>
/// Represents the event arguments for the <see cref="GameEnd"/> scene.
/// </summary>
internal class GameEndDisplayEventArgs(IEnumerable<Player> players) : SceneDisplayEventArgs(false)
{
    /// <summary>
    /// Gets the join code to join the game.
    /// </summary>
    public IEnumerable<Player> Players { get; } = players;

#if HACKATHON

    /// <summary>
    /// Gets the replay scene display arguments.
    /// </summary>
    public ReplaySceneDisplayEventArgs? ReplayArgs { get; init; }

#endif
}
