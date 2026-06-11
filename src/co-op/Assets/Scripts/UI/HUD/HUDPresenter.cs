using Data.Players;
using Data.World;
using Infrastructure.Services.Network;
using Infrastructure.Services.Player;
using Signals;
using UI.Common;

namespace UI.HUD
{
    public sealed class HUDPresenter : IPresenter
    {
        private readonly HUDView _view;
        private readonly ISessionService _session;
        private readonly IPlayerService _playerService;
        private readonly SignalBus _signalBus;

        private bool _sourceOpen;
        private bool _charged;

        public HUDPresenter(HUDView view, ISessionService session, IPlayerService playerService, SignalBus signalBus)
        {
            _view = view; _session = session; _playerService = playerService; _signalBus = signalBus;
        }

        public void Initialize()
        {
            _session.StateChanged += OnSessionState;
            _playerService.LocalPlayerAssigned += OnLocalPlayer;
            _playerService.LocalPlayerRemoved += OnLocalPlayer;
            _signalBus.Subscribe<InteractPromptSignal>(OnInteractPrompt);
            _signalBus.Subscribe<SourceStateChangedSignal>(OnSourceState);
            _signalBus.Subscribe<WeaponMountedSignal>(OnWeaponMounted);
            _signalBus.Subscribe<CannonChargeChangedSignal>(OnCannonCharge);
            _signalBus.Subscribe<CannonModulesChangedSignal>(OnCannonModules);
            _signalBus.Subscribe<WaveStartedSignal>(OnWaveStarted);
            _signalBus.Subscribe<WaveClearedSignal>(OnWaveCleared);
            _signalBus.Subscribe<AllWavesClearedSignal>(OnAllWavesCleared);
            _signalBus.Subscribe<MeleePromptSignal>(OnMeleePrompt);
            _signalBus.Subscribe<PlayerDownedSignal>(OnPlayerDowned);
            _signalBus.Subscribe<PlayerRevivedSignal>(OnPlayerRevived);
            _signalBus.Subscribe<CorpseHeldSignal>(OnCorpseHeld);
            Refresh();
        }

        public void Dispose()
        {
            _session.StateChanged -= OnSessionState;
            _playerService.LocalPlayerAssigned -= OnLocalPlayer;
            _playerService.LocalPlayerRemoved -= OnLocalPlayer;
            _signalBus.TryUnsubscribe<InteractPromptSignal>(OnInteractPrompt);
            _signalBus.TryUnsubscribe<SourceStateChangedSignal>(OnSourceState);
            _signalBus.TryUnsubscribe<WeaponMountedSignal>(OnWeaponMounted);
            _signalBus.TryUnsubscribe<CannonChargeChangedSignal>(OnCannonCharge);
            _signalBus.TryUnsubscribe<CannonModulesChangedSignal>(OnCannonModules);
            _signalBus.TryUnsubscribe<WaveStartedSignal>(OnWaveStarted);
            _signalBus.TryUnsubscribe<WaveClearedSignal>(OnWaveCleared);
            _signalBus.TryUnsubscribe<AllWavesClearedSignal>(OnAllWavesCleared);
            _signalBus.TryUnsubscribe<MeleePromptSignal>(OnMeleePrompt);
            _signalBus.TryUnsubscribe<PlayerDownedSignal>(OnPlayerDowned);
            _signalBus.TryUnsubscribe<PlayerRevivedSignal>(OnPlayerRevived);
            _signalBus.TryUnsubscribe<CorpseHeldSignal>(OnCorpseHeld);
        }

        private void OnSessionState(SessionState _) => Refresh();
        private void OnLocalPlayer(ILocalPlayer _) => Refresh();

        private void OnInteractPrompt(InteractPromptSignal s)
            => _view.SetInteractPrompt(s.Show, s.Show ? PromptText(s.Kind) : null);

        private void OnSourceState(SourceStateChangedSignal s)
        {
            _view.SetGather(s.State == SourceState.Gather && s.Total > 0f, s.Remaining, s.Total);
            _sourceOpen = s.State == SourceState.Open;
            UpdateBreakable();
        }

        private void OnCannonCharge(CannonChargeChangedSignal s)
        {
            _charged = s.IsCharged;
            _view.SetCharge(s.Loaded, s.Required);
            UpdateBreakable();
        }

        private void OnCannonModules(CannonModulesChangedSignal s)
        {
            _view.SetModulesWarning(s.UnderAttack, s.Detached);
            _view.SetCannonModules(s.Modules, s.Assembled, s.Total);
        }

        private void OnWaveStarted(WaveStartedSignal s) => _view.SetCannonWaveActive(true);
        private void OnWaveCleared(WaveClearedSignal s) => _view.SetCannonWaveActive(false);
        private void OnAllWavesCleared(AllWavesClearedSignal s) => _view.SetCannonWaveActive(false);

        private void OnMeleePrompt(MeleePromptSignal s) => _view.SetMeleePrompt(s.Show);

        private void OnCorpseHeld(CorpseHeldSignal s) => _view.SetChargePrompt(s.Holding);

        private void OnPlayerDowned(PlayerDownedSignal s)
        {
            if (s.IsLocal) _view.SetDownedSelf(true);
            else _view.SetPartnerDowned(true);
        }

        private void OnPlayerRevived(PlayerRevivedSignal s)
        {
            if (s.IsLocal) _view.SetDownedSelf(false);
            else _view.SetPartnerDowned(false);
        }

        private void OnWeaponMounted(WeaponMountedSignal s) => _view.SetCrosshair(!s.Mounted);

        private void UpdateBreakable() => _view.SetShootNow(_sourceOpen && _charged);

        private static string PromptText(InteractPromptKind kind) => kind switch
        {
            InteractPromptKind.PickUp => "Hold to pick up",
            InteractPromptKind.Drop => "Release to drop",
            InteractPromptKind.PlaceOnSocket => "Release to place",
            InteractPromptKind.Drink => "Зажмите E чтобы выпить",
            _ => "Hold to interact",
        };

        private void Refresh() =>
            _view.SetStatus($"{_session.State} | ClientId: {_session.LocalClientId} | Player: {(_playerService.HasLocalPlayer ? "OK" : "—")}");
    }
}
