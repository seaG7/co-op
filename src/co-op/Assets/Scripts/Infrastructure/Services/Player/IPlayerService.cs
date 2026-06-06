using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Data.Players;

namespace Infrastructure.Services.Player
{
    public interface IPlayerService
    {
        ILocalPlayer LocalPlayer { get; }
        bool HasLocalPlayer { get; }

        event Action<ILocalPlayer> LocalPlayerAssigned;
        event Action<ILocalPlayer> LocalPlayerRemoved;

        UniTask<ILocalPlayer> WaitForLocalPlayerAsync(CancellationToken ct = default);

        void RegisterLocalPlayer(ILocalPlayer player);
        void UnregisterLocalPlayer(ILocalPlayer player);
    }
}
