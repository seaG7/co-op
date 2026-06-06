using UnityEngine;

namespace Data.Configs
{
    [CreateAssetMenu(menuName = "Configs/Interactable Item Config", fileName = "InteractableItemConfig")]
    public sealed class InteractableItemConfig : ScriptableObject
    {
        [Header("Prefab")]
        [Tooltip("The prefab to spawn for this item. Must have NetworkObject + Rigidbody + Carryable.")]
        public GameObject Prefab;

        [Header("Physics")]
        [Tooltip("Item mass (kg). Applied to the Rigidbody at runtime, also gates player movement speed (heavier = slower).")]
        public float Mass = 2f;

        [Header("Carry")]
        [Tooltip("Player linearVelocity (m/s) above which the held item is forcibly detached. Heavy items should have low MaxCarrySpeed so sprint/jump drops them.")]
        public float MaxCarrySpeed = 5f;
        [Tooltip("Distance from the camera at which the item floats while held (meters). 0 = use CarryConfig default.")]
        public float HoldDistance = 0f;

        [Header("Two-handed")]
        [Tooltip("Number of simultaneous holders required to LIFT this item (1 = one-hand, 2 = two-hand). A 2-holder item can't be lifted by one player. The prefab must have at least this many grab anchors on Carryable.")]
        [Min(1)] public int MinHolders = 1;

        [Header("Fragility")]
        [Tooltip("Collision impulse above which this item fires ItemImpactSignal (audio/VFX). <= 0 falls back to CarryConfig.DefaultFragileImpulse.")]
        public float FragileImpulse = 0f;
    }
}
