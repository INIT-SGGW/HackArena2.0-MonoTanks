﻿namespace GameLogic;

/// <summary>
/// Represents a logic updater.
/// </summary>
/// <param name="grid">The grid that will be updated.</param>
public class LogicUpdater(Grid grid)
{
    /// <summary>
    /// Gets or sets the bullet delta time.
    /// </summary>
    public float BulletDeltaTime { get; set; } = 1f;

    /// <summary>
    /// Updates the grid.
    /// </summary>
    public void UpdateGrid()
    {
        grid.UpdateBullets(this.BulletDeltaTime);
        grid.UpdateLasers();
        grid.UpdateMines();
        grid.UpdatePlayersStunEffects();
        grid.RegeneratePlayersBullets();
        grid.UpdateTanksRegenerationProgress();
        grid.UpdatePlayersVisibilityGrids();
        grid.UpdateZones();
        grid.PickUpItems();
    }

    /// <summary>
    /// Resets the player radar usage.
    /// </summary>
    /// <remarks>
    /// This method should be called after creating
    /// a game state packet.
    /// </remarks>
    public void ResetPlayerRadarUsage()
    {
        foreach (Tank tank in grid.Tanks)
        {
            tank.Owner.IsUsingRadar = false;
        }
    }
}
