using Infrastructure.Services.Camera;
using Infrastructure.Services.Carry;
using Infrastructure.Services.DI;
using Infrastructure.Services.Network;
using Infrastructure.Services.Spawn;
using UnityEngine;
using Zenject;

namespace Infrastructure.Installers
{
    public class GameSceneInstaller : MonoInstaller
    {
        [Header("Player")]
        [SerializeField] private GameObject _playerPrefab;

        [Header("Camera")]
        [Tooltip("Game scene camera. If empty, CameraService falls back to Camera.main.")]
        [SerializeField] private UnityEngine.Camera _gameCamera;

        public override void InstallBindings()
        {
            RegisterSceneContainer();
            BindSpawn();
            BindMarkerSpawn();
            BindWeaponBase();
            BindBridge();
            BindCamera();
            BindCarry();
            BindEnemies();
            BindEffects();
            BindExecutionOrders();
        }

        private void BindCarry()
        {

            Container.BindInterfacesAndSelfTo<PhysicalCarryService>().AsSingle().NonLazy();
        }

        private void BindEnemies()
        {
            Container.Bind<Infrastructure.Services.Enemies.IEnemyTargetingService>()
                .To<Infrastructure.Services.Enemies.EnemyTargetingService>().AsSingle();
        }

        private void BindEffects()
        {
            Container.Bind<Infrastructure.Services.Effects.IVfxService>()
                .To<Infrastructure.Services.Effects.VfxService>().AsSingle();
            Container.Bind<Infrastructure.Services.Effects.ISfxService>()
                .To<Infrastructure.Services.Effects.SfxService>().AsSingle();
            Container.BindInterfacesAndSelfTo<Infrastructure.Services.Effects.WorldEffectBindings>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<Infrastructure.Services.Effects.PlayerEffectBindings>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<Infrastructure.Services.Effects.EnemyEffectBindings>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<Infrastructure.Services.Effects.CameraShakeBindings>().AsSingle().NonLazy();
        }

        private void RegisterSceneContainer()
        {

            Container.BindInterfacesAndSelfTo<SceneContainerLifetime>().AsSingle().NonLazy();
        }

        private void BindExecutionOrders()
        {

            Container.BindExecutionOrder<SceneContainerLifetime>(-100);

            Container.BindExecutionOrder<PlayerSpawnService>(-20);
            Container.BindExecutionOrder<WeaponBaseSpawner>(-20);
            Container.BindExecutionOrder<MarkerBasedSpawnService>(-10);
        }

        private void BindMarkerSpawn()
        {
            Container.BindInterfacesAndSelfTo<MarkerBasedSpawnService>().AsSingle().NonLazy();
        }

        private void BindWeaponBase()
        {
            Container.BindInterfacesAndSelfTo<WeaponBaseSpawner>().AsSingle().NonLazy();
        }

        private void BindSpawn()
        {
            Container.Bind<INetworkSpawnService>().To<NetworkSpawnService>().AsSingle();

            if (_playerPrefab == null)
            {
                Debug.LogError("[GameSceneInstaller] Player prefab is not assigned. Spawn will fail.", this);
                Container.BindInterfacesAndSelfTo<PlayerSpawnService>().AsSingle();
            }
            else
            {
                Container.BindInterfacesAndSelfTo<PlayerSpawnService>().AsSingle().WithArguments(_playerPrefab);
            }
        }

        private void BindBridge()
        {
            Container.BindInterfacesAndSelfTo<NetworkEventBridge>().AsSingle().NonLazy();
        }

        private void BindCamera()
        {
            if (_gameCamera != null)
                Container.Bind<UnityEngine.Camera>().FromInstance(_gameCamera).AsSingle();
            Container.Bind<ICameraService>().To<CameraService>().AsSingle();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_playerPrefab == null) Debug.LogWarning("[GameSceneInstaller] _playerPrefab not assigned.", this);
        }
#endif
    }
}
