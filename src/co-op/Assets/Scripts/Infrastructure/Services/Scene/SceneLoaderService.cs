using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Infrastructure.Services.Scene
{
    public sealed class SceneLoaderService : ISceneLoaderService
    {
        private readonly ILoadingScreenService _loadingScreen;

        public SceneLoaderService(ILoadingScreenService loadingScreen)
        {
            _loadingScreen = loadingScreen;
        }

        public async UniTask LoadSceneAsync(string sceneName,
            LoadSceneMode loadSceneMode = LoadSceneMode.Single,
            CancellationToken ct = default)
        {
            _loadingScreen.Show();
            _loadingScreen.SetProgress(0f);
            await UniTask.NextFrame(ct);

            AsyncOperation op = null;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                op = SceneManager.LoadSceneAsync(sceneName, loadSceneMode);
                if (op != null) break;
                await UniTask.NextFrame(ct);
            }
            if (op == null)
            {
                _loadingScreen.Hide();
                throw new InvalidOperationException(
                    $"[SceneLoaderService] Failed to start load of scene '{sceneName}' after retries. " +
                    "Verify it is in Build Settings and that no other scene load/unload is stuck in progress.");
            }

            try
            {
                while (!op.isDone)
                {
                    _loadingScreen.SetProgress(Mathf.Clamp01(op.progress / 0.9f));
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
                _loadingScreen.SetProgress(1f);
                await UniTask.NextFrame(ct);
            }
            catch
            {
                _loadingScreen.Hide();
                throw;
            }
        }
    }
}
