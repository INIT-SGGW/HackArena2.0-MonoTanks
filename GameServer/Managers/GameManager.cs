using System.Diagnostics;
using GameLogic;
using GameLogic.Networking;
using Newtonsoft.Json;
using Serilog.Core;

namespace GameServer;

/// <summary>
/// Represents the game manager.
/// </summary>
/// <param name="game">The game instance.</param>
/// <param name="log">The logger.</param>
internal class GameManager(GameInstance game, Logger log)
{
#if HACKATHON
    // Used to shuffle the bot actions.
    private readonly Random random = new(game.Settings.Seed);
#endif

    private readonly LogicUpdater logicUpdater = new(game.Grid);
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
    /// Gets a value indicating whether the game is in progress.
    /// </summary>
    public bool IsInProgess
    {
        get
        {
            lock (this)
            {
                return this.Status is GameStatus.Starting or GameStatus.Running;
            }
        }
    }

    /// <summary>
    /// Starts the game.
    /// </summary>
    public async void StartGame()
    {
        lock (this)
        {
            if (this.Status is GameStatus.Starting or GameStatus.Running)
            {
                return;
            }

            this.Status = GameStatus.Starting;
        }

        log.Information("Game is starting...");

        game.ReplayManager?.SaveLobbyData();
        await game.LobbyManager.SendGameStartingToAll();

        if (!game.Settings.SandboxMode)
        {
            log.Debug("Waiting for all players to be ready to receive the game state...");

            while (game.Players.Any(x => !x.IsReadyToReceiveGameState))
            {
                await Task.Delay(100);
            }

            log.Debug("All players are ready to receive the game state.");
        }

        _ = game.LobbyManager.SendGameStartedToAll();

        lock (this)
        {
            this.Status = GameStatus.Running;
        }

        log.Information("Game has started!");

        _ = Task.Run(this.StartBroadcastingAsync);
    }

    /// <summary>
    /// Ends the game.
    /// </summary>
    public async void EndGame()
    {
        this.Status = GameStatus.Ended;

        log.Information("Game has ended.");

        game.ReplayManager?.SaveGameEnd();
        game.ReplayManager?.SaveReplay();

#if HACKATHON
        game.ReplayManager?.SaveResults();
#endif

        var tasks = new List<Task>();
        var cancellationTokenSource = new CancellationTokenSource(10_000);

        log.Verbose("Sending game end to all clients...");

        foreach (var connection in game.Connections)
        {
            var payload = game.PayloadHelper.GetGameEndPayload(out var converters);
            var packet = new ResponsePacket(payload, log, converters);
            var task = packet.SendAsync(connection, cancellationTokenSource.Token, this);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        log.Verbose("Game end sent to all clients.");
        log.Verbose("Closing all connections...");

        tasks.Clear();
        foreach (var connection in game.Connections)
        {
            var task = connection.CloseAsync(description: "Game ended");
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        log.Verbose("All connections closed.");
        log.Debug("Exiting the game server...");
        Environment.Exit(0);
    }

    private async Task StartBroadcastingAsync()
    {
        log.Debug("Starting game broadcasting...");

        // Give some time for the clients to load the game
        await PreciseTimer.PreciseDelay(game.Settings.BroadcastInterval);

        var stopwatch = new Stopwatch();

        while (this.Status is GameStatus.Running)
        {
            if (this.tick++ >= game.Settings.Ticks)
            {
                log.Verbose("Game has reached the maximum number of ticks.");
                this.EndGame();
                break;
            }

            stopwatch.Restart();

            lock (this)
            {
#if HACKATHON
                log.Verbose("Processing hackathon bot actions...");

                var actionList = game.PacketHandler.HackathonBotActions.ToList();
                actionList.Sort((x, y) => x.Key.Instance.Nickname.CompareTo(y.Key.Instance.Nickname));
                Action[] actions = actionList.Select(x => x.Value).ToArray();
                this.random.Shuffle(actions);

                foreach (Action action in actions)
                {
                    action.Invoke();
                }

                game.PacketHandler.HackathonBotActions.Clear();

                log.Verbose("Hackathon bot actions processed.");
#endif

                try
                {
                    log.Verbose("Updating game logic...");
                    this.logicUpdater.UpdateGrid();
                    log.Verbose("Game logic updated.");
                }
                catch (Exception ex)
                {
                    log.Error(ex, "An error occurred while updating the game logic.");
                }

                // Spawn new items on the map
                log.Verbose("Generating new item on the map...");

                SecondaryItem? item = null;
                if (game.Settings.SandboxMode)
                {
                    // In sandbox mode, spawn new items only if there are players in the game
                    int playerCount = game.Players.Count();
                    if (playerCount > 0)
                    {
                        item = game.Grid.GenerateNewItemOnMap();
                    }
                }
                else
                {
                    item = game.Grid.GenerateNewItemOnMap();
                }

                if (item is not null)
                {
                    log.Verbose("New item generated on the map: {Item}", item);
                }

                // Broadcast the game state
                lock (this.CurrentGameStateId ?? new object())
                {
                    this.CurrentGameStateId = Guid.NewGuid().ToString();
                    log.Verbose("New game state id generated (tick: {tick}): {GameStateId}", this.tick, this.CurrentGameStateId);
                }

                log.Verbose("Resetting player game tick properties...");
                foreach (PlayerConnection player in game.Players)
                {
                    player.ResetGameTickProperties();
                }

                log.Verbose("Broadcasting game state...");
                this.BroadcastGameStateAsync();

                log.Verbose("Resetting player radar usage...");
                this.logicUpdater.ResetPlayerRadarUsage();
            }

            game.ReplayManager?.AddGameState(this.tick, this.CurrentGameStateId);

            log.Verbose("Game state broadcast completed.");
            stopwatch.Stop();

            var sleepTime = (int)(game.Settings.BroadcastInterval - stopwatch.ElapsedMilliseconds);
            log.Verbose("Sleep time: {SleepTime} ms", sleepTime);

#if HACKATHON
            var tcs = new TaskCompletionSource<bool>();

            void EagerBroadcast(object? sender, PlayerConnection player)
            {
                lock (player)
                {
                    if (this.tick > 5 // Warm-up period
                        && game.Settings.EagerBroadcast
                        && game.Players.All(x => x.IsHackathonBot && x.HasMadeActionToCurrentGameState))
                    {
                        _ = tcs.TrySetResult(true);
                        log.Verbose("Eager broadcast triggered.");
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
                log.Warning(
                    "Game state broadcast took longer than expected! " +
                    "Tick: {Tick}, {BroadcastTime}/{BroadcastInterval} ms",
                    this.tick,
                    broadcastTime,
                    broadcastInterval);
            }
        }
    }

    private void BroadcastGameStateAsync()
    {
        var cancellationTokenSource = new CancellationTokenSource(game.Settings.BroadcastInterval * 100);

        foreach (Connection connection in game.Connections)
        {
            if (!connection.IsReadyToReceiveGameState)
            {
                continue;
            }

            log.Verbose("Sending game state ({tick}) to {Connection}.", this.tick, connection);

            GameStatePayload payload = game.PayloadHelper.GetGameStatePayload(
                connection,
                this.tick,
                this.CurrentGameStateId!,
                out var converters);

            var packet = new ResponsePacket(payload, log, converters);
            _ = packet.SendAsync(connection, cancellationTokenSource.Token, this);
        }
    }
}
