using System.Threading;
using Cysharp.Threading.Tasks;
using Data.Configs;
using Infrastructure.Services.UI;
using UnityEngine;

namespace Infrastructure.Providers.Configs
{
    public interface IConfigDataProvider
    {
        UniTask LoadAsync(CancellationToken ct = default);

        WindowsConfig Windows { get; }
        NetworkConfig Network { get; }
        MovementConfig Movement { get; }
        CarryConfig Carry { get; }
        VitalsConfig Vitals { get; }
        WeaponConfig Weapon { get; }

        GameObject GetWindowPrefab(WindowID id);
    }
}
