using FishNet.Object;
using FishNet.Object.Synchronizing;
using Signals;
using UnityEngine;
using Zenject;

namespace Gameplay.Player.Vitals
{
    // Stacking drunkenness. Each drink adds intensity (capped); it decays over time.
    // Intensity is replicated so all clients drive drunk anims; the local owner's camera
    // reads it for the visual effect (PlayerCameraRig.SetDrunk).
    public sealed class PlayerDrunk : NetworkBehaviour
    {
        [SerializeField] private float _maxIntensity = 2f;
        [SerializeField] private float _perDrink = 0.6f;
        [SerializeField] private float _decayPerSec = 0.06f;
        [SerializeField] private float _drunkThreshold = 0.05f;

        [Inject] private SignalBus _signalBus;

        private readonly SyncVar<float> _intensity = new(0f);
        private bool _wasDrunk;

        public float Intensity => _intensity.Value;
        public bool IsDrunk => _intensity.Value > _drunkThreshold;

        public static PlayerDrunk Local { get; private set; }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _intensity.OnChange += OnIntensityChanged;
        }

        public override void OnStopNetwork()
        {
            _intensity.OnChange -= OnIntensityChanged;
            base.OnStopNetwork();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (base.IsOwner) Local = this;
        }

        public override void OnStopClient()
        {
            if (Local == this) Local = null;
            base.OnStopClient();
        }

        public void ServerAddDrink()
        {
            if (!base.IsServerInitialized) return;
            _intensity.Value = Mathf.Min(_maxIntensity, _intensity.Value + _perDrink);
        }

        private void Update()
        {
            if (!base.IsServerInitialized || _intensity.Value <= 0f) return;
            _intensity.Value = Mathf.Max(0f, _intensity.Value - _decayPerSec * Time.deltaTime);
        }

        private void OnIntensityChanged(float prev, float next, bool asServer)
        {
            bool drunk = next > _drunkThreshold;
            if (drunk != _wasDrunk)
            {
                _wasDrunk = drunk;
                _signalBus?.Fire(new PlayerDrunkSignal(base.IsOwner, next));
            }
        }
    }
}
