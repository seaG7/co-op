using System;
using UnityEngine;

namespace Infrastructure.Services.Input
{
    public interface IInputService : IDisposable
    {
        Vector2 MoveAxis { get; }
        Vector2 LookAxis { get; }
        bool JumpHeld { get; }
        bool IsEnabled { get; }

        event Action<Vector2> MoveChanged;
        event Action<Vector2> LookChanged;
        event Action JumpStarted;
        event Action JumpCanceled;
        event Action InteractStarted;
        event Action InteractCanceled;

        void Enable();
        void Disable();
    }
}
