using Zenject;

namespace Infrastructure.Services.DI
{

    public interface ISceneDiContainerRegistry
    {

        DiContainer Current { get; }

        void SetCurrent(DiContainer container);

        void Clear(DiContainer container);
    }
}
