using System;
using Cysharp.Threading.Tasks;
using Global.Publisher;
using Internal;

namespace Global.Settings
{
    // The settings of a game are a subclass over a derived save rather than hooks: the fields of the game live in
    // its own save type (one JSON object with the base fields, stored by ISaves under the name of the derived type)
    // and the properties of the game in its subclass, which maps the fields to them in OnPush. The game declares
    // its own interface over ISettings<TSave> for its properties and registers it with AddSettings<TSettings, TSave,
    // TService>:
    //
    //     public interface IGameSettings : ISettings<GameSettingsSave> { IViewableProperty<bool> IsShakeOn { get; } }
    //     public class GameSettings : Settings<GameSettingsSave>, IGameSettings { ... OnPush(save) ... }
    //     builder.AddSettings<GameSettings, GameSettingsSave, IGameSettings>(GlobalAssets.SettingsOptions);
    //
    // The save is taken from the loaded saves (AddSaves loads them while the scope is built) when the settings are built,
    // so every service that takes them sees the saved values from its own constructor on. OnLoad and OnPush are called from this constructor, before the constructor of the subclass: the
    // properties of the game are field initializers of the subclass, not assignments of its constructor.
    public abstract class Settings<TSave> : ISettings<TSave> where TSave : SettingsSave, new()
    {
        protected Settings(ISaves saves, SettingsOptions<TSave> options)
        {
            _saves = saves;

            _save = _saves.Get<TSave>();

            if (_save.WasChanged == false)
                _save.CopyFrom(options.DefaultValues);

            OnLoad(_save);
            Push(_save);
        }

        private readonly ISaves _saves;

        private readonly ViewableProperty<float> _masterVolume = new();
        private readonly ViewableProperty<float> _soundsVolume = new();
        private readonly ViewableProperty<float> _musicVolume = new();

        private TSave _save;

        public IViewableProperty<float> MasterVolume => _masterVolume;
        public IViewableProperty<float> SoundsVolume => _soundsVolume;
        public IViewableProperty<float> MusicVolume => _musicVolume;

        // The save in force: what Copy copies and the last Apply kept.
        protected TSave Current => _save;

        public TSave Copy()
        {
            return SettingsSave.Copy(_save);
        }

        public void Push(TSave save)
        {
            _masterVolume.Set(save.MasterVolume);
            _soundsVolume.Set(save.SoundsVolume);
            _musicVolume.Set(save.MusicVolume);

            OnPush(save);
        }

        // A copy is kept: the caller's instance (the save a settings window edits) changed later changed the
        // settings in force without a Push or a save.
        public async UniTask Apply(TSave save)
        {
            var applied = SettingsSave.Copy(save);
            applied.MarkChanged();

            await _saves.Save(applied);
            _save = applied;
            Push(applied);
        }

        // The save is read and has its defaults, before the first Push: the fields of the game that follow something at
        // every start (the language of the system until the player picks one).
        protected virtual void OnLoad(TSave save)
        {
        }

        // The fields of the game go to its properties; called on every Push, the base values are already set.
        protected virtual void OnPush(TSave save)
        {
        }

        SettingsSave ISettings.Copy()
        {
            return Copy();
        }

        void ISettings.Push(SettingsSave save)
        {
            Push(Typed(save));
        }

        UniTask ISettings.Apply(SettingsSave save)
        {
            return Apply(Typed(save));
        }

        private static TSave Typed(SettingsSave save)
        {
            if (save is TSave typed)
                return typed;

            throw new ArgumentException($"[Settings] {save.GetType().Name} is not a {typeof(TSave).Name}", nameof(save));
        }
    }

    // The settings of a game without settings of its own: the volumes.
    public sealed class Settings : Settings<SettingsSave>
    {
        public Settings(ISaves saves, SettingsOptions<SettingsSave> options) : base(saves, options)
        {
        }
    }
}
