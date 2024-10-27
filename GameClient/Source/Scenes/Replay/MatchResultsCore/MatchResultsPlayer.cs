namespace GameClient.Scenes.Replay.MatchResultsCore;

/// <summary>
/// Represents the match results player.
/// </summary>
/// <param name="Nickname">The nickname of the player.</param>
/// <param name="Color">The color of the player.</param>
/// <param name="Points">The points of the player.</param>
/// <param name="Kills">The kills of the player.</param>
internal record class MatchResultsPlayer(string Nickname, uint Color, int Points, int Kills);
