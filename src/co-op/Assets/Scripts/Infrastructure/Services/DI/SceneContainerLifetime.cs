using System;
using Zenject;

namespace Infrastructure.Services.DI
{

    public sealed class SceneContainerLifetime : IInitializable, IDisposable
    {
        private readonly ISceneDiContainerRegistry _registry;
        private readonly DiContainer _sceneContainer;

        public SceneContainerLifetime(ISceneDiContainerRegistry registry, DiContainer sceneContainer)
        {
            _registry = registry;
            _sceneContainer = sceneContainer;
        }

        public void Initialize() => _registry.SetCurrent(_sceneContainer);

        public void Dispose() => _registry.Clear(_sceneContainer);
    }
}
