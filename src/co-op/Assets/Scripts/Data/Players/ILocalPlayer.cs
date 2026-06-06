using UnityEngine;

namespace Data.Players
{

    public interface ILocalPlayer
    {
        Transform Transform { get; }
        GameObject GameObject { get; }
    }
}
