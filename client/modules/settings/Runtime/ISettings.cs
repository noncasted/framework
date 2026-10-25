using Cysharp.Threading.Tasks;
using Global.Audio;

namespace Global.Settings
{
    // The settings of the player. The volumes are the IAudioVolume of the audio player; the language goes to the
    // Localizer. A view edits a Copy of the save, shows it at once with Push and keeps it with Apply (which pushes
    // too). The untyped members are for code that knows only the base save; a game works with ISettings<TSave>
    // (or its own interface over it, see Settings<TSave>).
    public interface ISettings : IAudioVolume
    {
        SettingsSave Copy();
        void Push(SettingsSave save);
        UniTask Apply(SettingsSave save);
    }

    public interface ISettings<TSave> : ISettings where TSave : SettingsSave, new()
    {
        new TSave Copy();
        void Push(TSave save);
        UniTask Apply(TSave save);
    }
}
