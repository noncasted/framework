
namespace Internal
{
    public class ForwardSpriteAnimation : SpriteAnimation
    {
        public ForwardSpriteAnimation(Utils utils, ISpriteAnimationData data) : base(
                utils.Updater,
                utils.Renderer,
                data.Time,
                data.Color,
                new ForwardFrameProvider(data.Sprites)
            )
        {
        }

        public class Utils
        {
            public Utils(IUpdater updater, ISpriteAnimationRenderer renderer)
            {
                Updater = updater;
                Renderer = renderer;
            }

            public IUpdater Updater { get; }
            public ISpriteAnimationRenderer Renderer { get; }
        }
    }
}