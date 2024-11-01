﻿using GameLogic;
using Microsoft.Xna.Framework;
using MonoRivUI;

namespace GameClient.Sprites;

/// <summary>
/// Represents a tank sprite.
/// </summary>
internal class Tank : ISprite, IDetectableByRadar
{
    private static readonly ScalableTexture2D.Static StaticTankTexture = new("Images/Game/tank.svg");
    private static readonly ScalableTexture2D.Static StaticTurretTexture = new("Images/Game/turret.svg");

    private readonly GridComponent grid;
    private readonly ScalableTexture2D tankTexture;
    private readonly ScalableTexture2D turretTexture;

    /// <summary>
    /// Initializes a new instance of the <see cref="Tank"/> class.
    /// </summary>
    /// <param name="logic">The tank logic.</param>
    /// <param name="grid">The grid component.</param>
    public Tank(GameLogic.Tank logic, GridComponent grid)
    {
        this.Logic = logic;
        this.grid = grid;

        this.tankTexture = new ScalableTexture2D(StaticTankTexture)
        {
            Color = new Color(logic.Owner.Color),
            RelativeOrigin = new Vector2(0.5f),
            CenterOrigin = true,
            Transform =
            {
                Type = TransformType.Absolute,
                Size = new Point(grid.TileSize, grid.TileSize),
            },
        };

        this.turretTexture = new ScalableTexture2D(StaticTurretTexture)
        {
            RelativeOrigin = new Vector2(0.5f),
            CenterOrigin = true,
            Transform =
            {
                Type = TransformType.Absolute,
                Size = new Point(grid.TileSize, grid.TileSize),
            },
        };
    }

    /// <summary>
    /// Gets the tank logic.
    /// </summary>
    public GameLogic.Tank Logic { get; private set; }

    /// <inheritdoc/>
    float IDetectableByRadar.Opacity
    {
        get => this.tankTexture.Opacity;
        set => this.tankTexture.Opacity = this.turretTexture.Opacity = value;
    }

    /// <inheritdoc/>
    public static void LoadContent()
    {
        StaticTankTexture.Load();
        StaticTurretTexture.Load();
    }

    /// <inheritdoc/>
    IDetectableByRadar IDetectableByRadar.Copy()
    {
        return new Tank(this.Logic, this.grid);
    }

    /// <summary>
    /// Updates the tank logic.
    /// </summary>
    /// <param name="logic">The new tank logic.</param>
    public void UpdateLogic(GameLogic.Tank logic)
    {
        this.Logic = logic;
    }

    /// <inheritdoc/>
    public void Update(GameTime gameTime)
    {
        if (this.Logic.IsDead)
        {
            return;
        }

        this.tankTexture.Rotation = DirectionUtils.ToRadians(this.Logic.Direction);
        this.turretTexture.Rotation = DirectionUtils.ToRadians(this.Logic.Turret.Direction);

        int tileSize = this.grid.TileSize;
        int drawOffset = this.grid.DrawOffset;
        int gridLeft = this.grid.Transform.DestRectangle.Left;
        int gridTop = this.grid.Transform.DestRectangle.Top;

        this.tankTexture.Transform.Location = this.turretTexture.Transform.Location
            = new Point(
                gridLeft + (this.Logic.X * tileSize) + drawOffset,
                gridTop + (this.Logic.Y * tileSize) + drawOffset);

        this.tankTexture.Transform.Size = this.turretTexture.Transform.Size
            = new Point(tileSize, tileSize);

        StaticTankTexture.Transform.Size = new Point(this.grid.TileSize);
        StaticTurretTexture.Transform.Size = new Point(this.grid.TileSize);

        this.tankTexture.Update(gameTime);
        this.turretTexture.Update(gameTime);
    }

    /// <inheritdoc/>
    public void Draw(GameTime gameTime)
    {
        if (this.Logic.IsDead)
        {
            return;
        }

        this.tankTexture.Draw(gameTime);
        this.turretTexture.Draw(gameTime);
    }
}
