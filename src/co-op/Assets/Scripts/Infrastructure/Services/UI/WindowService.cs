using Infrastructure.Factories.UI;
using UI.Common;
using UnityEngine;

namespace Infrastructure.Services.UI
{
    public sealed class WindowService : IWindowService
    {
        private readonly IUIFactory _factory;

        public WindowService(IUIFactory factory) => _factory = factory;

        public bool IsWindowOpened(WindowID id) => _factory.Exists(id);

        public WindowView Open(WindowID id) => _factory.CreateScreen(id);

        public T OpenAndGet<T>(WindowID id) where T : WindowView => Open(id) as T;

        public T Get<T>(WindowID id) where T : Component => _factory.GetView<T>(id);

        public void Close(WindowID id) => _factory.DestroyScreen(id);
    }
}
