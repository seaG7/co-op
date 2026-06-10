using Gameplay.Player.Vitals;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gameplay.Player.View
{
    // Drives a global post-processing Volume's weight from the local player's drunkenness.
    // Put this on a global Volume in the scene (or drop the DrunkVolume prefab). The profile
    // holds the drunk look (chromatic aberration / lens distortion / vignette / ...) at full
    // strength; this fades it in/out with the drunk intensity.
    [RequireComponent(typeof(Volume))]
    public sealed class DrunkPostFx : MonoBehaviour
    {
        [SerializeField] private Volume _volume;
        [Tooltip("Drunk intensity at which the post-fx Volume reaches full weight.")]
        [SerializeField] private float _fullAtIntensity = 1.2f;
        [SerializeField] private float _smoothing = 4f;

        private void Awake()
        {
            if (_volume == null) _volume = GetComponent<Volume>();
            if (_volume != null) _volume.weight = 0f;
        }

        private void Update()
        {
            if (_volume == null) return;
            float intensity = PlayerDrunk.Local != null ? PlayerDrunk.Local.Intensity : 0f;
            float target = Mathf.Clamp01(intensity / Mathf.Max(0.01f, _fullAtIntensity));
            _volume.weight = Mathf.Lerp(_volume.weight, target, 1f - Mathf.Exp(-_smoothing * Time.deltaTime));
        }
    }
}
