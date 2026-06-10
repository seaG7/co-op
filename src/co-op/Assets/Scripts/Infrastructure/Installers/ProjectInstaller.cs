using Core.StateMachine;
using Core.States;
using FishNet.Managing;
using Infrastructure.Factories.Objects;
using Infrastructure.Factories.UI;
using Infrastructure.Providers.Configs;
using Infrastructure.Services.DI;
using Infrastructure.Services.Input;
using Infrastructure.Services.Lobby;
using Infrastructure.Services.Network;
using Infrastructure.Services.Player;
using Infrastructure.Services.Round;
using Infrastructure.Services.Scene;
using Infrastructure.Services.UI;
using Signals;
using UnityEngine;
using Zenject;

namespace Infrastructure.Installers
{
    public class ProjectInstaller : MonoInstaller
    {
        [Header("Prefabs")]
        [SerializeField] private NetworkManager _networkManagerPrefab;

        public override void InstallBindings()
        {
            BindSignals();
            BindProviders();
            BindDI();
            BindCoreServices();
            BindUIServices();
            BindInput();
            BindNetwork();
            BindPlayer();
            BindRound();
            BindFactories();
            BindStateMachine();
            BindExecutionOrders();
        }

        private void BindSignals()
        {
            SignalBusInstaller.Install(Container);

            Container.DeclareSignal<ServerStartedSignal>();
            Container.DeclareSignal<ServerStoppedSignal>();
            Container.DeclareSignal<ClientConnectedSignal>();
            Container.DeclareSignal<ClientDisconnectedSignal>();
            Container.DeclareSignal<ConnectionFailedSignal>();
            Container.DeclareSignal<ConnectionLostSignal>();

            Container.DeclareSignal<LocalPlayerSpawnedSignal>();
            Container.DeclareSignal<SpawnFailedSignal>();
            Container.DeclareSignal<GameStartedSignal>();
            Container.DeclareSignal<GameEndedSignal>();

            Container.DeclareSignal<LevelReadySignal>();
            Container.DeclareSignal<ItemImpactSignal>();

            Container.DeclareSignal<WaveStartedSignal>();
            Container.DeclareSignal<WaveClearedSignal>();
            Container.DeclareSignal<AllWavesClearedSignal>();

            Container.DeclareSignal<PlayerDownedSignal>();
            Container.DeclareSignal<PlayerRevivedSignal>();
            Container.DeclareSignal<PlayerDiedSignal>();
            Container.DeclareSignal<AllPlayersDownedOrDeadSignal>();

            Container.DeclareSignal<SourceVulnerableSignal>();
            Container.DeclareSignal<SourceDamagedSignal>();
            Container.DeclareSignal<SourceDestroyedSignal>();
            Container.DeclareSignal<WeaponFiredSignal>();
        }

        private void BindProviders()
        {
            Container.Bind<IConfigDataProvider>().To<ConfigDataProvider>().AsSingle();
        }

        private void BindDI()
        {

            Container.Bind<ISceneDiContainerRegistry>().To<SceneDiContainerRegistry>().AsSingle();
        }

        private void BindCoreServices()
        {
            Container.Bind<ISceneLoaderService>().To<SceneLoaderService>().AsSingle();
            Container.Bind<ILoadingScreenService>().To<LoadingScreenService>().AsSingle();
        }

        private void BindUIServices()
        {
            Container.Bind<IUIFactory>().To<UIFactory>().AsSingle();
            Container.Bind<IWindowService>().To<WindowService>().AsSingle();
        }

        private void BindInput()
        {
            Container.BindInterfacesAndSelfTo<InputService>().AsSingle();
        }

        private void BindNetwork()
        {
            if (_networkManagerPrefab == null)
            {
                Debug.LogError("[ProjectInstaller] NetworkManager prefab is not assigned. Network features will be unavailable until you set it.", this);
            }
            else
            {
                Container.Bind<NetworkManager>()
                    .FromComponentInNewPrefab(_networkManagerPrefab)
                    .AsSingle()
                    .NonLazy();
            }

            Container.BindInterfacesAndSelfTo<NetworkService>().AsSingle();
            Container.BindInterfacesAndSelfTo<SessionService>().AsSingle();
            Container.BindInterfacesAndSelfTo<LobbyService>().AsSingle().NonLazy();
        }

        private void BindPlayer()
        {
            Container.Bind<IPlayerService>().To<PlayerService>().AsSingle();
        }

        private void BindRound()
        {
            Container.BindInterfacesAndSelfTo<RoundService>().AsSingle().NonLazy();
        }

        private void BindFactories()
        {
            Container.Bind<IGameObjectFactory>().To<GameObjectFactory>().AsSingle();
        }

        private void BindStateMachine()
        {
            Container.BindInterfacesAndSelfTo<GameStateMachine>().AsSingle();
            Container.Bind<BootstrapState>().AsTransient();
            Container.Bind<LoadMainMenuState>().AsTransient();
            Container.Bind<MainMenuState>().AsTransient();
            Container.Bind<LobbyState>().AsTransient();
            Container.Bind<LoadGameState>().AsTransient();
            Container.Bind<GameplayState>().AsTransient();
            Container.Bind<GameOverState>().AsTransient();
        }

        private void BindExecutionOrders()
        {
            Container.BindExecutionOrder<NetworkService>(-40);
            Container.BindExecutionOrder<SessionService>(-30);
            Container.BindExecutionOrder<LobbyService>(-20);
        }
    }
}
