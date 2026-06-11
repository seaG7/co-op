using System;

namespace Infrastructure.Services.Settings
{
    public interface ISettingsService
    {
        float MasterVolume { get; }
        float MouseSensitivity { get; }

        event Action Changed;

        void SetMasterVolume(float value);
        void SetMouseSensitivity(float value);
    }
}
