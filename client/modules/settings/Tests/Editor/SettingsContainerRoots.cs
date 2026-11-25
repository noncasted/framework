using Cysharp.Threading.Tasks;
using Global.Publisher;
using Internal;
using UnityEngine;

namespace Global.Settings.Tests
{
    public static class SettingsContainerRoots
    {
        [ContainerGraphRoot]
        public static async UniTask ListenerFirst(IScopeBuilder builder)
        {
            builder.Register<VolumeListener>().As<IScopeSetup>();
            await builder.AddSaves(SettingsContainerProbe.Saves);
            builder.AddSettings(SettingsContainerProbe.Options);
        }
    }
}
