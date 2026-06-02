using Zenject;

namespace Signals
{
    public static class SignalBusInstaller
    {
        public static void Install(DiContainer container)
        {
            if (container.HasBinding<SignalBus>()) return;
            container.Bind<SignalBus>().AsSingle();
        }
    }
}
