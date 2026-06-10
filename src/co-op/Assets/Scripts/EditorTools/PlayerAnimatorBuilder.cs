#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CoOp.EditorTools
{
    // One-shot, re-runnable builder for the samurai PlayerAnimator controller.
    // Builds: locomotion 1D blend tree (Idle->Walk->Run), airborne (Jump->Fall), GettingUp,
    // and an upper-body override layer (Carry/PickUp) with an AvatarMask + IK Pass.
    // Also forces loopTime on the looping locomotion/carry clips of the FBX.
    public static class PlayerAnimatorBuilder
    {
        private const string FbxPath = "Assets/Prefabs/Samurai/SamuraiBoyRed.fbx";
        private const string ControllerPath = "Assets/Prefabs/PlayerAnimator.controller";
        private const string MaskPath = "Assets/Prefabs/UpperBodyMask.mask";

        // clip key (suffix after "Armature|") -> role
        private const string KIdle = "Idle_ok_remap";
        private const string KWalk = "Walk_remap";
        private const string KRun = "Running_remap";
        private const string KJump = "Jumping_remap";
        private const string KFall = "Falling_remap";
        private const string KCarry = "Carrying_remap";
        private const string KPickup = "PickUp_remap";
        private const string KGetUp = "GettingUp_remap";

        [MenuItem("Tools/CoOp/Build Player Animator")]
        public static void Build()
        {
            SetClipLooping();

            var clips = LoadClips();
            AnimationClip Get(string key) =>
                clips.FirstOrDefault(c => c.name == key || c.name.EndsWith("|" + key));

            var idle = Get(KIdle); var walk = Get(KWalk); var run = Get(KRun);
            var jump = Get(KJump); var fall = Get(KFall);
            var carry = Get(KCarry); var pickup = Get(KPickup); var getup = Get(KGetUp);

            if (idle == null || walk == null || run == null)
                Debug.LogError($"[PlayerAnimatorBuilder] Missing locomotion clips (idle/walk/run) in {FbxPath}. " +
                               "Controller will be incomplete.");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                             ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            ClearController(controller);

            // Parameters
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("LocalVelX", AnimatorControllerParameterType.Float);
            controller.AddParameter("LocalVelZ", AnimatorControllerParameterType.Float);
            controller.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsCarrying", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsDowned", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Land", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("PickUp", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("GettingUp", AnimatorControllerParameterType.Trigger);
            controller.AddParameter(new AnimatorControllerParameter { name = "PickUpSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

            // ---- Base layer: locomotion + airborne + getting up ----
            var baseSm = controller.layers[0].stateMachine;

            var blend = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(blend, controller);
            if (idle != null) blend.AddChild(idle, 0f);
            if (walk != null) blend.AddChild(walk, 0.3f);
            if (run != null) blend.AddChild(run, 1f);

            var locoState = baseSm.AddState("Locomotion");
            locoState.motion = blend;
            locoState.writeDefaultValues = true;
            baseSm.defaultState = locoState;

            var jumpState = baseSm.AddState("Jump");
            jumpState.motion = jump;
            var knockdownState = baseSm.AddState("Knockdown");
            knockdownState.motion = fall; // Falling_remap = the fall INTO knockdown when an enemy pounces
            var getUpState = baseSm.AddState("GettingUp");
            getUpState.motion = getup;

            // Locomotion -> Jump (one full jump clip, start to land)
            var tLocoJump = locoState.AddTransition(jumpState);
            tLocoJump.hasExitTime = false; tLocoJump.duration = 0.08f;
            tLocoJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");

            // Jump -> Locomotion (landed)
            var tJumpLoco = jumpState.AddTransition(locoState);
            tJumpLoco.hasExitTime = false; tJumpLoco.duration = 0.12f;
            tJumpLoco.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");

            // AnyState -> Knockdown while downed (enemy pounce); holds last frame
            var tAnyDown = baseSm.AddAnyStateTransition(knockdownState);
            tAnyDown.hasExitTime = false; tAnyDown.duration = 0.08f;
            tAnyDown.canTransitionToSelf = false;
            tAnyDown.AddCondition(AnimatorConditionMode.If, 0, "IsDowned");

            // AnyState -> GettingUp (revive), then back to locomotion
            var tAnyGetUp = baseSm.AddAnyStateTransition(getUpState);
            tAnyGetUp.hasExitTime = false; tAnyGetUp.duration = 0.1f;
            tAnyGetUp.canTransitionToSelf = false;
            tAnyGetUp.AddCondition(AnimatorConditionMode.If, 0, "GettingUp");
            var tGetUpLoco = getUpState.AddTransition(locoState);
            tGetUpLoco.hasExitTime = true; tGetUpLoco.exitTime = 0.9f; tGetUpLoco.duration = 0.15f;

            // ---- Upper-body layer: carry + pickup (override, masked, IK pass) ----
            var mask = BuildUpperBodyMask();
            controller.AddLayer("UpperBody");
            var upperSm = controller.layers[controller.layers.Length - 1].stateMachine;

            var emptyState = upperSm.AddState("Empty");   // no motion -> base layer shows through
            emptyState.writeDefaultValues = true;
            upperSm.defaultState = emptyState;
            var carryState = upperSm.AddState("Carry");
            carryState.motion = carry;
            var pickupState = upperSm.AddState("PickUp");
            pickupState.motion = pickup;
            pickupState.speedParameterActive = true;
            pickupState.speedParameter = "PickUpSpeed";

            var tEmptyCarry = emptyState.AddTransition(carryState);
            tEmptyCarry.hasExitTime = false; tEmptyCarry.duration = 0.2f;
            tEmptyCarry.AddCondition(AnimatorConditionMode.If, 0, "IsCarrying");

            var tCarryEmpty = carryState.AddTransition(emptyState);
            tCarryEmpty.hasExitTime = false; tCarryEmpty.duration = 0.2f;
            tCarryEmpty.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCarrying");

            var tAnyPickup = upperSm.AddAnyStateTransition(pickupState);
            tAnyPickup.hasExitTime = false; tAnyPickup.duration = 0.1f;
            tAnyPickup.canTransitionToSelf = false;
            tAnyPickup.AddCondition(AnimatorConditionMode.If, 0, "PickUp");

            var tPickupCarry = pickupState.AddTransition(carryState);
            tPickupCarry.hasExitTime = true; tPickupCarry.exitTime = 0.7f; tPickupCarry.duration = 0.2f;
            tPickupCarry.AddCondition(AnimatorConditionMode.If, 0, "IsCarrying");
            var tPickupEmpty = pickupState.AddTransition(emptyState);
            tPickupEmpty.hasExitTime = true; tPickupEmpty.exitTime = 0.9f; tPickupEmpty.duration = 0.2f;

            // configure the upper-body layer (mask, override, IK pass, full weight)
            var layers = controller.layers;
            var ub = layers[layers.Length - 1];
            ub.defaultWeight = 1f;
            ub.iKPass = true;
            ub.blendingMode = AnimatorLayerBlendingMode.Override;
            ub.avatarMask = mask;
            layers[layers.Length - 1] = ub;
            controller.layers = layers;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PlayerAnimatorBuilder] PlayerAnimator.controller built " +
                      $"(idle={(idle!=null)}, walk={(walk!=null)}, run={(run!=null)}, jump={(jump!=null)}, " +
                      $"fall={(fall!=null)}, carry={(carry!=null)}, pickup={(pickup!=null)}, getup={(getup!=null)}).");
        }

        private static void ClearController(AnimatorController controller)
        {
            while (controller.parameters.Length > 0)
                controller.RemoveParameter(0);

            while (controller.layers.Length > 1)
                controller.RemoveLayer(controller.layers.Length - 1);

            if (controller.layers.Length == 0)
                controller.AddLayer("Base Layer");

            var sm = controller.layers[0].stateMachine;
            foreach (var t in sm.anyStateTransitions.ToList())
                sm.RemoveAnyStateTransition(t);
            foreach (var cs in sm.states.ToList())
                sm.RemoveState(cs.state);
        }

        private static AnimationClip[] LoadClips()
        {
            return AssetDatabase.LoadAllAssetsAtPath(FbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToArray();
        }

        private static void SetClipLooping()
        {
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[PlayerAnimatorBuilder] No ModelImporter at {FbxPath}.");
                return;
            }

            string[] loopKeys = { KIdle, KWalk, KRun, KCarry };
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;

            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
            {
                bool shouldLoop = loopKeys.Any(k => clips[i].name == k || clips[i].name.EndsWith("|" + k));
                if (shouldLoop && !clips[i].loopTime)
                {
                    clips[i].loopTime = true;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.clipAnimations = clips;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                Debug.Log("[PlayerAnimatorBuilder] Set loopTime on idle/walk/run/carry/fall clips.");
            }
        }

        private static AvatarMask BuildUpperBodyMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            bool isNew = mask == null;
            if (isNew) mask = new AvatarMask();

            void Set(AvatarMaskBodyPart part, bool on) => mask.SetHumanoidBodyPartActive(part, on);
            Set(AvatarMaskBodyPart.Root, false);
            Set(AvatarMaskBodyPart.Body, true);
            Set(AvatarMaskBodyPart.Head, false);
            Set(AvatarMaskBodyPart.LeftLeg, false);
            Set(AvatarMaskBodyPart.RightLeg, false);
            Set(AvatarMaskBodyPart.LeftArm, true);
            Set(AvatarMaskBodyPart.RightArm, true);
            Set(AvatarMaskBodyPart.LeftFingers, true);
            Set(AvatarMaskBodyPart.RightFingers, true);
            Set(AvatarMaskBodyPart.LeftFootIK, false);
            Set(AvatarMaskBodyPart.RightFootIK, false);
            Set(AvatarMaskBodyPart.LeftHandIK, true);
            Set(AvatarMaskBodyPart.RightHandIK, true);

            if (isNew) AssetDatabase.CreateAsset(mask, MaskPath);
            else EditorUtility.SetDirty(mask);
            return mask;
        }
    }
}
#endif
