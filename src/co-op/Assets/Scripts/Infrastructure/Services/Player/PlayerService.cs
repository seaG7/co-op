using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Gameplay.Player;
using UnityEngine;

namespace Infrastructure.Services.Player
{
    public sealed class PlayerService : IPlayerService
    {
        private readonly List<UniTaskCompletionSource<PlayerNetwork>> _waiters = new();
        private PlayerNetwork _localPlayer;

        public PlayerNetwork LocalPlayer => _localPlayer;
        public bool HasLocalPlayer => _localPlayer != null;

        public event Action<PlayerNetwork> LocalPlayerAssigned;
        public event Action<PlayerNetwork> LocalPlayerRemoved;

        public void RegisterLocalPlayer(PlayerNetwork player)
        {
            if (player == null) { Debug.LogError("[PlayerService] RegisterLocalPlayer received null."); return; }
            if (_localPlayer == player) return;
            if (_localPlayer != null)
                Debug.LogWarning("[PlayerService] Overwriting existing LocalPlayer.");

            _localPlayer = player;
            LocalPlayerAssigned?.Invoke(player);

            var snapshot = _waiters.ToArray();
            _waiters.Clear();
            foreach (var w in snapshot) w.TrySetResult(player);
        }

        public void UnregisterLocalPlayer(PlayerNetwork player)
        {
            if (_localPlayer != player) return;
            _localPlayer = null;
            LocalPlayerRemoved?.Invoke(player);

            var snapshot = _waiters.ToArray();
            _waiters.Clear();
            foreach (var w in snapshot) w.TrySetCanceled();
        }

        public UniTask<PlayerNetwork> WaitForLocalPlayerAsync(CancellationToken ct = default)
        {
            if (_localPlayer != null) return UniTask.FromResult(_localPlayer);

            var tcs = new UniTaskCompletionSource<PlayerNetwork>();
            _waiters.Add(tcs);

            if (!ct.CanBeCanceled) return tcs.Task;

            CancellationTokenRegistration reg = default;
            reg = ct.Register(() =>
            {
                if (tcs.TrySetCanceled())
                    _waiters.Remove(tcs);
                reg.Dispose();
            });

            return AwaitWithDispose(tcs, reg);
        }

        private static async UniTask<PlayerNetwork> AwaitWithDispose(
            UniTaskCompletionSource<PlayerNetwork> tcs,
            CancellationTokenRegistration reg)
        {
            try { return await tcs.Task; }
            finally { reg.Dispose(); }
        }
    }
}
