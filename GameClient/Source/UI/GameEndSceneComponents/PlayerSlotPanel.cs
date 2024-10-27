using GameClient.UI.SceneComponents;
using GameLogic;
using Microsoft.Xna.Framework;
using MonoRivUI;

namespace GameClient.UI.GameEndSceneComponents;

/// <summary>
/// Represents a player slot panel.
/// </summary>
/// <remarks>
/// The player slot panel displays the player's nickname,
/// score and tank icon on the game end screen.
/// </remarks>
internal class PlayerSlotPanel : Component
{
    private readonly RoundedSolidColor background;

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "CodeQuality",
        "IDE0052:Remove unread private members",
        Justification = "Used in other configuration.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "CodeQuality",
        "IDE0079:Remove unnecessary suppression",
        Justification = "Used in other configuration.")]
    private readonly Text score;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerSlotPanel"/> class.
    /// </summary>
    /// <param name="player">The player to display.</param>
    public PlayerSlotPanel(Player player)
    {
        var color = new Color(player.Color);

        this.background = new RoundedSolidColor(MonoTanks.ThemeColor, 20)
        {
            AutoAdjustRadius = true,
            Parent = this,
            Opacity = 0.45f,
        };

        var iconBackground = new RoundedSolidColor(Color.White, 15)
        {
            Parent = this,
            Opacity = 0.3f,
            Transform =
            {
                Alignment = Alignment.Left,
                Ratio = new Ratio(1, 1),
            },
        };

        var tankSpriteIcon = new TankSpriteIcon()
        {
            Parent = iconBackground,
            Transform =
            {
                RelativeSize = new Vector2(0.55f),
                Alignment = Alignment.Center,
                Ratio = new Ratio(1, 1),
            },
        };

        tankSpriteIcon.SetColor(color);

        var font = new ScalableFont(Styles.Fonts.Paths.Main, 19);

        var nickContainer = new Container()
        {
            Parent = this,
            Transform =
            {
                RelativeSize = new Vector2(0.82f, 1f),
                Alignment = Alignment.Right,
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

        this.score = new Text(font, color)
        {
            Parent = this.background,
            Value = player.Score.ToString(),
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

#if HACKATHON
    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerSlotPanel"/> class.
    /// </summary>
    /// <param name="player">The player to display.</param>
    /// <param name="isQualified">A value indicating whether the player is qualified.</param>
    public PlayerSlotPanel(Player player, bool isQualified)
        : this(player)
    {
        if (!isQualified)
        {
            this.score.Color = Color.White;
            this.background.Opacity = 0.15f;
        }
    }
#endif
}
