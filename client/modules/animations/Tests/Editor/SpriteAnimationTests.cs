using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Internal.Tests
{
    // SpriteAnimation driven by hand (OnUpdate): a PlayAwaited awaited right after another one completes; a loop keeps
    // the time left over at its wrap and draws the frame it wraps to; the first frame is drawn at the start; an
    // animation without frames is refused.
    public class SpriteAnimationTests
    {
        [Test]
        public void PlayAsync_ChainedAfterCompletion_SecondCompletes()
        {
            var (animation, _, frames, lifetime) = Create(2);
            var completed = 0;

            Chain().NoAwait();

            for (var i = 0; i < 10; i++)
                animation.OnUpdate(0.3f);

            Assert.AreEqual(2, completed);
            Assert.IsFalse(animation.IsPlaying);
            lifetime.Terminate();

            async UniTaskVoid Chain()
            {
                await animation.PlayAwaited(lifetime, 1f);
                completed++;
                await animation.PlayAwaited(lifetime, 1f);
                completed++;
            }
        }

        // Two frames of 0.5 s: 0.75 s in - frame 1; 0.5 s more wraps with 0.25 s left - frame 0; 0.3 s more passes
        // 0.5 s of the new round - frame 1.
        [Test]
        public void PlayLooped_Wrap_KeepsLeftoverTime_DrawsFirstFrame()
        {
            var (animation, renderer, frames, lifetime) = Create(2);

            animation.PlayLooped(lifetime, 1f);

            animation.OnUpdate(0.75f);
            Assert.AreSame(frames[1], renderer.Sprite);

            animation.OnUpdate(0.5f);
            Assert.AreSame(frames[0], renderer.Sprite, "the frame the loop wraps to is not drawn");

            animation.OnUpdate(0.3f);
            Assert.AreSame(frames[1], renderer.Sprite, "the time left over at the wrap is lost");
            lifetime.Terminate();
        }

        // The lifetime given to the play ends the play at once: nothing needs the update of the animation for that (its
        // own update may be over too).
        [Test]
        public void PlayAsync_LifetimeEnds_WaitIsCanceled()
        {
            var (animation, _, _, lifetime) = Create(2);

            var wait = animation.PlayAwaited(lifetime, 1f);
            lifetime.Terminate();

            Assert.AreEqual(UniTaskStatus.Canceled, wait.Status, "the await hangs for good");
            Assert.IsFalse(animation.IsPlaying);
        }

        [Test]
        public void Play_LifetimeEnds_Stops()
        {
            var (animation, _, _, lifetime) = Create(2);

            animation.PlayLooped(lifetime, 1f);
            lifetime.Terminate();

            Assert.IsFalse(animation.IsPlaying);
        }

        // A lifetime of an older play does not stop the newer one.
        [Test]
        public void OlderLifetimeEnds_NewerPlayGoesOn()
        {
            var (animation, _, _, older) = Create(2);
            var newer = new Lifetime();

            animation.Play(older, 1f);
            animation.PlayLooped(newer, 1f);
            older.Terminate();

            Assert.IsTrue(animation.IsPlaying);
            newer.Terminate();
        }

        // Four frames of 0.25 s: 0.1 s in - frame 0; one long delta of 2 s jumps over the last frame - it is drawn anyway.
        [Test]
        public void PlayAsync_LongDeltaPastEnd_EndsOnLastFrame()
        {
            var (animation, renderer, frames, lifetime) = Create(4);

            animation.PlayAwaited(lifetime, 1f).Forget();
            animation.OnUpdate(0.1f);
            animation.OnUpdate(2f);

            Assert.IsFalse(animation.IsPlaying);
            Assert.AreSame(frames[3], renderer.Sprite, "the animation ended before its last frame");
            lifetime.Terminate();
        }

        [Test]
        public void Play_DrawsFirstFrameAtOnce()
        {
            var (animation, renderer, frames, lifetime) = Create(3);

            animation.Play(lifetime, 1f);

            Assert.AreSame(frames[0], renderer.Sprite);
            lifetime.Terminate();
        }

        [Test]
        public void Play_NoFrames_Throws()
        {
            var (animation, _, _, lifetime) = Create(0);

            var exception = Assert.Throws<InvalidOperationException>(() => animation.Play(lifetime, 1f));
            StringAssert.Contains("no frames", exception.Message);
            lifetime.Terminate();
        }

        private static (SpriteAnimation, FakeRenderer, List<Sprite>, Lifetime) Create(int frameCount)
        {
            var frames = new List<Sprite>();
            var texture = new Texture2D(1, 1);

            for (var i = 0; i < frameCount; i++)
                frames.Add(Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero));

            var renderer = new FakeRenderer();
            var animation = new SpriteAnimation(null, renderer, 1f, Color.white, new ForwardFrameProvider(frames));
            return (animation, renderer, frames, new Lifetime());
        }

        private class FakeRenderer : ISpriteAnimationRenderer
        {
            public Sprite Sprite { get; private set; }

            public void SetSprite(Sprite sprite)
            {
                Sprite = sprite;
            }

            public void SetColor(Color color)
            {
            }
        }
    }
}
