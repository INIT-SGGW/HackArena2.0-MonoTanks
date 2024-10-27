using System.Collections.Concurrent;

namespace GameLogic;

/// <summary>
/// Represents a grid.
/// </summary>
/// <param name="dimension">The dimension of the grid.</param>
/// <param name="seed">The seed of the grid.</param>
public class Grid(int dimension, int seed)
{
    private readonly object lasersLock = new();
    private readonly object minesLock = new();

    private readonly ConcurrentQueue<Bullet> queuedBullets = new();
    private readonly Random random = new(seed);
    private readonly MapGenerator generator = new(dimension, seed);

    private List<Zone> zones = [];
    private List<Tank> tanks = [];
    private List<Bullet> bullets = [];
    private List<Laser> lasers = [];
    private List<Mine> mines = [];
    private List<SecondaryItem> items = [];

    private FogOfWarManager fogOfWarManager = new(new bool[0, 0]);

    /// <summary>
    /// Occurs when the state is updating.
    /// </summary>
    public event EventHandler? StateUpdating;

    /// <summary>
    /// Occurs when the state has updated.
    /// </summary>
    public event EventHandler? StateUpdated;

    /// <summary>
    /// Occurs when the dimensions are changing.
    /// </summary>
    public event EventHandler? DimensionsChanging;

    /// <summary>
    /// Occurs when the dimensions have changed.
    /// </summary>
    public event EventHandler? DimensionsChanged;

    /// <summary>
    /// Occurs when a generation warning is raised.
    /// </summary>
    public event EventHandler<string>? GenerationWarning;

    /// <summary>
    /// Gets an empty grid.
    /// </summary>
    public static Grid Empty => new(0, 0);

    /// <summary>
    /// Gets the dimension of the grid.
    /// </summary>
    public int Dim { get; private set; } = dimension;

    /// <summary>
    /// Gets the seed of the grid.
    /// </summary>
    public int Seed { get; private init; } = seed;

    /// <summary>
    /// Gets the wall grid.
    /// </summary>
    public Wall?[,] WallGrid { get; private set; } = new Wall?[dimension, dimension];

    /// <summary>
    /// Gets the zones.
    /// </summary>
    public IEnumerable<Zone> Zones => this.zones;

    /// <summary>
    /// Gets the tanks.
    /// </summary>
    public IEnumerable<Tank> Tanks => this.tanks;

    /// <summary>
    /// Gets the bullets.
    /// </summary>
    public IEnumerable<Bullet> Bullets => this.bullets;

    /// <summary>
    /// Gets the lasers.
    /// </summary>
    public IEnumerable<Laser> Lasers => this.lasers;

    /// <summary>
    /// Gets the mines.
    /// </summary>
    public IEnumerable<Mine> Mines => this.mines;

    /// <summary>
    /// Gets the items.
    /// </summary>
    public IEnumerable<SecondaryItem> Items => this.items;

    /// <summary>
    /// Updates the grid state from a payload.
    /// </summary>
    /// <param name="payload">The payload to update from.</param>
    /// <remarks>
    /// <para>
    /// This method performs the following property settings:
    /// <list type="bullet">
    /// <item><description>
    /// <see cref="Player.Tank"/> is set for each player based on the payload.
    /// </description></item>
    /// <item><description>
    /// <see cref="Tank.Owner"/> is set for each tank based on the payload.
    /// </description></item>
    /// <item><description>
    /// <see cref="Turret.Tank"/> is set for each turret based on its associated tank.
    /// </description></item>
    /// <item><description>
    /// <see cref="Bullet.Shooter"/> is set for each bullet based on the payload.
    /// </description></item>
    /// </list>
    /// </para>
    /// <para>
    /// This method raises the <see cref="StateUpdating"/> event before updating the state,
    /// and the <see cref="StateUpdated"/> event after updating the state.
    /// </para>
    /// <para>
    /// If the dimensions of the grid have changed, this method raises the
    /// <see cref="DimensionsChanging"/>  event before updating the dimensions,
    /// and the <see cref="DimensionsChanged"/> event after updating the dimensions.
    /// </para>
    /// </remarks>
    public void UpdateFromGameStatePayload(Networking.GameStatePayload payload)
    {
        this.StateUpdating?.Invoke(this, EventArgs.Empty);

        var map = payload.Map;
        var tiles = map.Tiles;

        // Update the dimensions of the grid.
        var newDim = tiles.WallGrid.GetLength(0);
        if (newDim != this.Dim)
        {
            this.DimensionsChanging?.Invoke(this, EventArgs.Empty);
            this.Dim = newDim;
            this.WallGrid = new Wall?[this.Dim, this.Dim];
            this.DimensionsChanged?.Invoke(this, EventArgs.Empty);
        }

        // Update the walls.
        this.WallGrid = tiles.WallGrid;

        // Update the tanks.
        this.tanks = [.. tiles.Tanks];

        // Set the tanks' owners, owners' tanks and turrets' tanks.
        foreach (Tank tank in this.tanks)
        {
            var owner = payload.Players.First(p => p.Id == tank.OwnerId);

            tank.Owner = owner;
            tank.Turret.Tank = tank;
            owner.Tank = tank;
        }

        // Update the bullets.
        this.bullets = tiles.Bullets;

        // Set the bullets' shooters.
        foreach (Bullet bullet in this.bullets)
        {
            if (bullet.ShooterId is null)
            {
                continue;
            }

            var shooter = payload.Players.First(p => p.Id == bullet.ShooterId);
            bullet.Shooter = shooter;
        }

        // Update the lasers.
        this.lasers = tiles.Lasers;

        // Update the lasers' shooters.
        foreach (Laser laser in this.lasers)
        {
            if (laser.ShooterId is null)
            {
                continue;
            }

            var shooter = payload.Players.First(p => p.Id == laser.ShooterId);
            laser.Shooter = shooter;
        }

        // Update the mines.
        this.mines = tiles.Mines;

        // Update the mines' layers.
        foreach (Mine mine in this.mines)
        {
            if (mine.LayerId is null)
            {
                continue;
            }

            var layer = payload.Players.First(p => p.Id == mine.LayerId);
            mine.Layer = layer;
        }

        // Update the zones.
        this.zones = map.Zones;

        Player player;
        foreach (Zone zone in this.zones)
        {
            switch (zone.Status)
            {
                case ZoneStatus.BeingCaptured beingCaptured:
                    player = payload.Players.First(p => p.Id == beingCaptured.PlayerId);
                    beingCaptured.Player = player;
                    break;

                case ZoneStatus.Captured captured:
                    player = payload.Players.First(p => p.Id == captured.PlayerId);
                    captured.Player = player;
                    break;

                case ZoneStatus.BeingContested beingContested:
                    if (beingContested.CapturedById is not null)
                    {
                        player = payload.Players.First(p => p.Id == beingContested.CapturedById);
                        beingContested.CapturedBy = player;
                    }

                    break;

                case ZoneStatus.BeingRetaken beingRetaken:
                    player = payload.Players.First(p => p.Id == beingRetaken.CapturedById);
                    var player2 = payload.Players.First(p => p.Id == beingRetaken.RetakenById);
                    beingRetaken.CapturedBy = player;
                    beingRetaken.RetakenBy = player2;
                    break;
            }
        }

        // Update the items.
        this.items = [.. tiles.Items];

        this.StateUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Generates the map.
    /// </summary>
    public void GenerateMap()
    {
        this.generator.GenerationWarning += (s, e) => this.GenerationWarning?.Invoke(s, e);

        this.zones = this.generator.GenerateZones();
        var walls = this.generator.GenerateWalls(this.zones);

        this.fogOfWarManager = new FogOfWarManager(walls);

        for (int x = 0; x < this.Dim; x++)
        {
            for (int y = 0; y < this.Dim; y++)
            {
                this.WallGrid[x, y] = walls[x, y] ? new Wall() { X = x, Y = y } : null;
            }
        }

        this.generator.GenerationWarning -= (s, e) => this.GenerationWarning?.Invoke(s, e);
    }

    /// <summary>
    /// Generates a tank.
    /// </summary>
    /// <param name="owner">The owner of the tank.</param>
    /// <returns>The generated tank.</returns>
    public Tank GenerateTank(Player owner)
    {
        var (x, y) = this.GetTankSpawnPostion();

        var tankDirection = EnumUtils.Random<Direction>(this.random);
        var turretDirection = EnumUtils.Random<Direction>(this.random);

        var tank = new Tank(x, y, tankDirection, turretDirection, owner);

        owner.Tank = tank;
        this.tanks.Add(tank);

        tank.Turret.BulletShot += this.queuedBullets.Enqueue;

        tank.Turret.LaserUsed += (lasers) =>
        {
            lock (this.lasersLock)
            {
                this.lasers.AddRange(lasers);
            }
        };

        tank.MineDropped += (sender, mine) =>
        {
            lock (this.minesLock)
            {
                this.mines.Add(mine);
            }
        };

        owner.TankRegenerated += (s, e) =>
        {
            var (x, y) = this.GetTankSpawnPostion();
            tank.SetPosition(x, y);
        };

        tank.Dying += (s, e) =>
        {
            if (tank.SecondaryItemType is { } itemType)
            {
                var point = this.GetTileToDropItem(tank.X, tank.Y);

                if (point is { } p)
                {
                    var item = new SecondaryItem(p.X, p.Y, itemType);
                    this.items.Add(item);
                }
            }
        };

        return tank;
    }

    /// <summary>
    /// Removes a tank.
    /// </summary>
    /// <param name="owner">The owner of the tank to remove.</param>
    /// <returns>The removed tank if found; otherwise, <c>null</c>.</returns>
    public Tank? RemoveTank(Player owner)
    {
        var tank = this.tanks.FirstOrDefault(t => t.Owner == owner);

        if (tank is not null)
        {
            _ = this.tanks.Remove(tank);
        }

        foreach (Zone zone in this.zones)
        {
            zone.HandlePlayerRemoved(owner, this.tanks.Select(x => x.Owner));
        }

        _ = this.bullets.RemoveAll(b => b.Shooter == owner);

        lock (this.lasersLock)
        {
            _ = this.lasers.RemoveAll(l => l.Shooter == owner);
        }

        lock (this.minesLock)
        {
            _ = this.mines.RemoveAll(m => m.Layer == owner);
        }

        return tank;
    }

    /// <summary>
    /// Tries to move a tank.
    /// </summary>
    /// <param name="tank">The tank to move.</param>
    /// <param name="movement">The movement direction.</param>
    /// <remarks>
    /// The movement is ignored if the tank is stunned by the
    /// <see cref="StunBlockEffect.Movement"/> effect.
    /// </remarks>
    public void TryMoveTank(Tank tank, MovementDirection movement)
    {
        if (tank.IsBlockedByStun(StunBlockEffect.Movement))
        {
            return;
        }

        var (dx, dy) = DirectionUtils.Normal(tank.Direction);
        int step = -((int)movement * 2) + 1;
        int x = tank.X + (dx * step);
        int y = tank.Y + (dy * step);

        if (this.IsCellWithinBounds(x, y)
            && !this.GetCellObjects(x, y).Any(x => x is Wall or Tank))
        {
            tank.SetPosition(x, y);
        }
    }

    /// <summary>
    /// Generates a new item on the map.
    /// </summary>
    /// <returns>
    /// The generated item if successful;
    /// otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// If the number of items on the map is greater than
    /// twice the dimension of the grid, no item is generated.
    /// </remarks>
    public SecondaryItem? GenerateNewItemOnMap()
    {
        if (this.items.Count > 2 * this.Dim)
        {
            return null;
        }

        var nullWeight = 99.5f;
        var itemWeights = new Dictionary<SecondaryItemType, double>
        {
            { SecondaryItemType.Laser, 0.09 },
            { SecondaryItemType.DoubleBullet, 0.9 },
            { SecondaryItemType.Radar, 0.3 },
            { SecondaryItemType.Mine, 0.5 },
        };

        double totalWeight = itemWeights.Values.Sum() + nullWeight;
        double randomValue = this.random.NextDouble() * totalWeight;

        var selectedItemType = GetRandomItemByWeight(itemWeights, randomValue);
        if (selectedItemType == null)
        {
            return null;
        }

        int x, y;
        int attemptsLeft = 200;
        do
        {
            x = this.random.Next(this.Dim);
            y = this.random.Next(this.Dim);
        } while (attemptsLeft-- > 0
                && (this.GetCellObjects(x, y).Any()
                    || this.IsVisibleByTank(x, y)
                    || this.zones.Any(z => z.ContainsPoint(x, y))));

        SecondaryItem? item = null;
        if (attemptsLeft > 0)
        {
            item = new SecondaryItem(x, y, selectedItemType.Value);
            this.items.Add(item);
        }

        return item;
    }

    /// <summary>
    /// Updates the tanks' regeneration progress.
    /// </summary>
    /// <remarks>
    /// This method regenerates the dead tanks
    /// by calling the <see cref="Player.UpdateRegenerationProgress"/> method.
    /// </remarks>
    internal void UpdateTanksRegenerationProgress()
    {
        this.Tanks
            .Select(x => x.Owner)
            .ToList()
            .ForEach(x => x.UpdateRegenerationProgress());
    }

    /// <summary>
    /// Updates the bullets.
    /// </summary>
    /// <param name="deltaTime">The time elapsed since the last update.</param>
    internal void UpdateBullets(float deltaTime)
    {
        Dictionary<Bullet, List<(int X, int Y)>> trajectories = [];

        foreach (Bullet bullet in this.bullets.ToList())
        {
            int previousX = bullet.X;
            int previousY = bullet.Y;
            bullet.UpdatePosition(deltaTime);
            trajectories[bullet] = bullet.CalculateTrajectory(previousX, previousY);
        }

        List<Bullet> destroyedBullets = [];
        foreach (Bullet bullet in this.bullets.ToList())
        {
            if (destroyedBullets.Contains(bullet))
            {
                continue;
            }

            Collision? collision = CollisionDetector.CheckBulletCollision(bullet, this, trajectories);

            if (collision is not null)
            {
                destroyedBullets.AddRange(this.HandleBulletCollision(bullet, collision));
            }
        }

        var newBullets = new List<Bullet>();
        while (!this.queuedBullets.IsEmpty)
        {
            if (!this.queuedBullets.TryDequeue(out var bullet) || bullet is null)
            {
                continue;
            }

            newBullets.Add(bullet);
            this.bullets.Add(bullet);
            trajectories.Add(bullet, [(bullet.X, bullet.Y)]);
        }

        foreach (Bullet newBullet in this.bullets.ToList())
        {
            Collision? collision = CollisionDetector.CheckBulletCollision(newBullet, this, trajectories);

            if (collision is not null)
            {
                _ = this.HandleBulletCollision(newBullet, collision);
            }
        }
    }

    /// <summary>
    /// Updates the lasers.
    /// </summary>
    internal void UpdateLasers()
    {
        lock (this.lasersLock)
        {
            foreach (Laser laser in this.lasers.ToList())
            {
                laser.DecreaseRemainingTicks();
                if (laser.RemainingTicks <= 0)
                {
                    _ = this.lasers.Remove(laser);
                }

                var tanksInLaser = this.tanks.Where(t => t.X == laser.X && t.Y == laser.Y);
                foreach (Tank tank in tanksInLaser)
                {
                    var damageTaken = tank.TakeDamage(laser.Damage!.Value, laser.Shooter);
                    laser.Shooter!.Score += damageTaken;
                }

                lock (this.minesLock)
                {
                    var minesInLaser = this.mines.Where(m => m.X == laser.X && m.Y == laser.Y);
                    foreach (Mine mine in minesInLaser)
                    {
                        if (!mine.IsExploded)
                        {
                            mine.Explode(null);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Updates the mines.
    /// </summary>
    internal void UpdateMines()
    {
        lock (this.minesLock)
        {
            foreach (Mine mine in this.mines.ToList())
            {
                // Remove mine outside the grid.
                if (!this.IsCellWithinBounds(mine.X, mine.Y))
                {
                    _ = this.mines.Remove(mine);
                    continue;
                }

                // Remove if the mine is on a wall.
                if (this.WallGrid[mine.X, mine.Y] is not null)
                {
                    _ = this.mines.Remove(mine);
                    continue;
                }

                mine.DecreaseExplosionTicks();

                // Remove if the mine is fully exploded.
                if (mine.IsFullyExploded)
                {
                    _ = this.mines.Remove(mine);
                    continue;
                }

                // Explode the mine if a tank is on it.
                if (!mine.IsExploded)
                {
                    var tankInMine = this.tanks.FirstOrDefault(t => t.X == mine.X && t.Y == mine.Y);
                    if (tankInMine is not null)
                    {
                        mine.Explode(tankInMine);
                    }
                }
            }

            // Remove duplicate mines.
            // Reverse the list to remove the last mine in case of duplicates.
            var mines = this.mines.ToList();
            mines.Reverse();
            foreach (Mine mine in mines)
            {
                if (this.mines.Any(m => m.X == mine.X && m.Y == mine.Y && m != mine))
                {
                    _ = this.mines.Remove(mine);
                }
            }
        }
    }

    /// <summary>
    /// Regenerates the players' bullets.
    /// </summary>
    internal void RegeneratePlayersBullets()
    {
        foreach (Tank tank in this.tanks)
        {
            tank.Turret.RegenerateBullets();
        }
    }

    /// <summary>
    /// Updates the players' visibility grids.
    /// </summary>
    internal void UpdatePlayersVisibilityGrids()
    {
        foreach (Tank tank in this.tanks)
        {
            if (tank.IsDead)
            {
                continue;
            }

            tank.Owner.CalculateVisibilityGrid(this.fogOfWarManager);
        }
    }

    /// <summary>
    /// Updates the zones.
    /// </summary>
    internal void UpdateZones()
    {
        foreach (Zone zone in this.zones)
        {
            zone.UpdateCapturingStatus(this.tanks);
        }
    }

    /// <summary>
    /// Picks up the items that are on the same cell as the tanks
    /// and sets the tanks' secondary item type.
    /// </summary>
    internal void PickUpItems()
    {
        foreach (Tank tank in this.tanks)
        {
            if (tank.SecondaryItemType is not null)
            {
                continue;
            }

            SecondaryItem? item = this.items.FirstOrDefault(i => i.X == tank.X && i.Y == tank.Y);

            if (item is null)
            {
                continue;
            }

            tank.SecondaryItemType = item.Type;
            _ = this.items.Remove(item);
        }
    }

    /// <summary>
    /// Updates the players' stun effects.
    /// </summary>
    internal void UpdatePlayersStunEffects()
    {
        foreach (Tank tank in this.tanks)
        {
            tank.UpdateStunables();
        }
    }

    /// <summary>
    /// Converts the grid to a map payload.
    /// </summary>
    /// <param name="player">The player to convert the grid for.</param>
    /// <returns>The map payload for the grid.</returns>
    internal MapPayload ToMapPayload(Player? player)
    {
        var visibility = player is not null
            ? new VisibilityPayload(player!.VisibilityGrid!)
            : null;

        var tiles = new TilesPayload(
            this.WallGrid,
            this.tanks,
            this.bullets,
            this.lasers,
            this.mines,
            this.items);

        return new MapPayload(visibility, tiles, this.zones);
    }

    private static SecondaryItemType? GetRandomItemByWeight(
        Dictionary<SecondaryItemType, double> itemWeights,
        double randomValue)
    {
        double cumulativeWeight = 0.0;

        foreach (var itemWeight in itemWeights)
        {
            cumulativeWeight += itemWeight.Value;
            if (randomValue <= cumulativeWeight)
            {
                return itemWeight.Key;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the tile coordinates to drop an item.
    /// </summary>
    /// <param name="x">The x-coordinate of the tank.</param>
    /// <param name="y">The y-coordinate of the tank.</param>
    /// <returns>
    /// The X and Y coordinates of the tile to drop the item
    /// or <see langword="null"/> if no tile is found.
    /// </returns>
    private (int X, int Y)? GetTileToDropItem(int x, int y)
    {
        var visited = new bool[this.Dim, this.Dim];
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((x, y));

        while (queue.Count > 0)
        {
            var (currentX, currentY) = queue.Dequeue();

            if (!this.IsCellWithinBounds(currentX, currentY)
                || visited[currentX, currentY]
                || this.WallGrid[currentX, currentY] is not null)
            {
                continue;
            }

            if (!this.GetCellObjects(currentX, currentY).Any(x => x is SecondaryItem))
            {
                return (currentX, currentY);
            }

            visited[currentX, currentY] = true;

            queue.Enqueue((currentX + 1, currentY));
            queue.Enqueue((currentX - 1, currentY));
            queue.Enqueue((currentX, currentY + 1));
            queue.Enqueue((currentX, currentY - 1));
        }

        return null;
    }

    private (int X, int Y) GetTankSpawnPostion()
    {
        int x, y, attempts = 0;
        do
        {
            x = this.random.Next(this.Dim);
            y = this.random.Next(this.Dim);
        }
        while ((this.GetCellObjects(x, y).Any() || (this.zones.Any(z => z.ContainsPoint(x, y)) && attempts++ < 2000))
            || (this.IsVisibleByTank(x, y) && attempts < 1000));

        return (x, y);
    }

    private bool IsVisibleByTank(int x, int y)
    {
        return this.tanks.Any(t => t.Owner.VisibilityGrid is not null && t.Owner.VisibilityGrid[x, y]);
    }

    private IEnumerable<object> GetCellObjects(int x, int y)
    {
        Wall? wall = this.WallGrid[x, y];
        if (wall is not null)
        {
            yield return wall;
        }

        Tank? tank = this.tanks.FirstOrDefault(t => t.X == x && t.Y == y);
        if (tank is not null)
        {
            yield return tank;
        }

        Bullet? bullet = this.bullets.FirstOrDefault(b => b.X == x && b.Y == y);
        if (bullet is not null)
        {
            yield return bullet;
        }

        Laser? laser = this.lasers.FirstOrDefault(l => l.X == x && l.Y == y);
        if (laser is not null)
        {
            yield return laser;
        }

        Mine? mine = this.mines.FirstOrDefault(m => m.X == x && m.Y == y);
        if (mine is not null)
        {
            yield return mine;
        }

        IEnumerable<SecondaryItem> items = this.items.Where(i => i.X == x && i.Y == y);
        foreach (SecondaryItem item in items)
        {
            yield return item;
        }
    }

    private bool IsCellWithinBounds(int x, int y)
    {
        return x >= 0 && x < this.Dim && y >= 0 && y < this.Dim;
    }

    private List<Bullet> HandleBulletCollision(Bullet bullet, Collision collision)
    {
        var destroyedBullets = new List<Bullet>();
        _ = this.bullets.Remove(bullet);

        switch (collision)
        {
            case TankCollision tankCollision:
                destroyedBullets.Add(bullet);
                var damageTaken = tankCollision.Tank.TakeDamage(bullet.Damage!.Value, bullet.Shooter);
                bullet.Shooter!.Score += damageTaken / 2;
                break;

            case BulletCollision bulletCollision:
                _ = this.bullets.Remove(bulletCollision.Bullet);
                destroyedBullets.Add(bulletCollision.Bullet);
                destroyedBullets.Add(bullet);

                if (!(bullet is DoubleBullet ^ bulletCollision.Bullet is DoubleBullet))
                {
                    break;
                }

                var bulletToDowngrade = bullet is DoubleBullet
                    ? bullet
                    : bulletCollision.Bullet;

                var newBullet = new Bullet(
                    bulletToDowngrade.X,
                    bulletToDowngrade.Y,
                    bulletToDowngrade.Direction,
                    bulletToDowngrade.Speed,
                    bulletToDowngrade.Damage!.Value / 2,
                    bulletToDowngrade.Shooter!);

                this.bullets.Add(newBullet);

                break;
        }

        return destroyedBullets;
    }

    /// <summary>
    /// Represents a map payload for the grid.
    /// </summary>
    /// <param name="Visibility">The visibility payload for the grid.</param>
    /// <param name="Tiles">The tiles payload for the grid.</param>
    /// <param name="Zones">The zones of the grid.</param>
    internal record class MapPayload(VisibilityPayload? Visibility, TilesPayload Tiles, List<Zone> Zones);

    /// <summary>
    /// Represents a tiles payload for the grid.
    /// </summary>
    /// <param name="WallGrid">The wall grid of the grid.</param>
    /// <param name="Tanks">The tanks of the grid.</param>
    /// <param name="Bullets">The bullets of the grid.</param>
    /// <param name="Lasers">The lasers on the grid.</param>
    /// <param name="Mines">The mines on the grid.</param>
    /// <param name="Items">The items on the grid.</param>
    internal record class TilesPayload(
        Wall?[,] WallGrid,
        List<Tank> Tanks,
        List<Bullet> Bullets,
        List<Laser> Lasers,
        List<Mine> Mines,
        List<SecondaryItem> Items);

    /// <summary>
    /// Represents a visibility payload for the grid.
    /// </summary>
    /// <param name="VisibilityGrid">The visibility grid of the grid.</param>
    internal record class VisibilityPayload(bool[,] VisibilityGrid);
}
