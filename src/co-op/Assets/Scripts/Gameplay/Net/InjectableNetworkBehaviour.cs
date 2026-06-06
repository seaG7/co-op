using System;
using FishNet.Object;
using Infrastructure.Services.DI;
using UnityEngine;
using Zenject;

namespace Gameplay.Net
{

    public abstract class InjectableNetworkBehaviour : NetworkBehaviour, IRuntimeInjectable
    {
        private bool _injected;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            EnsureInjected();
        }

        public override void OnStopNetwork()
        {

            _injected = false;
            base.OnStopNetwork();
        }

        public void EnsureInjected()
        {
            if (_injected) return;

            var container = ResolveSceneContainer();
            if (container == null)
            {
                Debug.LogWarning(
                    $"[InjectableNetworkBehaviour] No scene DiContainer available to inject '{name}'. " +
                    "Falling back to serialized data. (Is GameSceneInstaller present and ProjectContext alive?)",
                    this);
                return;
            }

            try
            {
                container.InjectGameObject(gameObject);
                _injected = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[InjectableNetworkBehaviour] Injection failed for '{name}': {e.Message}", this);
            }
        }

        public void MarkAlreadyInjected() => _injected = true;

        private static DiContainer ResolveSceneContainer()
        {

            if (!ProjectContext.HasInstance) return null;

            var registry = ProjectContext.Instance.Container.TryResolve<ISceneDiContainerRegistry>();
            return registry?.Current;
        }
    }
}
