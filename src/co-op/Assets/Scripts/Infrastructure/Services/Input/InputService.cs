using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace Infrastructure.Services.Input
{
    public sealed class InputService : IInputService, IInitializable
    {
        private bool _hasGeneratedActions;
        private object _generatedControls;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _interactAction;
        private InputAction _fireAction;
        private InputAction _meleeAction;

        public Vector2 MoveAxis { get; private set; }
        public Vector2 LookAxis { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool IsEnabled { get; private set; }

        public event Action<Vector2> MoveChanged;
        public event Action<Vector2> LookChanged;
        public event Action JumpStarted;
        public event Action JumpCanceled;
        public event Action InteractStarted;
        public event Action InteractCanceled;
        public event Action FireStarted;
        public event Action MeleeStarted;

        public void Initialize() => TryBindGeneratedControls();

        public void Enable()
        {
            if (IsEnabled) return;
            if (_hasGeneratedActions)
            {
                _moveAction?.Enable();
                _lookAction?.Enable();
                _jumpAction?.Enable();
                _interactAction?.Enable();
                _fireAction?.Enable();
                _meleeAction?.Enable();
            }
            IsEnabled = true;
        }

        public void Disable()
        {
            if (!IsEnabled) return;
            if (_hasGeneratedActions)
            {
                _moveAction?.Disable();
                _lookAction?.Disable();
                _jumpAction?.Disable();
                _interactAction?.Disable();
                _fireAction?.Disable();
                _meleeAction?.Disable();
            }
            IsEnabled = false;
            MoveAxis = Vector2.zero;
            LookAxis = Vector2.zero;
            JumpHeld = false;
        }

        public void Dispose()
        {
            Disable();
            UnsubscribeAction(_moveAction, OnMovePerformed, OnMovePerformed);
            UnsubscribeAction(_lookAction, OnLookPerformed, OnLookPerformed);
            UnsubscribeAction(_jumpAction, OnJumpPerformed, OnJumpCanceled);
            UnsubscribeAction(_interactAction, OnInteractPerformed, OnInteractCanceled);
            if (_fireAction != null) _fireAction.performed -= OnFirePerformed;
            if (_meleeAction != null) _meleeAction.performed -= OnMeleePerformed;
            if (_generatedControls is IDisposable d) d.Dispose();
            _generatedControls = null;
            _moveAction = null;
            _lookAction = null;
            _jumpAction = null;
            _interactAction = null;
            _fireAction = null;
            _meleeAction = null;
            _hasGeneratedActions = false;
        }

        private void TryBindGeneratedControls()
        {
            var type = FindPlayerControlsType();
            if (type == null)
            {
                Debug.LogWarning("[InputService] Generated PlayerControls type not found. Tried 'CoOp.Input.PlayerControls' and 'PlayerControls' (global namespace) across all loaded assemblies. " +
                                 "Check that the .inputactions asset has 'Generate C# Class' enabled and that the generated PlayerControls.cs has been compiled.");
                return;
            }

            try
            {
                _generatedControls = Activator.CreateInstance(type);
                var gameplay = type.GetProperty("Gameplay")?.GetValue(_generatedControls);
                if (gameplay == null)
                {
                    Debug.LogError("[InputService] PlayerControls has no Gameplay action map.");
                    return;
                }

                _moveAction = gameplay.GetType().GetProperty("Move")?.GetValue(gameplay) as InputAction;
                _lookAction = gameplay.GetType().GetProperty("Look")?.GetValue(gameplay) as InputAction;
                _jumpAction = gameplay.GetType().GetProperty("Jump")?.GetValue(gameplay) as InputAction;

                if (_moveAction == null || _lookAction == null)
                {
                    Debug.LogError("[InputService] Move/Look actions not found on Gameplay map.");
                    return;
                }
                if (_jumpAction == null)
                    Debug.LogWarning("[InputService] Jump action not found on Gameplay map. Add a Jump button (e.g. <Keyboard>/space) to the .inputactions asset.");

                _interactAction = gameplay.GetType().GetProperty("Interact")?.GetValue(gameplay) as InputAction;
                if (_interactAction == null)
                    Debug.LogWarning("[InputService] Interact action not found on Gameplay map. Add an Interact button (e.g. <Keyboard>/e) to the .inputactions asset.");

                _fireAction = gameplay.GetType().GetProperty("Fire")?.GetValue(gameplay) as InputAction;
                if (_fireAction == null)
                    Debug.LogWarning("[InputService] Fire action not found on Gameplay map. Add a Fire button (e.g. <Mouse>/leftButton) to the .inputactions asset to operate the weapon.");

                _meleeAction = gameplay.GetType().GetProperty("Melee")?.GetValue(gameplay) as InputAction;
                if (_meleeAction == null)
                    Debug.LogWarning("[InputService] Melee action not found on Gameplay map. Add a Melee button (e.g. <Keyboard>/f) to the .inputactions asset to bash enemies.");

                SubscribeAction(_moveAction, OnMovePerformed, OnMovePerformed);
                SubscribeAction(_lookAction, OnLookPerformed, OnLookPerformed);
                SubscribeAction(_jumpAction, OnJumpPerformed, OnJumpCanceled);
                SubscribeAction(_interactAction, OnInteractPerformed, OnInteractCanceled);
                if (_fireAction != null) _fireAction.performed += OnFirePerformed;
                if (_meleeAction != null) _meleeAction.performed += OnMeleePerformed;
                _hasGeneratedActions = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputService] Failed to bind generated controls: {ex}");
            }
        }

        private static Type FindPlayerControlsType()
        {
            var candidates = new[]
            {
                "CoOp.Input.PlayerControls, Assembly-CSharp",
                "CoOp.Input.PlayerControls",
                "PlayerControls, Assembly-CSharp",
                "PlayerControls",
            };
            foreach (var name in candidates)
            {
                var t = Type.GetType(name);
                if (t != null) return t;
            }

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types; }
                if (types == null) continue;
                foreach (var t in types)
                {
                    if (t == null) continue;
                    if (t.Name == "PlayerControls" && IsInputControlsType(t))
                        return t;
                }
            }
            return null;
        }

        private static bool IsInputControlsType(Type t)
        {
            foreach (var i in t.GetInterfaces())
                if (i.Name == "IInputActionCollection2" || i.Name == "IInputActionCollection") return true;
            return t.GetProperty("Gameplay") != null;
        }

        private static void SubscribeAction(InputAction action,
            Action<InputAction.CallbackContext> performed,
            Action<InputAction.CallbackContext> canceled)
        {
            if (action == null) return;
            action.performed += performed;
            action.canceled += canceled;
        }

        private static void UnsubscribeAction(InputAction action,
            Action<InputAction.CallbackContext> performed,
            Action<InputAction.CallbackContext> canceled)
        {
            if (action == null) return;
            action.performed -= performed;
            action.canceled -= canceled;
        }

        private void OnMovePerformed(InputAction.CallbackContext ctx)
        {
            MoveAxis = ctx.ReadValue<Vector2>();
            MoveChanged?.Invoke(MoveAxis);
        }

        private void OnLookPerformed(InputAction.CallbackContext ctx)
        {
            LookAxis = ctx.ReadValue<Vector2>();
            LookChanged?.Invoke(LookAxis);
        }

        private void OnJumpPerformed(InputAction.CallbackContext ctx)
        {
            JumpHeld = true;
            JumpStarted?.Invoke();
        }

        private void OnJumpCanceled(InputAction.CallbackContext ctx)
        {
            JumpHeld = false;
            JumpCanceled?.Invoke();
        }

        private void OnInteractPerformed(InputAction.CallbackContext _) => InteractStarted?.Invoke();
        private void OnInteractCanceled(InputAction.CallbackContext _)  => InteractCanceled?.Invoke();
        private void OnFirePerformed(InputAction.CallbackContext _) => FireStarted?.Invoke();
        private void OnMeleePerformed(InputAction.CallbackContext _) => MeleeStarted?.Invoke();
    }
}
