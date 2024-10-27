#if HACKATHON

using GameClient.Scenes.Replay.MatchResultsCore;
using Microsoft.Xna.Framework;
using MonoRivUI;
using System.Collections.Generic;

namespace GameClient.UI.MatchResultsSceneComponents;

/// <summary>
/// Represents a player slot panel.
/// </summary>
internal class Scoreboard : Component
{
    private readonly AlignedListBox listBox;

    /// <summary>
    /// Initializes a new instance of the <see cref="Scoreboard"/> class.
    /// </summary>
    public Scoreboard()
    {
        this.listBox = new AlignedListBox
        {
            Parent = this,
            Orientation = MonoRivUI.Orientation.Vertical,
            Spacing = 10,
            ElementsAlignment = Alignment.Center,
        };

        var font = new ScalableFont(Styles.Fonts.Paths.Main, 17)
        {
            AutoResize = true,
            Spacing = 5,
        };

        _ = new Text(font, Color.White * 0.8f)
        {
            Parent = this,
            Value = "Points",
            Case = TextCase.Upper,
            TextAlignment = Alignment.Center,
            Transform =
            {
                RelativeSize = new Vector2(0.08f),
                RelativeOffset = new Vector2(0.7f, -0.1f),
            },
        };

        _ = new Text(font, Color.White * 0.8f)
        {
            Parent = this,
            Value = "Kills",
            Case = TextCase.Upper,
            TextAlignment = Alignment.Center,
            Transform =
            {
                RelativeSize = new Vector2(0.08f),
                RelativeOffset = new Vector2(0.9f, -0.1f),
            },
        };
    }

    /// <summary>
    /// Sets the players to display.
    /// </summary>
    /// <param name="players">The players to set.</param>
    public void SetPlayers(List<MatchResultsPlayer> players)
    {
        this.listBox.Clear();

        for (int i = 0; i < players.Count; i++)
        {
            var isQualified = i < 2;
            _ = new PlayerSlotPanel(players[i], isQualified)
            {
                Parent = this.listBox.ContentContainer,
                Transform =
                {
                    RelativeSize = new Vector2(1.0f, 0.22f),
                },
            };
        }
    }
}

#endif
