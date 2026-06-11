using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Common
{
    public class LoadingBarView : WindowView<EmptyPresenter>
    {
        [Header("Wire any one or more — all are optional")]
        [SerializeField] private Slider _progressBar;
        [SerializeField] private Image _fillImage;
        [SerializeField] private TMP_Text _percentLabel;

        [Tooltip("Higher = the bar catches up to the target faster. ~5-10 feels good.")]
        [SerializeField] private float _smoothing = 7f;

        private float _target;
        private float _current;

        public static LoadingBarView Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
            ResetBar();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetProgress(float p)
        {
            float v = Mathf.Clamp01(p);
            if (v > _target) _target = v;
        }

        public void ResetBar()
        {
            _target = 0f;
            _current = 0f;
            Apply();
        }

        protected override void OnBound() => ResetBar();

        private void Update()
        {
            if (_current >= _target) return;
            _current = Mathf.Lerp(_current, _target, 1f - Mathf.Exp(-_smoothing * Time.unscaledDeltaTime));
            if (_target - _current < 0.005f) _current = _target;
            Apply();
        }

        private void Apply()
        {
            if (_progressBar != null) _progressBar.value = _current;
            if (_fillImage != null) _fillImage.fillAmount = _current;
            if (_percentLabel != null) _percentLabel.text = $"{Mathf.RoundToInt(_current * 100f)}%";
        }
    }
}
