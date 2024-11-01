﻿using System.Diagnostics;

namespace GameLogic;

/// <summary>
/// Represents a tank.
/// </summary>
public class Tank : IEquatable<Tank>
{
    private const int MineDamage = 50;
    private readonly Dictionary<IStunEffect, int> remainingStuns = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="Tank"/> class.
    /// </summary>
    /// <param name="x">The x coordinate of the tank.</param>
    /// <param name="y">The y coordinate of the tank.</param>
    /// <param name="direction">The direction of the tank.</param>
    /// <param name="turretDirection">The direction of the turret.</param>
    /// <param name="owner">The owner of the tank.</param>
    internal Tank(int x, int y, Direction direction, Direction turretDirection, Player owner)
        : this(x, y, owner.Id, direction)
    {
        this.Owner = owner;
        this.Health = 100;
        this.Turret = new Turret(this, turretDirection);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Tank"/> class.
    /// </summary>
    /// <param name="x">The x coordinate of the tank.</param>
    /// <param name="y">The y coordinate of the tank.</param>
    /// <param name="ownerId">The owner ID of the tank.</param>
    /// <param name="direction">The direction of the tank.</param>
    /// <param name="turret">The turret of the tank.</param>
    /// <remarks>
    /// <para>
    /// This constructor should be used when creating a tank
    /// from player perspective, because they shouldn't know
    /// the <see cref="Health"/> and <see cref="SecondaryItem"/>
    /// (these will be set to <see langword="null"/>).
    /// </para>
    /// <para>
    /// The <see cref="Owner"/> property is set to <see langword="null"/>.
    /// See its documentation for more information.
    /// </para>
    /// </remarks>
    internal Tank(int x, int y, string ownerId, Direction direction, Turret turret)
        : this(x, y, ownerId, direction)
    {
        this.Turret = turret;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Tank"/> class.
    /// </summary>
    /// <param name="x">The x coordinate of the tank.</param>
    /// <param name="y">The y coordinate of the tank.</param>
    /// <param name="ownerId">The owner ID of the tank.</param>
    /// <param name="health">The health of the tank.</param>
    /// <param name="direction">The direction of the tank.</param>
    /// <param name="turret">The turret of the tank.</param>
    /// <param name="secondaryItemType">The secondary item type of the tank.</param>
    /// <remarks>
    /// <para>
    /// This constructor should be used when creating a tank
    /// from the server or spectator perspective, because they know
    /// all the properties of the tank.
    /// </para>
    /// <para>
    /// The <see cref="Owner"/> property is set to <see langword="null"/>.
    /// See its documentation for more information.
    /// </para>
    /// </remarks>
    internal Tank(
        int x,
        int y,
        string ownerId,
        int health,
        Direction direction,
        Turret turret,
        SecondaryItemType? secondaryItemType)
        : this(x, y, ownerId, direction, turret)
    {
        this.Health = health;
        this.SecondaryItemType = secondaryItemType;
    }

    private Tank(int x, int y, string ownerId, Direction direction)
    {
        this.X = x;
        this.Y = y;
        this.Owner = null!;
        this.OwnerId = ownerId;
        this.Direction = direction;
        this.Turret = null!;  // Set in the other constructors
    }

    /// <summary>
    /// Occurs when the tank is about to die.
    /// </summary>
    internal event EventHandler? Dying;

    /// <summary>
    /// Occurs when the tank dies.
    /// </summary>
    internal event EventHandler? Died;

    /// <summary>
    /// Occurs when the mine has been dropped;
    /// </summary>
    internal event EventHandler<Mine>? MineDropped;

    /// <summary>
    /// Gets the x coordinate of the tank.
    /// </summary>
    public int X { get; private set; }

    /// <summary>
    /// Gets the y coordinate of the tank.
    /// </summary>
    public int Y { get; private set; }

    /// <summary>
    /// Gets the health of the tank.
    /// </summary>
    public int? Health { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the tank is dead.
    /// </summary>
    public bool IsDead => this.Health <= 0;

    /// <summary>
    /// Gets the owner of the tank.
    /// </summary>
    /// <remarks>
    /// The setter is internal because the owner is set
    /// in the <see cref="Grid.UpdateFromGameStatePayload"/> method.
    /// </remarks>
    public Player Owner { get; internal set; }

    /// <summary>
    /// Gets the direction of the tank.
    /// </summary>
    public Direction Direction { get; private set; }

    /// <summary>
    /// Gets the turret of the tank.
    /// </summary>
    public Turret Turret { get; private set; }

#if DEBUG
    /// <summary>
    /// Gets or sets the secondary item of the tank.
    /// </summary>
    public SecondaryItemType? SecondaryItemType { get; set; }
#else
    /// <summary>
    /// Gets the secondary item of the tank.
    /// </summary>
    public SecondaryItemType? SecondaryItemType { get; internal set; }
#endif

    /// <summary>
    /// Gets the previous x coordinate of the tank.
    /// </summary>
    internal int? PreviousX { get; private set; }

    /// <summary>
    /// Gets the previous y coordinate of the tank.
    /// </summary>
    internal int? PreviousY { get; private set; }

    /// <summary>
    /// Gets the owner ID of the tank.
    /// </summary>
    internal string OwnerId { get; private set; }

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>
    /// <see langword="true"/> if the specified object is equal to the current object;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public override bool Equals(object? obj)
    {
        return this.Equals(obj as Tank);
    }

    /// <inheritdoc cref="Equals(object)"/>
    public bool Equals(Tank? other)
    {
        return this.OwnerId == other?.OwnerId;
    }

    /// <summary>
    /// Gets the hash code of the tank.
    /// </summary>
    /// <returns>The hash code of the tank.</returns>
    public override int GetHashCode()
    {
        return this.Owner.GetHashCode();
    }

    /// <summary>
    /// Rotates the tank.
    /// </summary>
    /// <param name="rotation">The rotation to apply.</param>
    /// <remarks>
    /// The rotation is ignored if the tank is stunned by the
    /// <see cref="StunBlockEffect.TankRotation"/> effect.
    /// </remarks>
    public void Rotate(Rotation rotation)
    {
        if (this.IsBlockedByStun(StunBlockEffect.TankRotation))
        {
            return;
        }

        this.Direction = rotation switch
        {
            Rotation.Left => EnumUtils.Previous(this.Direction),
            Rotation.Right => EnumUtils.Next(this.Direction),
            _ => throw new NotImplementedException(),
        };
    }

    /// <summary>
    /// Tries to use the radar.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the radar was used;
    /// otherwise, <see langword="false"/>.
    /// <remarks>
    /// The radar is used only if the tank
    /// has a radar and is not stunned with the
    /// <see cref="StunBlockEffect.AbilityUse"/> block effect.
    public bool TryUseRadar()
    {
        if (this.IsBlockedByStun(StunBlockEffect.AbilityUse))
        {
            return false;
        }

        if (this.SecondaryItemType is not GameLogic.SecondaryItemType.Radar)
        {
            return false;
        }

        this.Owner.IsUsingRadar = true;
        this.SecondaryItemType = null;
        return true;
    }

    /// <summary>
    /// Tries to drop a mine.
    /// </summary>
    /// <returns>
    /// The mine that was dropped, or <see langword="null"/>
    /// if the tank is stunned with the <see cref="StunBlockEffect.AbilityUse"/>
    /// block effect or the tank doesn't have a mine.
    /// </returns>
    public Mine? TryDropMine()
    {
        if (this.IsBlockedByStun(StunBlockEffect.AbilityUse))
        {
            return null;
        }

        if (this.SecondaryItemType is not GameLogic.SecondaryItemType.Mine)
        {
            return null;
        }

        var (nx, ny) = DirectionUtils.Normal(this.Direction);
        var mine = new Mine(
            this.X - nx,
            this.Y - ny,
            MineDamage,
            this.Owner);

        this.SecondaryItemType = null;
        this.MineDropped?.Invoke(this, mine);

        return mine;
    }

    /// <summary>
    /// Reduces the health of the tank.
    /// </summary>
    /// <param name="damage">The amount of damage to take.</param>
    /// <param name="damager">The player that made the damage.</param>
    /// <returns>The amount of damage taken.</returns>
    /// <remarks>
    /// If the damager is provided, the kills of the damager
    /// are increased by one if the tank is killed.
    /// </remarks>
    internal int TakeDamage(int damage, Player? damager = null)
    {
        return this.TakeDamage(damage, damager, out _);
    }

    /// <summary>
    /// Reduces the health of the tank.
    /// </summary>
    /// <param name="damage">The amount of damage to take.</param>
    /// <param name="damager">The player that made the damage.</param>
    /// <param name="killed">A value indicating whether the tank was killed.</param>
    /// <returns>The amount of damage taken.</returns>
    /// <remarks>
    /// If the damager is provided, the kills of the damager
    /// are increased by one if the tank is killed.
    /// </remarks>
    internal int TakeDamage(int damage, Player? damager, out bool killed)
    {
        Debug.Assert(damage >= 0, "Damage cannot be negative.");

        var damageTaken = Math.Min(this.Health!.Value, damage);

        this.Health -= damage;
        killed = false;

        if (this.Health <= 0)
        {
            this.Dying?.Invoke(this, EventArgs.Empty);

            this.SetPosition(-1, -1);
            this.Health = 0;
            this.SecondaryItemType = null;

            killed = true;
            this.Died?.Invoke(this, EventArgs.Empty);

            if (damager is not null)
            {
                damager.Kills++;

                if (!damager.IsDead)
                {
                    damager.Tank.Heal(40);
                }
            }
        }

        return damageTaken;
    }

    /// <summary>
    /// Heals the tank by the specified amount of points.
    /// </summary>
    /// <param name="points">The amount of points to heal.</param>
    /// <remarks>
    /// The tank cannot be healed if it is dead.
    /// Use <see cref="SetHealth"/> instead.
    /// </remarks>
    internal void Heal(int points)
    {
        Debug.Assert(points >= 0, "Healing points cannot be negative.");
        Debug.Assert(!this.IsDead, "Cannot heal a dead tank, use SetHealth instead.");
        this.Health = Math.Min(100, this.Health!.Value + points);
    }

    /// <summary>
    /// Sets the health of the tank.
    /// </summary>
    /// <param name="health">The health of the tank.</param>
    internal void SetHealth(int health)
    {
        this.Health = health;
    }

    /// <summary>
    /// Sets the position of the tank.
    /// </summary>
    /// <param name="x">The x coordinate of the tank.</param>
    /// <param name="y">The y coordinate of the tank.</param>
    internal void SetPosition(int x, int y)
    {
        this.PreviousX = this.X;
        this.PreviousY = this.Y;
        this.X = x;
        this.Y = y;
    }

    /// <summary>
    /// Stuns the tank.
    /// </summary>
    /// <param name="stun">
    /// The stun to apply to the tank.
    /// </param>
    internal void Stun(IStunEffect stun)
    {
        this.remainingStuns[stun] = stun.StunTicks;
    }

    /// <summary>
    /// Stuns the tank.
    /// </summary>
    /// <param name="stuns">
    /// The stuns to apply to the tank.
    /// </param>
    internal void Stun(IEnumerable<IStunEffect> stuns)
    {
        foreach (var stun in stuns)
        {
            this.Stun(stun);
        }
    }

    /// <summary>
    /// Updates the stun effects.
    /// </summary>
    internal void UpdateStunables()
    {
        foreach (var stunable in this.remainingStuns.Keys.ToList())
        {
            if (--this.remainingStuns[stunable] <= 0)
            {
                _ = this.remainingStuns.Remove(stunable);
            }
        }
    }

    /// <summary>
    /// Determines whether the tank is blocked by the specified stun effect.
    /// </summary>
    /// <param name="effect">The stun effect to check.</param>
    /// <returns>
    /// <see langword="true"/> if the tank is blocked by the specified stun effect;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    internal bool IsBlockedByStun(StunBlockEffect effect = StunBlockEffect.All)
    {
        return this.remainingStuns.Keys.Any(stun => stun.StunBlockEffect.HasFlag(effect));
    }
}
