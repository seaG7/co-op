using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Data.Players;
using UnityEngine;

namespace Infrastructure.Services.Player
{
    public sealed class PlayerService : IPlayerService
    {
        private readonly List<UniTaskCompletionSource<ILocalPlayer>> _waiters = new();
        private ILocalPlayer _localPlayer;

        public ILocalPlayer LocalPlayer => _localPlayer;
        public bool HasLocalPlayer => _localPlayer != null;

        public event Action<ILocalPlayer> LocalPlayerAssigned;
        public event Action<ILocalPlayer> LocalPlayerRemoved;

        public void RegisterLocalPlayer(ILocalPlayer player)
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

        public void UnregisterLocalPlayer(ILocalPlayer player)
        {
            if (_localPlayer != player) return;
            _localPlayer = null;
            LocalPlayerRemoved?.Invoke(player);

            var snapshot = _waiters.ToArray();
            _waiters.Clear();
            foreach (var w in snapshot) w.TrySetCanceled();
        }

        public UniTask<ILocalPlayer> WaitForLocalPlayerAsync(CancellationToken ct = default)
        {
            if (_localPlayer != null) return UniTask.FromResult(_localPlayer);

            var tcs = new UniTaskCompletionSource<ILocalPlayer>();
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

        private static async UniTask<ILocalPlayer> AwaitWithDispose(
            UniTaskCompletionSource<ILocalPlayer> tcs,
            CancellationTokenRegistration reg)
        {
            try { return await tcs.Task; }
            finally { reg.Dispose(); }
        }
    }
}
