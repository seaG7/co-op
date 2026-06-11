using System;
using TMPro;
using UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Settings
{
    public class SettingsView : WindowView<SettingsPresenter>
    {
        [Header("Sound — master volume (Slider 0..1)")]
        [SerializeField] private Slider _masterVolume;
        [SerializeField] private TMP_Text _masterVolumeLabel;

        [Header("Mouse sensitivity (Slider min/max = multiplier range, e.g. 0.1..5)")]
        [SerializeField] private Slider _sensitivity;
        [SerializeField] private TMP_Text _sensitivityLabel;

        [Header("Controls")]
        [SerializeField] private Button _backButton;

        public event Action<float> MasterVolumeChanged;
        public event Action<float> SensitivityChanged;
        public event Action BackClicked;

        protected override void OnBound()
        {
            if (_masterVolume != null) _masterVolume.onValueChanged.AddListener(OnMasterChanged);
            if (_sensitivity != null) _sensitivity.onValueChanged.AddListener(OnSensitivityChanged);
            if (_backButton != null) _backButton.onClick.AddListener(RaiseBack);
        }

        protected override void OnUnbinding()
        {
            if (_masterVolume != null) _masterVolume.onValueChanged.RemoveListener(OnMasterChanged);
            if (_sensitivity != null) _sensitivity.onValueChanged.RemoveListener(OnSensitivityChanged);
            if (_backButton != null) _backButton.onClick.RemoveListener(RaiseBack);
        }

        public void SetMasterVolume(float value)
        {
            if (_masterVolume != null) _masterVolume.SetValueWithoutNotify(value);
            UpdateMasterLabel(value);
        }

        public void SetSensitivity(float value)
        {
            if (_sensitivity != null) _sensitivity.SetValueWithoutNotify(value);
            UpdateSensitivityLabel(value);
        }

        private void OnMasterChanged(float v)
        {
            UpdateMasterLabel(v);
            MasterVolumeChanged?.Invoke(v);
        }

        private void OnSensitivityChanged(float v)
        {
            UpdateSensitivityLabel(v);
            SensitivityChanged?.Invoke(v);
        }

        private void RaiseBack() => BackClicked?.Invoke();

        private void UpdateMasterLabel(float v)
        {
            if (_masterVolumeLabel != null) _masterVolumeLabel.text = $"{Mathf.RoundToInt(Mathf.Clamp01(v) * 100f)}%";
        }

        private void UpdateSensitivityLabel(float v)
        {
            if (_sensitivityLabel != null) _sensitivityLabel.text = v.ToString("0.0");
        }
    }
}
