#if HACKATHON

using GameClient.UI.MatchResultsSceneComponents;
using Microsoft.Xna.Framework;
using MonoRivUI;

namespace GameClient.Scenes.Replay.MatchResultsCore;

/// <summary>
/// Represents the initializer for the match results scene.
/// </summary>
/// <param name="matchResults">The match results scene to initialize.</param>
internal class MatchResultsInitializer(MatchResults matchResults)
{
    /// <summary>
    /// Creates a match name component.
    /// </summary>
    /// <returns>The created match name component.</returns>
    public Text CreateMatchName()
    {
        var font = new ScalableFont(Styles.Fonts.Paths.Main, 21);
        return new Text(font, Color.White)
        {
            Parent = matchResults.BaseComponent,
            Value = "Match name",
            Case = TextCase.Upper,
            AdjustTransformSizeToText = AdjustSizeOption.HeightAndWidth,
            Spacing = 10,
            Transform =
            {
                Alignment = Alignment.Top,
                RelativeOffset = new Vector2(0.0f, 0.1f),
            },
        };
    }

    /// <summary>
    /// Initializes the scoreboard component.
    /// </summary>
    /// <returns>The initialized scoreboard component.</returns>
    public Scoreboard InitializeScoreboard()
    {
        return new Scoreboard()
        {
            Parent = matchResults.BaseComponent,
            Transform =
            {
                Alignment = Alignment.Center,
                RelativeSize = new Vector2(0.6f, 0.6f),
                RelativeOffset = new Vector2(0.0f, 0.1f),
                MinSize = new Point(620, 1),
            },
        };
    }
}

#endif
