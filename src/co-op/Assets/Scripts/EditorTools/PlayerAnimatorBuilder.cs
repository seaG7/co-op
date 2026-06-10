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
        private const string KDrinking = "Drinking_remap";
        private const string KDizzyIdle = "DizzyIdle_remap";
        private const string KDrunkWalk = "DrunkWalking_remap";
        private const string KDrunkRun = "DrunkRunning_remap";

        private const string LeftStrafeFbx = "Assets/Prefabs/Samurai/boysamurai_LeftStrafe.fbx";
        private const string RightStrafeFbx = "Assets/Prefabs/Samurai/boysamurai_RightStrafe.fbx";
        private const string LeftStrafeRunFbx = "Assets/Prefabs/Samurai/boysamurai_LeftStrafeRun.fbx";
        private const string RightStrafeRunFbx = "Assets/Prefabs/Samurai/boysamurai_RightStrafeRun.fbx";
        private const string LeftTurnFbx = "Assets/Prefabs/Samurai/boysamurai_LeftTurn.fbx";
        private const string RightTurnFbx = "Assets/Prefabs/Samurai/boysamurai_RightTurn.fbx";
        private const string KickingFbx = "Assets/Prefabs/Samurai/boysamuraiKicking.fbx";

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

            var leftStrafe = LoadClipFrom(LeftStrafeFbx, "LeftStrafe");
            var rightStrafe = LoadClipFrom(RightStrafeFbx, "RightStrafe");
            var leftStrafeRun = LoadClipFrom(LeftStrafeRunFbx, "LeftStrafeRun");
            var rightStrafeRun = LoadClipFrom(RightStrafeRunFbx, "RightStrafeRun");
            var leftTurn = LoadClipFrom(LeftTurnFbx, "LeftTurn");
            var rightTurn = LoadClipFrom(RightTurnFbx, "RightTurn");
            var kicking = LoadClipFrom(KickingFbx, "Kicking");
            var drinking = Get(KDrinking);
            var dizzyIdle = Get(KDizzyIdle);
            var drunkWalk = Get(KDrunkWalk);
            var drunkRun = Get(KDrunkRun);

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
            controller.AddParameter("Turn", AnimatorControllerParameterType.Float);
            controller.AddParameter("Kick", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Drinking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsDrunk", AnimatorControllerParameterType.Bool);

            // ---- Base layer: locomotion + airborne + getting up ----
            var baseSm = controller.layers[0].stateMachine;

            // Idle node = 1D blend on Turn (LeftTurn / Idle / RightTurn) for in-place turning.
            var idleTurn = New1D("IdleTurn", "Turn", controller);
            if (leftTurn != null) idleTurn.AddChild(leftTurn, -1f);
            if (idle != null) idleTurn.AddChild(idle, 0f);
            if (rightTurn != null) idleTurn.AddChild(rightTurn, 1f);

            // Walk ring = 2D directional (forward / strafe left / strafe right; diagonals blend).
            var walkTree = New2D("WalkDir", controller);
            if (walk != null) walkTree.AddChild(walk, new Vector2(0f, 1f));
            if (leftStrafe != null) walkTree.AddChild(leftStrafe, new Vector2(-1f, 0f));
            if (rightStrafe != null) walkTree.AddChild(rightStrafe, new Vector2(1f, 0f));

            // Run ring = 2D directional.
            var runTree = New2D("RunDir", controller);
            if (run != null) runTree.AddChild(run, new Vector2(0f, 1f));
            if (leftStrafeRun != null) runTree.AddChild(leftStrafeRun, new Vector2(-1f, 0f));
            if (rightStrafeRun != null) runTree.AddChild(rightStrafeRun, new Vector2(1f, 0f));

            // Locomotion = 1D blend on Speed: idle(+turn) -> walk ring -> run ring.
            var loco = New1D("Locomotion", "Speed", controller);
            loco.AddChild(idleTurn, 0f);
            loco.AddChild(walkTree, 0.5f);
            loco.AddChild(runTree, 1f);

            var locoState = baseSm.AddState("Locomotion");
            locoState.motion = loco;
            locoState.writeDefaultValues = true;
            baseSm.defaultState = locoState;

            var jumpState = baseSm.AddState("Jump");
            jumpState.motion = jump;
            var knockdownState = baseSm.AddState("Knockdown");
            knockdownState.motion = fall; // Falling_remap = the fall INTO knockdown when an enemy pounces
            var getUpState = baseSm.AddState("GettingUp");
            getUpState.motion = getup;
            var kickState = baseSm.AddState("Kick");
            kickState.motion = kicking;

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

            // AnyState -> Kick (LMB), full-body kick, returns to locomotion
            var tAnyKick = baseSm.AddAnyStateTransition(kickState);
            tAnyKick.hasExitTime = false; tAnyKick.duration = 0.06f;
            tAnyKick.canTransitionToSelf = false;
            tAnyKick.AddCondition(AnimatorConditionMode.If, 0, "Kick");
            var tKickLoco = kickState.AddTransition(locoState);
            tKickLoco.hasExitTime = true; tKickLoco.exitTime = 0.8f; tKickLoco.duration = 0.15f;

            // Drink (hold-to-complete) — full-body, controlled by the Drinking bool
            var drinkState = baseSm.AddState("Drink");
            drinkState.motion = drinking;
            var tAnyDrink = baseSm.AddAnyStateTransition(drinkState);
            tAnyDrink.hasExitTime = false; tAnyDrink.duration = 0.12f; tAnyDrink.canTransitionToSelf = false;
            tAnyDrink.AddCondition(AnimatorConditionMode.If, 0, "Drinking");
            var tDrinkLoco = drinkState.AddTransition(locoState);
            tDrinkLoco.hasExitTime = false; tDrinkLoco.duration = 0.15f;
            tDrinkLoco.AddCondition(AnimatorConditionMode.IfNot, 0, "Drinking");

            // Drunk locomotion (Dizzy idle / drunk walk / drunk run), swapped in while IsDrunk
            var drunkTree = New1D("DrunkLocomotion", "Speed", controller);
            if (dizzyIdle != null) drunkTree.AddChild(dizzyIdle, 0f);
            if (drunkWalk != null) drunkTree.AddChild(drunkWalk, 0.5f);
            if (drunkRun != null) drunkTree.AddChild(drunkRun, 1f);
            var drunkState = baseSm.AddState("DrunkLocomotion");
            drunkState.motion = drunkTree;
            var tLocoDrunk = locoState.AddTransition(drunkState);
            tLocoDrunk.hasExitTime = false; tLocoDrunk.duration = 0.3f;
            tLocoDrunk.AddCondition(AnimatorConditionMode.If, 0, "IsDrunk");
            var tDrunkLoco = drunkState.AddTransition(locoState);
            tDrunkLoco.hasExitTime = false; tDrunkLoco.duration = 0.3f;
            tDrunkLoco.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDrunk");

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
            Debug.Log("[PlayerAnimatorBuilder] built — " +
                      $"loco(idle={(idle!=null)},walk={(walk!=null)},run={(run!=null)}), " +
                      $"strafe(lW={(leftStrafe!=null)},rW={(rightStrafe!=null)},lR={(leftStrafeRun!=null)},rR={(rightStrafeRun!=null)}), " +
                      $"turn(L={(leftTurn!=null)},R={(rightTurn!=null)}), kick={(kicking!=null)}, " +
                      $"drink(drinking={(drinking!=null)},dizzy={(dizzyIdle!=null)},dWalk={(drunkWalk!=null)},dRun={(drunkRun!=null)}), " +
                      $"jump={(jump!=null)},fall={(fall!=null)},carry={(carry!=null)},pickup={(pickup!=null)},getup={(getup!=null)}.");
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

            string[] loopKeys = { KIdle, KWalk, KRun, KCarry, KDizzyIdle, KDrunkWalk, KDrunkRun };
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;

            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
            {
                bool shouldLoop = loopKeys.Any(k => clips[i].name == k || clips[i].name.EndsWith("|" + k));
                if (!shouldLoop) continue;
                if (!clips[i].loopTime || !clips[i].lockRootRotation || !clips[i].keepOriginalOrientation
                    || !clips[i].lockRootHeightY || !clips[i].keepOriginalPositionY)
                {
                    clips[i].loopTime = true;
                    clips[i].lockRootRotation = true;        // bake body facing into the pose (root motion is off)
                    clips[i].keepOriginalOrientation = true;
                    clips[i].lockRootHeightY = true;         // stable foot height
                    clips[i].keepOriginalPositionY = true;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.clipAnimations = clips;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                Debug.Log("[PlayerAnimatorBuilder] Set loopTime on idle/walk/run/carry clips.");
            }

            SetSingleClipLoop(LeftStrafeFbx, true);
            SetSingleClipLoop(RightStrafeFbx, true);
            SetSingleClipLoop(LeftStrafeRunFbx, true);
            SetSingleClipLoop(RightStrafeRunFbx, true);
            SetSingleClipLoop(LeftTurnFbx, true);
            SetSingleClipLoop(RightTurnFbx, true);
            SetSingleClipLoop(KickingFbx, false);
        }

        private static BlendTree New1D(string name, string param, AnimatorController c)
        {
            var t = new BlendTree { name = name, blendType = BlendTreeType.Simple1D, blendParameter = param, useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(t, c);
            return t;
        }

        private static BlendTree New2D(string name, AnimatorController c)
        {
            var t = new BlendTree { name = name, blendType = BlendTreeType.FreeformDirectional2D, blendParameter = "LocalVelX", blendParameterY = "LocalVelZ" };
            AssetDatabase.AddObjectToAsset(t, c);
            return t;
        }

        private static AnimationClip LoadClipFrom(string fbxPath, string clipName)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__") && (c.name == clipName || c.name.EndsWith("|" + clipName)));
            if (clip == null) Debug.LogWarning($"[PlayerAnimatorBuilder] Clip '{clipName}' not found in {fbxPath}.");
            return clip;
        }

        private static void SetSingleClipLoop(string fbxPath, bool loop)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) { Debug.LogWarning($"[PlayerAnimatorBuilder] No ModelImporter at {fbxPath}."); return; }
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (clip.loopTime != loop || !clip.lockRootRotation || !clip.keepOriginalOrientation
                    || !clip.lockRootHeightY || !clip.keepOriginalPositionY)
                {
                    clip.loopTime = loop;
                    clip.lockRootRotation = true;        // bake the strafe/turn body facing into the pose
                    clip.keepOriginalOrientation = true;
                    clip.lockRootHeightY = true;
                    clip.keepOriginalPositionY = true;
                    changed = true;
                }
            }
            if (changed) { importer.clipAnimations = clips; EditorUtility.SetDirty(importer); importer.SaveAndReimport(); }
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
