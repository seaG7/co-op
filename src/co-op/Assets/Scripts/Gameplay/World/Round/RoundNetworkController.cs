using Data.Rounds;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Gameplay.Net;
using Signals;
using Zenject;

namespace Gameplay.World.Round
{
    public sealed class RoundNetworkController : InjectableNetworkBehaviour
    {
        [Inject] private SignalBus _signalBus;

        private readonly SyncVar<RoundOutcome> _outcome = new(RoundOutcome.None);
        private bool _subscribed;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _outcome.OnChange += OnOutcomeChanged;
        }

        public override void OnStopNetwork()
        {
            _outcome.OnChange -= OnOutcomeChanged;
            base.OnStopNetwork();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Subscribe();
        }

        public override void OnStopServer()
        {
            Unsubscribe();
            base.OnStopServer();
        }

        private void Subscribe()
        {
            if (_subscribed || _signalBus == null) return;
            _signalBus.Subscribe<SourceDestroyedSignal>(OnSourceDestroyed);
            _signalBus.Subscribe<AllPlayersDownedOrDeadSignal>(OnAllPlayersDown);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _signalBus == null) return;
            _signalBus.TryUnsubscribe<SourceDestroyedSignal>(OnSourceDestroyed);
            _signalBus.TryUnsubscribe<AllPlayersDownedOrDeadSignal>(OnAllPlayersDown);
            _subscribed = false;
        }

        private void OnSourceDestroyed(SourceDestroyedSignal _) => ServerSetOutcome(RoundOutcome.Victory);
        private void OnAllPlayersDown(AllPlayersDownedOrDeadSignal _) => ServerSetOutcome(RoundOutcome.Defeat);

        private void ServerSetOutcome(RoundOutcome outcome)
        {
            if (!base.IsServerInitialized || _outcome.Value != RoundOutcome.None) return;
            _outcome.Value = outcome;
        }

        public void ServerDebugSetOutcome(RoundOutcome outcome) => ServerSetOutcome(outcome);

        private void OnOutcomeChanged(RoundOutcome prev, RoundOutcome next, bool asServer)
        {
            if (asServer || next == RoundOutcome.None) return;
            _signalBus?.Fire(new GameEndedSignal(next));
        }

        // Any client may ask the server to restart the round; the server relays it to everyone
        // (RunLocally → server included) so all peers re-enter LoadGameState and the server reloads.
        [ServerRpc(RequireOwnership = false)]
        public void ServerRequestRestart() => RpcRestart();

        [ObserversRpc(RunLocally = true)]
        private void RpcRestart() => _signalBus?.Fire(new GameRestartingSignal());
    }
}
