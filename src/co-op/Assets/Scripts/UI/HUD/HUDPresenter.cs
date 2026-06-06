using Data.Players;
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
            Refresh();
        }

        public void Dispose()
        {
            _session.StateChanged -= OnSessionState;
            _playerService.LocalPlayerAssigned -= OnLocalPlayer;
            _playerService.LocalPlayerRemoved -= OnLocalPlayer;
            _signalBus.TryUnsubscribe<InteractPromptSignal>(OnInteractPrompt);
        }

        private void OnSessionState(SessionState _) => Refresh();
        private void OnLocalPlayer(ILocalPlayer _) => Refresh();

        private void OnInteractPrompt(InteractPromptSignal s)
            => _view.SetInteractPrompt(s.Show, s.Show ? PromptText(s.Kind) : null);

        private static string PromptText(InteractPromptKind kind) => kind switch
        {
            InteractPromptKind.PickUp => "Hold to pick up",
            InteractPromptKind.Drop => "Release to drop",
            InteractPromptKind.PlaceOnSocket => "Release to place",
            _ => "Hold to interact",
        };

        private void Refresh() =>
            _view.SetStatus($"{_session.State} | ClientId: {_session.LocalClientId} | Player: {(_playerService.HasLocalPlayer ? "OK" : "—")}");
    }
}
