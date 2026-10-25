using System;
using Cysharp.Threading.Tasks;
using Global.Localization;
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
    public abstract class Settings<TSave> : ISettings<TSave>, IScopeSetupAsync where TSave : SettingsSave, new()
    {
        protected Settings(ISaves saves, SettingsOptions<TSave> options)
        {
            _saves = saves;
            _options = options;
        }

        private readonly ISaves _saves;
        private readonly SettingsOptions<TSave> _options;

        private readonly ViewableProperty<float> _masterVolume = new();
        private readonly ViewableProperty<float> _soundsVolume = new();
        private readonly ViewableProperty<float> _musicVolume = new();

        private TSave _save;

        public IViewableProperty<float> MasterVolume => _masterVolume;
        public IViewableProperty<float> SoundsVolume => _soundsVolume;
        public IViewableProperty<float> MusicVolume => _musicVolume;

        // The save in force: what Copy copies and the last Apply kept.
        protected TSave Current => _save;

        public UniTask OnSetupAsync(IReadOnlyLifetime lifetime)
        {
            _save = _saves.Get<TSave>();

            if (_save.WasChanged == false)
                _save.CopyFrom(_options.DefaultValues);

            if (_save.IsLanguagePicked == false)
                _save.Language = Localizer.FromSystem();

            Push(_save);

            return UniTask.CompletedTask;
        }

        public TSave Copy()
        {
            return SettingsSave.Copy(_save);
        }

        public void Push(TSave save)
        {
            _masterVolume.Set(save.MasterVolume);
            _soundsVolume.Set(save.SoundsVolume);
            _musicVolume.Set(save.MusicVolume);

            Localizer.SetLanguage(save.Language);

            OnPush(save);
        }

        public async UniTask Apply(TSave save)
        {
            save.WasChanged = true;
            await _saves.Save(save);
            _save = save;
            Push(save);
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

    // The settings of a game without settings of its own: the volumes and the language.
    public sealed class Settings : Settings<SettingsSave>
    {
        public Settings(ISaves saves, SettingsOptions<SettingsSave> options) : base(saves, options)
        {
        }
    }
}
