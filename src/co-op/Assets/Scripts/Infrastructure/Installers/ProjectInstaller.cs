using Core.StateMachine;
using Core.States;
using FishNet.Managing;
using Infrastructure.Factories.Objects;
using Infrastructure.Factories.UI;
using Infrastructure.Providers.Configs;
using Infrastructure.Services.Input;
using Infrastructure.Services.Network;
using Infrastructure.Services.Player;
using Infrastructure.Services.Scene;
using Infrastructure.Services.UI;
using Signals;
using UI.Common;
using UI.MainMenu;
using UI.Connect;
using UI.HUD;
using UI.GameOver;
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
            BindCoreServices();
            BindUIServices();
            BindInput();
            BindNetwork();
            BindPlayer();
            BindFactories();
            BindStateMachine();
            BindPresenters();
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
        }

        private void BindProviders()
        {
            Container.Bind<IConfigDataProvider>().To<ConfigDataProvider>().AsSingle();
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
        }

        private void BindPlayer()
        {
            Container.Bind<IPlayerService>().To<PlayerService>().AsSingle();
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
            Container.Bind<LoadGameState>().AsTransient();
            Container.Bind<GameplayState>().AsTransient();
            Container.Bind<GameOverState>().AsTransient();
        }

        private void BindPresenters()
        {
            Container.Bind<EmptyPresenter>().AsTransient();
            Container.Bind<MainMenuPresenter>().AsTransient();
            Container.Bind<ConnectPresenter>().AsTransient();
            Container.Bind<HUDPresenter>().AsTransient();
            Container.Bind<GameOverPresenter>().AsTransient();
        }

        private void BindExecutionOrders()
        {
            Container.BindExecutionOrder<NetworkService>(-40);
            Container.BindExecutionOrder<SessionService>(-30);
        }
    }
}
