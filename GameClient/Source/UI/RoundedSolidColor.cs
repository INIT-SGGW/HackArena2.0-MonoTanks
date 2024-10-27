using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoRivUI;

namespace GameClient;

/// <summary>
/// Represents a solid color with rounded corners.
/// </summary>
internal class RoundedSolidColor : SolidColor, IButtonContent<RoundedSolidColor>
{
    private static readonly Dictionary<TextureCacheKey, TextureCacheValue> Cache = [];
    private bool autoAdjustRadius;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoundedSolidColor"/> class.
    /// </summary>
    /// <param name="color">The color to be drawn.</param>
    /// <param name="radius">The radius of the rounded corners.</param>
    public RoundedSolidColor(Color color, int radius)
        : base(color)
    {
       this.Radius = radius;
       this.Transform.SizeChanged += (s, e) => this.Reload(e.Before);
    }

    /// <summary>
    /// Gets the radius of the rounded corners.
    /// </summary>
    public int Radius { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the radius should
    /// be automatically adjusted to the current screen size.
    /// </summary>
    /// <remarks>
    /// If set to <see langword="true"/>, the <see cref="Radius"/>
    /// represents the radius for a screen size of 1920x1080.
    /// </remarks>
    public bool AutoAdjustRadius
    {
        get => this.autoAdjustRadius;
        set
        {
            if (this.autoAdjustRadius == value)
            {
                return;
            }

            this.autoAdjustRadius = value;
            this.Reload(this.Transform.Size);
        }
    }

    /// <inheritdoc/>
    bool IButtonContent<RoundedSolidColor>.IsButtonContentHovered(Point mousePosition)
    {
        Point location = this.Transform.Location;
        Point size = this.Transform.Size;
        Point mouseOffset = mousePosition - location;

        var radius = Math.Min(this.Radius, Math.Min(size.X, size.Y) / 2);

        // Check if inside central rectangle (not including corners)
        if (mouseOffset.X >= radius && mouseOffset.X < size.X - radius &&
            mouseOffset.Y >= 0 && mouseOffset.Y < size.Y)
        {
            return true;
        }

        // Check if inside rounded corners (Top-left, Top-right, Bottom-left, Bottom-right)
        return IsInsideCorner(mouseOffset, new Vector2(radius, radius), radius) || // Top-left
               IsInsideCorner(mouseOffset, new Vector2(size.X - radius - 1, radius), radius) || // Top-right
               IsInsideCorner(mouseOffset, new Vector2(radius, size.Y - radius - 1), radius) || // Bottom-left
               IsInsideCorner(mouseOffset, new Vector2(size.X - radius - 1, size.Y - radius - 1), radius); // Bottom-right
    }

    /// <inheritdoc/>
    public override void Load()
    {
        var size = this.Transform.Size;
        var radius = this.Radius;

        if (this.autoAdjustRadius)
        {
            radius = (int)(radius * Math.Min(ScreenController.Scale.X, ScreenController.Scale.Y));
        }

        radius = Math.Min(radius, Math.Min(size.X, size.Y) / 2);

        var cacheKey = new TextureCacheKey(size, radius);

        if (Cache.TryGetValue(cacheKey, out var cacheValue))
        {
            cacheValue.ReferenceCount++;
            this.Texture = cacheValue.Texture;
            this.IsLoaded = true;
            return;
        }

        this.Texture = this.CreateTexture(radius);
        Cache[cacheKey] = new TextureCacheValue(this.Texture);
        this.IsLoaded = true;
    }

    private static void FillRoundedCorners(float[] opacities, int width, int height, int radius)
    {
        // Top-left
        FillCorner(opacities, new Vector2(radius), radius, width, true, false);

        // Top-right
        FillCorner(opacities, new Vector2(width - radius - 1, radius), radius, width, false, false);

        // Bottom-left
        FillCorner(opacities, new Vector2(radius, height - radius - 1), radius, width, true, true);

        // Bottom-right
        FillCorner(opacities, new Vector2(width - radius - 1, height - radius - 1), radius, width, false, true);
    }

    private static void FillBordersAndCenter(float[] opacities, int width, int height, int radius)
    {
        // Fill straight borders
        for (int i = radius; i < width - radius; i++)
        {
            opacities[i] = 0.5f; // Top border
            opacities[i + ((height - 1) * width)] = 0.5f; // Bottom border
        }

        for (int j = radius; j < height - radius; j++)
        {
            opacities[j * width] = 0.5f; // Left border
            opacities[width - 1 + (j * width)] = 0.5f; // Right border
        }

        // Fill left, right and center
        for (int i = 1; i < width - 1; i++)
        {
            for (int j = radius; j < height - radius; j++)
            {
                opacities[i + (j * width)] = 1f;
            }
        }

        // Fill top and bottom
        for (int i = radius; i < width - radius; i++)
        {
            for (int j = 1; j < radius; j++)
            {
                opacities[i + (j * width)] = 1f; // Top
                opacities[i + ((height - j - 1) * width)] = 1f; // Bottom
            }
        }
    }

    private static void FillCorner(float[] opacities, Vector2 corner, int radius, int width, bool isLeft, bool isBottom)
    {
        var offsetX = isLeft ? 0 : width - radius;
        var offsetY = isBottom ? corner.Y : 0;

        for (int i = 0; i < radius; i++)
        {
            for (int j = 0; j < radius; j++)
            {
                var p = new Point(i + offsetX, j + (int)offsetY);
                var distance = Vector2.Distance(p.ToVector2(), corner);
                if (distance <= radius)
                {
                    float opacity = 1f;
                    if (distance > radius - 1)
                    {
                        opacity = 1f - (distance - (radius - 1));
                    }

                    opacities[p.X + (p.Y * width)] = opacity;
                }
            }
        }
    }

    private static void RemoveOldTextureFromCache(TextureCacheKey cacheKey)
    {
        if (Cache.TryGetValue(cacheKey, out var cacheValue))
        {
            cacheValue.ReferenceCount--;
            if (cacheValue.ReferenceCount == 0)
            {
                cacheValue.Texture.Dispose();
                _ = Cache.Remove(cacheKey);
            }
        }
    }

    private static bool IsInsideCorner(Point mousePosition, Vector2 cornerCenter, int radius)
    {
        return Vector2.Distance(mousePosition.ToVector2(), cornerCenter) <= radius;
    }

    private Texture2D CreateTexture(int radius)
    {
        var size = this.Transform.Size;
        var width = size.X;
        var height = size.Y;

        var opacities = new float[width * height];
        FillRoundedCorners(opacities, width, height, radius);
        FillBordersAndCenter(opacities, width, height, radius);

        var texture = new Texture2D(ScreenController.GraphicsDevice, width, height);
        var data = new Color[width * height];

        for (int i = 0; i < data.Length; i++)
        {
            data[i] = Color.White * opacities[i];
        }

        texture.SetData(data);
        return texture;
    }

    private void Reload(Point sizeBefore)
    {
        if (this.IsLoaded)
        {
            this.IsLoaded = false;
            var cacheKey = new TextureCacheKey(sizeBefore, this.Radius);
            RemoveOldTextureFromCache(cacheKey);
            this.Load();
        }
    }

    private record struct TextureCacheKey(Point Size, int Radius);

    private class TextureCacheValue(Texture2D texture, int referenceCounter = 1)
    {
        public Texture2D Texture { get; } = texture;

        public int ReferenceCount { get; set; } = referenceCounter;
    }
}
