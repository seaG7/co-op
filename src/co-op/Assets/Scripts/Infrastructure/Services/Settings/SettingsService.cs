using System;
using UnityEngine;
using Zenject;

namespace Infrastructure.Services.Settings
{
    public sealed class SettingsService : ISettingsService, IInitializable
    {
        private const string KeyMaster = "settings.masterVolume";
        private const string KeySensitivity = "settings.mouseSensitivity";
        private const float DefaultMaster = 1f;
        private const float DefaultSensitivity = 1f;
        private const float MinSensitivity = 0.05f;
        private const float MaxSensitivity = 10f;

        private float _master = DefaultMaster;
        private float _sensitivity = DefaultSensitivity;

        public float MasterVolume => _master;
        public float MouseSensitivity => _sensitivity;

        public event Action Changed;

        public void Initialize()
        {
            _master = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMaster, DefaultMaster));
            _sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(KeySensitivity, DefaultSensitivity), MinSensitivity, MaxSensitivity);
            ApplyAudio();
        }

        public void SetMasterVolume(float value)
        {
            _master = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(KeyMaster, _master);
            PlayerPrefs.Save();
            ApplyAudio();
            Changed?.Invoke();
        }

        public void SetMouseSensitivity(float value)
        {
            _sensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
            PlayerPrefs.SetFloat(KeySensitivity, _sensitivity);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private void ApplyAudio() => AudioListener.volume = _master;
    }
}
