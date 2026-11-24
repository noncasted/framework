using Internal;

namespace Global.Audio
{
    public static class AudioExtensions
    {
        // The scope must have an IAudioVolume (the settings of the game).
        public static IScopeBuilder AddAudio(this IScopeBuilder builder, AudioPlayer playerPrefab, AudioListener listenerPrefab)
        {
            // An instance is registered: audio sources on the prefab asset itself do not play.
            var player = builder.Instantiate(playerPrefab);
            var listener = builder.Instantiate(listenerPrefab);

            builder.RegisterComponent(player)
                   .As<IAudioPlayer>()
                   .As<IScopeSetup>();

            builder.RegisterComponent(listener)
                   .As<IScopeSetup>();

            return builder;
        }
    }
}
