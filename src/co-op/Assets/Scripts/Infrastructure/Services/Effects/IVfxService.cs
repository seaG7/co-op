using UnityEngine;
using Data.Effects;

namespace Infrastructure.Services.Effects
{
    public interface IVfxService
    {
        void Play(VfxId id, Vector3 position, Quaternion rotation = default, Transform parent = null);
        IVfxHandle PlayLoop(VfxId id, Transform follow);
    }
}
