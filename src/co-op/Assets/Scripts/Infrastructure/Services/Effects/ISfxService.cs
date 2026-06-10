using UnityEngine;
using Data.Effects;

namespace Infrastructure.Services.Effects
{
    public interface ISfxService
    {
        void Play(SfxId id, Vector3 position);
        void Play2D(SfxId id);
        ISfxHandle PlayLoop(SfxId id, Transform follow);
    }
}
