using Data.Configs;
using Gameplay.Player;
using Gameplay.Player.Look;
using Gameplay.Player.Movement;
using Infrastructure.Providers.Configs;
using UnityEngine;
using Zenject;

namespace Infrastructure.Installers
{
    public class PlayerInstaller : MonoInstaller
    {
        [Header("Layers")]
        [SerializeField] private LayerMask _groundMask = ~0;

        public override void InstallBindings()
        {
            Container.Bind<PlayerNetwork>().FromComponentOnRoot().AsSingle();
            Container.Bind<PlayerMovement>().FromComponentOnRoot().AsSingle();
            Container.Bind<PlayerLookController>().FromComponentOnRoot().AsSingle();
            Container.Bind<CharacterController>().FromComponentOnRoot().AsSingle();

            Container.Bind<MovementConfig>()
                .FromMethod(ctx => ctx.Container.Resolve<IConfigDataProvider>().Movement)
                .AsSingle();

            Container.Bind<MovementCalculator>().AsSingle();
            Container.Bind<JumpController>().AsSingle();
            Container.Bind<GroundProbe>().AsSingle().WithArguments((LayerMask)_groundMask);
        }
    }
}
