using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Infrastructure.Services.Scene
{
    public interface ISceneLoaderService
    {
        UniTask LoadSceneAsync(string sceneName,
            LoadSceneMode loadSceneMode = LoadSceneMode.Single,
            CancellationToken ct = default);
    }
}
