using System;
using Internal;
using NUnit.Framework;
using UnityEngine;

namespace Global.Cameras.Tests
{
    // CurrentCamera holds a camera while the lifetime it was set with lives (the scene of the camera): after it ends
    // the camera is not current, and the listeners are told. ScreenToWorld without a camera is an error, not a zero.
    public class CurrentCameraTests
    {
        [Test]
        public void SetCamera_LifetimeEnds_CameraIsNotCurrent_ListenersTold()
        {
            var current = new CurrentCamera();
            var listeners = new Lifetime();
            var seen = 0;
            current.Value.Advise(listeners, (_, _) => seen++);

            var gameObject = new GameObject("camera");
            var camera = gameObject.AddComponent<Camera>();
            var scene = new Lifetime();

            try
            {
                current.SetCamera(scene, camera);
                Assert.AreSame(camera, current.Current);

                scene.Terminate();

                Assert.IsTrue(ReferenceEquals(current.Current, null), "the camera of an ended scene stays current");
                Assert.AreEqual(2, seen);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                listeners.Terminate();
            }
        }

        // A later camera is not cleared by the end of the lifetime of an earlier one.
        [Test]
        public void SetCamera_EarlierLifetimeEnds_LaterCameraStays()
        {
            var current = new CurrentCamera();
            var first = new GameObject("first").AddComponent<Camera>();
            var second = new GameObject("second").AddComponent<Camera>();
            var firstLifetime = new Lifetime();
            var secondLifetime = new Lifetime();

            try
            {
                current.SetCamera(firstLifetime, first);
                current.SetCamera(secondLifetime, second);

                firstLifetime.Terminate();

                Assert.AreSame(second, current.Current);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first.gameObject);
                UnityEngine.Object.DestroyImmediate(second.gameObject);
                secondLifetime.Terminate();
            }
        }

        [Test]
        public void ScreenToWorld_NoCamera_Throws()
        {
            var utils = new CameraUtils(new CurrentCamera());

            Assert.Throws<InvalidOperationException>(() => utils.ScreenToWorld(Vector3.one));
        }
    }
}
