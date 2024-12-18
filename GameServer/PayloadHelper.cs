﻿using GameLogic.Networking;
using Newtonsoft.Json;

namespace GameServer;

/// <summary>
/// A helper class for payloads.
/// </summary>
/// <param name="game">The game instance to get the payload for.</param>
internal class PayloadHelper(GameInstance game)
{
    private List<GameLogic.Player> Players => game.Players.Select(x => x.Instance).ToList();

    /// <summary>
    /// Gets the lobby data payload.
    /// </summary>
    /// <param name="connection">The connection to get the lobby data for.</param>
    /// <returns>The lobby data payload.</returns>
    /// <remarks>
    /// If the connection is null, the payload will be
    /// for internal use (e.g. replay manager).
    /// </remarks>
    public LobbyDataPayload GetLobbyDataPayload(Connection? connection)
    {
        string? playerId = (connection as PlayerConnection)?.Instance.Id;
        return new LobbyDataPayload(playerId, this.Players, game.Settings);
    }

    /// <summary>
    /// Gets the lobby data payload.
    /// </summary>
    /// <param name="connection">The connection to get the lobby data for.</param>
    /// <param name="converters">The list of converters to serialize with.</param>
    /// <returns>The lobby data payload.</returns>
    /// <remarks>
    /// If the connection is null, the payload will be
    /// for internal use (e.g. replay manager).
    /// </remarks>
    public LobbyDataPayload GetLobbyDataPayload(Connection? connection, out List<JsonConverter> converters)
    {
        converters = LobbyDataPayload.GetConverters();
        return this.GetLobbyDataPayload(connection);
    }

    /// <summary>
    /// Gets the game status payload.
    /// </summary>
    /// <returns>The game status payload.</returns>
    /// <remarks>
    /// If the game status is invalid, an error payload will be returned.
    /// </remarks>
    public IPacketPayload GetGameStatusPayload()
    {
        return game.GameManager.Status switch
        {
            GameStatus.InLobby => new EmptyPayload() { Type = PacketType.GameNotStarted },
            GameStatus.Starting => new EmptyPayload() { Type = PacketType.GameStarting },
            GameStatus.Running => new EmptyPayload() { Type = PacketType.GameInProgress },
            GameStatus.Ended => new EmptyPayload() { Type = PacketType.GameEnded },
            _ => new ErrorPayload(
                PacketType.InvalidPacketUsageError | PacketType.HasPayload,
                "Invalid game status."),
        };
    }

    /// <summary>
    /// Gets the game state payload.
    /// </summary>
    /// <param name="connection">The connection to get the game state for.</param>
    /// <param name="tick">The current tick of the game.</param>
    /// <param name="gameStateId">The game state id to get the payload for.</param>
    /// <returns>The game state payload.</returns>
    /// <remarks>
    /// If the connection is null, the payload will be
    /// for internal use (e.g. replay manager).
    /// </remarks>
    public GameStatePayload GetGameStatePayload(Connection? connection, int tick, string gameStateId)
    {
        return connection is PlayerConnection player
            ? new GameStatePayload.ForPlayer(gameStateId, tick, player.Instance, this.Players, game.Grid)
            : new GameStatePayload(tick, this.Players, game.Grid);
    }

    /// <summary>
    /// Gets the game state payload.
    /// </summary>
    /// <param name="connection">The connection to get the game state for.</param>
    /// <param name="tick">The current tick of the game.</param>
    /// <param name="gameStateId">The game state id to get the payload for.</param>
    /// <param name="converters">The list of converters to serialize with.</param>
    /// <returns>The game state payload.</returns>
    /// <remarks>
    /// If the connection is null, the payload will be
    /// for internal use (e.g. replay manager).
    /// </remarks>
    public GameStatePayload GetGameStatePayload(Connection? connection, int tick, string gameStateId, out List<JsonConverter> converters)
    {
        GameSerializationContext context = connection is PlayerConnection player
            ? new GameSerializationContext.Player(player.Instance, connection.Data.EnumSerialization)
            : new GameSerializationContext.Spectator();

        converters = GameStatePayload.GetConverters(context);
        return this.GetGameStatePayload(connection, tick, gameStateId);
    }

    /// <summary>
    /// Gets the game end payload.
    /// </summary>
    /// <returns>The game end payload.</returns>
    /// <remarks>
    /// If the connection is null, the payload will be
    /// for internal use (e.g. replay manager).
    /// </remarks>
    public GameEndPayload GetGameEndPayload()
    {
        var players = this.Players;
        players.Sort((x, y) => y.Score.CompareTo(x.Score));
        return new GameEndPayload(players);
    }

    /// <summary>
    /// Gets the game end payload.
    /// </summary>
    /// <param name="converters">The list of converters to serialize with.</param>
    /// <returns>The game end payload.</returns>
    /// <remarks>
    /// If the connection is null, the payload will be
    /// for internal use (e.g. replay manager).
    /// </remarks>
    public GameEndPayload GetGameEndPayload(out List<JsonConverter> converters)
    {
        converters = GameEndPayload.GetConverters();
        return this.GetGameEndPayload();
    }

#if HACKATHON

    /// <summary>
    /// Gets the game end results payload.
    /// </summary>
    /// <param name="converters">The list of converters to serialize with.</param>
    /// <returns>The game end results payload.</returns>
    internal GameEndPayload GetGameEndResultsPayload(out List<JsonConverter> converters)
    {
        converters = GameEndPayload.GetConverters();

        var players = this.Players;
        bool isValid = true;

        if (game.DisconnectedInGamePlayers.Any())
        {
            isValid = false;
            players.AddRange(game.DisconnectedInGamePlayers.Select(x => x.Instance));
        }

        players.Sort((x, y) => y.Score.CompareTo(x.Score));

        return new GameEndResultsPayload(players, isValid);
    }

    private record class GameEndResultsPayload(List<GameLogic.Player> Players, bool IsValid)
        : GameEndPayload(Players)
    {
    }

#endif
}
