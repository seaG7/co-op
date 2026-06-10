using FishNet.Object;
using UnityEngine;

namespace Gameplay.Player.View
{
    // First-person body visibility. The face is a SUBMESH of the single body SkinnedMeshRenderer,
    // so it can't be hidden per-renderer; instead the FP camera is placed in front of the eyes
    // (PlayerCameraRig offset) so the head sits behind the camera — out of the owner's view but
    // STILL CASTING ITS SHADOW (no headless silhouette). This component only hides small stray
    // renderers that might poke into view (e.g. eyes), and is a no-op if none are assigned.
    // Remotes are untouched (full head). Restored when the owner is shown (downed/dead view).
    public sealed class PlayerModelVisibility : NetworkBehaviour
    {
        [Tooltip("Optional small renderers hidden for the LOCAL owner in first-person (e.g. eyeballs). " +
                 "Leave empty if the camera placement already keeps the head out of view. Does NOT affect shadows of the rest of the body.")]
        [SerializeField] private Renderer[] _ownerHiddenRenderers;

        private bool _isLocalOwner;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!base.IsOwner) return;
            _isLocalOwner = true;
            SetOwnerHeadVisible(false);
        }

        public void SetOwnerHeadVisible(bool visible)
        {
            if (!_isLocalOwner || _ownerHiddenRenderers == null) return;
            for (int i = 0; i < _ownerHiddenRenderers.Length; i++)
                if (_ownerHiddenRenderers[i] != null) _ownerHiddenRenderers[i].enabled = visible;
        }
    }
}
