using UnityEngine;
using Zenject;

namespace UI.Common
{
    public abstract class WindowView<TPresenter> : WindowView where TPresenter : class, IPresenter
    {
        [Inject] private DiContainer _container;

        protected TPresenter Presenter { get; private set; }

        public sealed override void BindPresenter()
        {
            if (_container == null)
            {
                Debug.LogError($"[{GetType().Name}] DiContainer is null; cannot bind presenter.");
                return;
            }

            Presenter = _container.Instantiate<TPresenter>(new object[] { this });
            Presenter.Initialize();
            OnBound();
        }

        public sealed override void UnbindPresenter()
        {
            OnUnbinding();
            Presenter?.Dispose();
            Presenter = null;
        }

        protected virtual void OnBound() { }
        protected virtual void OnUnbinding() { }
    }
}
