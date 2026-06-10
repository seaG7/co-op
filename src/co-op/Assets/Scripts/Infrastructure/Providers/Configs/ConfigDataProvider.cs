using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Data.Configs;
using Data.Paths;
using Data.UI;
using Infrastructure.Services.UI;
using UnityEngine;

namespace Infrastructure.Providers.Configs
{
    public sealed class ConfigDataProvider : IConfigDataProvider
    {
        public WindowsConfig Windows { get; private set; }
        public NetworkConfig Network { get; private set; }
        public MovementConfig Movement { get; private set; }
        public CarryConfig Carry { get; private set; }
        public VitalsConfig Vitals { get; private set; }
        public WeaponConfig Weapon { get; private set; }
        public VfxCatalog Vfx { get; private set; }
        public SfxCatalog Sfx { get; private set; }

        public async UniTask LoadAsync(CancellationToken ct = default)
        {
            var (windows, network, movement, carry, vitals, weapon) = await UniTask.WhenAll(
                LoadOneAsync<WindowsConfig>(ConfigPaths.WINDOWS_CONFIG_PATH, ct),
                LoadOneAsync<NetworkConfig>(ConfigPaths.NETWORK_CONFIG_PATH, ct),
                LoadOneAsync<MovementConfig>(ConfigPaths.MOVEMENT_CONFIG_PATH, ct),
                LoadOneAsync<CarryConfig>(ConfigPaths.CARRY_CONFIG_PATH, ct),
                LoadOneAsync<VitalsConfig>(ConfigPaths.VITALS_CONFIG_PATH, ct),
                LoadOneAsync<WeaponConfig>(ConfigPaths.WEAPON_CONFIG_PATH, ct));

            Windows = windows; Network = network; Movement = movement; Carry = carry; Vitals = vitals; Weapon = weapon;

            Vfx = await LoadOneAsync<VfxCatalog>(ConfigPaths.VFX_CATALOG_PATH, ct);
            Sfx = await LoadOneAsync<SfxCatalog>(ConfigPaths.SFX_CATALOG_PATH, ct);

            Debug.Log(
                $"[ConfigDataProvider] Loaded — " +
                $"Windows: {(Windows != null ? $"{Windows.windows?.Count ?? 0} entries" : "MISSING")}, " +
                $"Network: {(Network != null ? "ok" : "MISSING")}, " +
                $"Movement: {(Movement != null ? "ok" : "MISSING")}, " +
                $"Carry: {(Carry != null ? "ok" : "MISSING")}, " +
                $"Vitals: {(Vitals != null ? "ok" : "MISSING")}, " +
                $"Weapon: {(Weapon != null ? "ok" : "MISSING")}, " +
                $"Vfx: {(Vfx != null ? "ok" : "MISSING")}, Sfx: {(Sfx != null ? "ok" : "MISSING")}.");
        }

        public GameObject GetWindowPrefab(WindowID id)
        {
            if (Windows == null || Windows.windows == null)
            {
                Debug.LogError($"[ConfigDataProvider] WindowsConfig not loaded; cannot get prefab for {id}.");
                return null;
            }

            var record = Windows.windows.FirstOrDefault(r => r != null && r.windowID == id);
            if (record == null || record.prefab == null)
            {
                Debug.LogError($"[ConfigDataProvider] No prefab for WindowID {id}.");
                return null;
            }

            return record.prefab;
        }

        private static async UniTask<T> LoadOneAsync<T>(string path, CancellationToken ct) where T : ScriptableObject
        {
            var handle = Resources.LoadAsync<T>(path);
            await handle.ToUniTask(cancellationToken: ct);
            var asset = handle.asset as T;
            if (asset == null)
                Debug.LogError($"[ConfigDataProvider] {typeof(T).Name} not found at Resources/{path}.asset");
            return asset;
        }
    }
}
