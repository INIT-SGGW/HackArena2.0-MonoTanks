﻿using System.Collections.Generic;
using System.Linq;
using GameClient.Scenes.GameOverlays.GameMenuCore;
using Microsoft.Xna.Framework;
using MonoRivUI;

namespace GameClient.Scenes.GameOverlays;

/// <summary>
/// Represents the game menu scene.
/// </summary>
[AutoInitialize]
[AutoLoadContent]
internal class GameMenu : Scene, IOverlayScene
{
    private SolidColor background = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameMenu"/> class.
    /// </summary>
    public GameMenu()
        : base()
    {
        var initializer = new GameMenuInitializer(this);
        _ = new GameMenuComponents(initializer);
    }

    /// <inheritdoc/>
    public IEnumerable<IComponent> OverlayComponents => [this.BaseComponent];

    /// <inheritdoc/>
    public int Priority => 1 << 10;

    /// <inheritdoc/>
    public override void Draw(GameTime gameTime)
    {
        this.background.Draw(gameTime);
        MainEffect.Draw();
        base.Draw(gameTime);
    }

    /// <inheritdoc/>
    protected override void Initialize(Component baseComponent)
    {
        this.background = new SolidColor(Color.Black * 0.88f)
        {
            Transform = { Type = TransformType.Relative },
        };
    }

    /// <inheritdoc/>
    protected override void LoadSceneContent()
    {
        this.background.Load();

        var textures = this.BaseComponent.GetAllDescendants<TextureComponent>();
        textures.ToList().ForEach(x => x.Load());
    }
}
