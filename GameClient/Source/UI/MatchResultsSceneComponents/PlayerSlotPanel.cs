#if HACKATHON

using GameClient.Scenes.Replay.MatchResultsCore;
using Microsoft.Xna.Framework;
using MonoRivUI;

namespace GameClient.UI.MatchResultsSceneComponents;

/// <summary>
/// Represents a player slot panel.
/// </summary>
/// <remarks>
/// The player slot panel displays the player's nickname,
/// score on the match results screen.
/// </remarks>
internal class PlayerSlotPanel : Component
{
    private readonly RoundedSolidColor background;
    private readonly Text kills;
    private readonly Text points;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerSlotPanel"/> class.
    /// </summary>
    /// <param name="player">The player to display.</param>
    /// <param name="isQualified">A value indicating whether the player is qualified.</param>
    public PlayerSlotPanel(MatchResultsPlayer player, bool isQualified)
        : this(player)
    {
        if (!isQualified)
        {
            this.background.Opacity = 0.15f;
        }
    }

    private PlayerSlotPanel(MatchResultsPlayer player)
    {
        var color = new Color(player.Color);

        this.background = new RoundedSolidColor(MonoTanks.ThemeColor, 20)
        {
            AutoAdjustRadius = true,
            Parent = this,
            Opacity = 0.45f,
        };

        var font = new ScalableFont(Styles.Fonts.Paths.Main, 19);

        var nickContainer = new Container()
        {
            Parent = this,
            Transform =
            {
                RelativeSize = new Vector2(0.62f, 1f),
                RelativeOffset = new Vector2(0.04f, 0.0f),
                Alignment = Alignment.Left,
            },
        };

        // Nickname
        _ = new Text(font, Color.White)
        {
            Parent = nickContainer,
            Value = player.Nickname,
            Spacing = 8,
            Case = TextCase.Upper,
            TextAlignment = Alignment.Left,
            TextShrink = TextShrinkMode.HeightAndWidth,
            Transform =
            {
                RelativeSize = new Vector2(0.9f, 0.6f),
                Alignment = Alignment.Left,
            },
        };

        this.points = new Text(font, color)
        {
            Parent = this.background,
            Value = player.Points.ToString(),
            Spacing = 8,
            TextAlignment = Alignment.Right,
            Transform =
            {
                RelativeSize = new Vector2(0.9f, 0.6f),
                Alignment = Alignment.Right,
                RelativeOffset = new Vector2(-0.25f, 0.0f),
            },
        };

        this.kills = new Text(font, color)
        {
            Parent = this.background,
            Value = player.Kills.ToString(),
            Spacing = 8,
            TextAlignment = Alignment.Right,
            Transform =
            {
                RelativeSize = new Vector2(0.9f, 0.6f),
                Alignment = Alignment.Right,
                RelativeOffset = new Vector2(-0.05f, 0.0f),
            },
        };
    }
}

#endif
