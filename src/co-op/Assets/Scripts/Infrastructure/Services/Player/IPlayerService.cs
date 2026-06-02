using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Gameplay.Player;

namespace Infrastructure.Services.Player
{
    public interface IPlayerService
    {
        PlayerNetwork LocalPlayer { get; }
        bool HasLocalPlayer { get; }

        event Action<PlayerNetwork> LocalPlayerAssigned;
        event Action<PlayerNetwork> LocalPlayerRemoved;

        UniTask<PlayerNetwork> WaitForLocalPlayerAsync(CancellationToken ct = default);

        void RegisterLocalPlayer(PlayerNetwork player);
        void UnregisterLocalPlayer(PlayerNetwork player);
    }
}
