using System.Diagnostics;
using System.Net.WebSockets;
using GameLogic.Networking;
using Newtonsoft.Json.Linq;

namespace GameServer;

/// <summary>
/// Represents the game manager.
/// </summary>
/// <param name="game">The game instance.</param>
/// <param name="saveReplayPath">The path to save the replay.</param>
internal class GameManager(GameInstance game, string? saveReplayPath)
{
#if HACKATHON
    // Used to shuffle the bot actions.
    private readonly Random random = new(game.Settings.Seed);
#endif

    private readonly List<string> gameStates = [];
    private string lobbyData = string.Empty;

    private int tick = 0;

    /// <summary>
    /// Gets a status of the game.
    /// </summary>
    public GameStatus Status { get; private set; }

    /// <summary>
    /// Gets the current game state id.
    /// </summary>
    public string? CurrentGameStateId { get; private set; }

    /// <summary>
    /// Starts the game.
    /// </summary>
    public void StartGame()
    {
        lock (this)
        {
            if (this.Status is GameStatus.Running)
            {
                return;
            }

            this.Status = GameStatus.Running;
        }

        var lobbyData = new LobbyDataPayload(
            PlayerId: null,
            [.. game.PlayerManager.Players.Values.Select(x => x.Instance)],
            game.Settings);

        var options = new SerializationOptions() { Formatting = Newtonsoft.Json.Formatting.None };
        var converters = LobbyDataPayload.GetConverters();
        _ = PacketSerializer.Serialize(lobbyData, out var lobbyDataObj, converters, options);
        this.lobbyData = lobbyDataObj.ToString(options.Formatting);

        foreach (var player in game.PlayerManager.Players.Keys)
        {
            var packet = new EmptyPayload() { Type = PacketType.GameStart };
            _ = game.SendPlayerPacketAsync(player, packet);
        }

        _ = Task.Run(this.StartBroadcastingAsync);
    }

    /// <summary>
    /// Ends the game.
    /// </summary>
    public void EndGame()
    {
        this.Status = GameStatus.Ended;

        var players = game.PlayerManager.Players.Values.Select(x => x.Instance).ToList();
        players.Sort((x, y) => y.Score.CompareTo(x.Score));

        var payload = new GameEndPayload(players);
        var converters = GameEndPayload.GetConverters();

        foreach (var player in game.PlayerManager.Players.Keys)
        {
            _ = game.SendPlayerPacketAsync(player, payload, converters);
        }

        foreach (var spectator in game.SpectatorManager.Spectators.Keys)
        {
            _ = game.SendSpectatorPacketAsync(spectator, payload, converters);
        }

        var clients = game.PlayerManager.Players.Keys.Concat(game.SpectatorManager.Spectators.Keys).ToList();

        foreach (var client in clients)
        {
            _ = client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Game ended", CancellationToken.None);
        }

        JObject results = [];

        if (saveReplayPath is not null)
        {
            try
            {
                var options = new SerializationOptions() { Formatting = Newtonsoft.Json.Formatting.None };

                _ = PacketSerializer.Serialize(payload, out results, converters, options);

                var jObject = new JObject()
                {
                    ["lobbyData"] = JObject.Parse(this.lobbyData),
                    ["gameStates"] = JArray.Parse($"[{string.Join(",", this.gameStates)}]"),
                    ["gameEnd"] = JObject.Parse(results.ToString()),
                };

                File.WriteAllText(saveReplayPath, jObject.ToString(options.Formatting));
                Console.WriteLine($"Replay saved to {saveReplayPath}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error while saving replay: {e.Message}");
            }

#if HACKATHON
            try
            {
                var path = Path.GetDirectoryName(saveReplayPath)!;
                var fileName = Path.GetFileNameWithoutExtension(saveReplayPath);
                var extension = Path.GetExtension(saveReplayPath);
                var savePath = Path.Combine(path, $"{fileName}_results{extension}");
                File.WriteAllText(savePath, results.ToString());

                Console.WriteLine($"Results saved to {savePath}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error while saving results: {e.Message}");
            }
#endif
        }

        Environment.Exit(0);
    }

    private async Task StartBroadcastingAsync()
    {
        // Give some time for the clients to load the game
        await PreciseTimer.PreciseDelay(game.Settings.BroadcastInterval);

        var stopwatch = new Stopwatch();

        while (this.Status is GameStatus.Running)
        {
            if (this.tick++ >= game.Settings.Ticks)
            {
                this.EndGame();
                break;
            }

            stopwatch.Restart();

            var grid = game.Grid;

#if HACKATHON
            var botActions = game.PacketHandler.HackathonBotActions;
            var actionList = botActions.ToList();
            actionList.Sort((x, y) => x.Key.Instance.Nickname.CompareTo(y.Key.Instance.Nickname));
            Action[] actions = actionList.Select(x => x.Value).ToArray();
            this.random.Shuffle(actions);

            foreach (Action action in actions)
            {
                action.Invoke();
            }

            game.PacketHandler.HackathonBotActions.Clear();
#endif

            // Update game logic
            grid.UpdateBullets(1f);
            grid.RegeneratePlayersBullets();
            grid.UpdateTanksRegenerationProgress();
            grid.UpdatePlayersVisibilityGrids();
            grid.UpdateZones();

            // Broadcast the game state
            this.ResetPlayerGameTickProperties();
            var broadcast = this.BroadcastGameStateAsync();

            if (saveReplayPath is not null)
            {
                var payload = new GameStatePayload(this.tick, [.. game.PlayerManager.Players.Values.Select(x => x.Instance)], game.Grid);
                var context = new GameSerializationContext.Spectator();
                var converters = GameStatePayload.GetConverters(context);
                var options = new SerializationOptions() { Formatting = Newtonsoft.Json.Formatting.None };
                _ = PacketSerializer.Serialize(payload, out var gameState, converters, options);
                this.gameStates.Add(gameState.ToString(options.Formatting));
            }

            await Task.WhenAll(broadcast);

            stopwatch.Stop();

            var sleepTime = (int)(game.Settings.BroadcastInterval - stopwatch.ElapsedMilliseconds);

#if HACKATHON
            var tcs = new TaskCompletionSource<bool>();

            void EagerBroadcast(object? sender, Player player)
            {
                lock (player)
                {
                    if (this.tick > 5 // Warm-up period
                        && game.Settings.EagerBroadcast
                        && game.PlayerManager.Players.Values.All(x => x.IsHackathonBot && x.HasMadeActionToCurrentGameState))
                    {
                        _ = tcs.TrySetResult(true);
                    }
                }
            }

            if (sleepTime > 0)
            {
                game.PacketHandler.HackathonBotMadeAction += EagerBroadcast;
                var delayTask = PreciseTimer.PreciseDelay(sleepTime);
                await Task.WhenAny(delayTask, tcs.Task);
                game.PacketHandler.HackathonBotMadeAction -= EagerBroadcast;
            }
#else
            if (sleepTime > 0)
            {
                await PreciseTimer.PreciseDelay(sleepTime);
            }
#endif
            else
            {
                var broadcastTime = stopwatch.ElapsedMilliseconds;
                var broadcastInterval = game.Settings.BroadcastInterval;
                Console.WriteLine(
                    $"[Tick {this.tick}] Game state broadcast took longer than expected! " +
                    $"({broadcastTime}/{broadcastInterval} ms)");
            }
        }
    }

    private void ResetPlayerGameTickProperties()
    {
        foreach (Player player in game.PlayerManager.Players.Values)
        {
            lock (player)
            {
                player.HasMadeActionThisTick = false;
#if HACKATHON
                player.HasMadeActionToCurrentGameState = false;
#endif
            }
        }
    }

    private List<Task> BroadcastGameStateAsync()
    {
        var tasks = new List<Task>();

        var players = game.PlayerManager.Players.ToDictionary(x => x.Key, x => x.Value.Instance);
        var clients = game.PlayerManager.Players.Keys.Concat(game.SpectatorManager.Spectators.Keys).ToList();

        lock (this.CurrentGameStateId ?? new object())
        {
            this.CurrentGameStateId = Guid.NewGuid().ToString();
        }

        foreach (WebSocket client in clients)
        {
            GameStatePayload packet;
            GameSerializationContext context;

            if (game.SpectatorManager.IsSpectator(client))
            {
                packet = new GameStatePayload(this.tick, [.. players.Values], game.Grid);
                context = new GameSerializationContext.Spectator();
            }
            else
            {
                var player = players[client];
                packet = new GameStatePayload.ForPlayer(this.CurrentGameStateId, this.tick, player, [.. players.Values], game.Grid);
                context = new GameSerializationContext.Player(player);
            }

            var converters = GameStatePayload.GetConverters(context);

            if (client.State == WebSocketState.Open)
            {
                var task = Task.Run(() => game.SendPacketAsync(client, packet, converters));
                tasks.Add(task);
            }
        }

        return tasks;
    }
}
