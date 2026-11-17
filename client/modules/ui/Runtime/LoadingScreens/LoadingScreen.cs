using System;
using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace Global.UI
{
    public interface ILoadingScreen
    {
        UniTask Show();
        void ShowInstantly();
        void Hide();

        // Hide only starts the fade: completes when nothing of the screen is left over the scene under it
        // (at once if it is not shown).
        UniTask WaitHidden(IReadOnlyLifetime lifetime);
    }

    [DisallowMultipleComponent]
    public class LoadingScreen : MonoBehaviour, ILoadingScreen, IUpdatable, IScopeSetup
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Curve _curve;
        [SerializeField] private LoadingScreenAnimation _animation;

        private IUpdater _updater;
        private CurveInstance _curveInstance;
        private Direction2 _direction;

        [Inject]
        public void Construct(IUpdater updater)
        {
            _updater = updater;
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _animation.Validate();
            _curveInstance = _curve.CreateInstance();
            _updater.Add(lifetime, this);
        }

        public async UniTask Show()
        {
            _direction = Direction2.Forward;

            // A Hide during the fade in ends the wait: nothing is going to finish the fade in any more.
            await UniTask.WaitUntil(() => _curveInstance.IsFinished == true || _direction != Direction2.Forward);
        }

        public void ShowInstantly()
        {
            _direction = Direction2.Forward;

            // The curve goes to its end too: the next update takes the alpha from it.
            _curveInstance.StepForward(_curve.Time);
            _group.alpha = 1f;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _direction = Direction2.Backward;
        }

        public UniTask WaitHidden(IReadOnlyLifetime lifetime)
        {
            return UniTask.WaitUntil(
                () => Mathf.Approximately(_group.alpha, 0f) == true,
                cancellationToken: lifetime.Token);
        }

        public void OnUpdate(float delta)
        {
            _animation.UpdateAnimation(delta);

            var alpha = _direction switch
            {
                Direction2.Forward => _curveInstance.StepForward(delta),
                Direction2.Backward => _curveInstance.StepBack(delta),
                _ => throw new ArgumentOutOfRangeException()
            };

            _group.alpha = alpha;

            if (Mathf.Approximately(alpha, 0f) == true)
                gameObject.SetActive(false);
            else
                gameObject.SetActive(true);
        }
    }
}