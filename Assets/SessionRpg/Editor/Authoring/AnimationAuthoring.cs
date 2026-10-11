using System;
using System.Linq;
using SessionRpg.Editor.Assets;
using SessionRpg.Unity.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SessionRpg.Editor.Authoring
{
    internal static class AnimationAuthoring
    {
        private const string AnimationFolder = AuthoringAssets.ArtFolder + "/Animations";
        private const string GeneralSource =
            "Assets/ThirdParty/KayKit/CharacterAnimations/Rig_Medium/Rig_Medium_General.fbx";
        private const string MovementSource =
            "Assets/ThirdParty/KayKit/CharacterAnimations/Rig_Medium/Rig_Medium_MovementBasic.fbx";
        private const string MeleeSource =
            "Assets/ThirdParty/KayKit/CharacterAnimations/Rig_Medium/Rig_Medium_CombatMelee.fbx";

        public static CombatantAnimationControllers Build()
        {
            EnsureAnimationFolder();

            AnimationClip heroIdle = EnsureClip(
                QuaterniusAssetValidator.AnimationSourcePath,
                "Idle_Loop",
                "Hero_Idle_Loop",
                true);
            AnimationClip heroWalk = EnsureClip(
                QuaterniusAssetValidator.AnimationSourcePath,
                "Walk_Loop",
                "Hero_Walk_Loop",
                true);
            AnimationClip heroRun = EnsureClip(
                QuaterniusAssetValidator.AnimationSourcePath,
                "Jog_Fwd_Loop",
                "Hero_Jog_Fwd_Loop",
                true);
            AnimationClip heroHit = EnsureClip(
                QuaterniusAssetValidator.AnimationSourcePath,
                "Hit_Chest",
                "Hero_Hit_Chest",
                false);
            AnimationClip heroKnockback = EnsureClip(
                QuaterniusAssetValidator.AnimationSourcePath,
                "Hit_Head",
                "Hero_Hit_Head",
                false);
            AnimationClip heroDeath = EnsureClip(
                QuaterniusAssetValidator.AnimationSourcePath,
                "Death01",
                "Hero_Death01",
                false);
            AnimationClip magicBasic = EnsureClip(
                QuaterniusAssetValidator.AnimationSourcePath,
                "Spell_Simple_Shoot",
                "Hero_Spell_Simple_Shoot",
                false);
            AnimationClip magicPower = EnsureClip(
                QuaterniusAssetValidator.AnimationSourcePath,
                "Spell_Simple_Enter",
                "Hero_Spell_Simple_Enter",
                false);
            AnimationClip magicUtility = EnsureClip(
                QuaterniusAssetValidator.AnimationSourcePath,
                "Interact",
                "Hero_Interact",
                false);
            AnimationClip magicGuard = EnsureClip(
                QuaterniusAssetValidator.AnimationSourcePath,
                "Spell_Simple_Idle_Loop",
                "Hero_Spell_Simple_Guard",
                true);
            AnimationClip magicExit = EnsureClip(
                QuaterniusAssetValidator.AnimationSourcePath,
                "Spell_Simple_Exit",
                "Hero_Spell_Simple_Exit",
                false);
            AnimationClip rogueBasic = EnsureClip(
                QuaterniusAssetValidator.AnimationSourcePath,
                "Punch_Jab",
                "Hero_Punch_Jab",
                false);
            AnimationClip roguePower = EnsureClip(
                QuaterniusAssetValidator.AnimationSourcePath,
                "Sword_Attack",
                "Hero_Sword_Attack",
                false);
            AnimationClip rogueUtility = EnsureClip(
                QuaterniusAssetValidator.AnimationSourcePath,
                "Punch_Cross",
                "Hero_Punch_Cross",
                false);
            AnimationClip rogueGuard = EnsureClip(
                QuaterniusAssetValidator.AnimationSourcePath,
                "Sword_Idle",
                "Hero_Sword_Idle",
                false);

            AnimationClip bossIdle = EnsureClip(GeneralSource, "Idle_A", "Boss_Idle_A", true);
            AnimationClip bossWalk = EnsureClip(
                MovementSource,
                "Walking_A",
                "Boss_Walking_A",
                true);
            AnimationClip bossRun = EnsureClip(
                MovementSource,
                "Running_A",
                "Boss_Running_A",
                true);
            AnimationClip bossHit = EnsureClip(GeneralSource, "Hit_A", "Boss_Hit_A", false);
            AnimationClip bossDeath = EnsureClip(GeneralSource, "Death_A", "Boss_Death_A", false);
            AnimationClip bossBasic = EnsureClip(
                MeleeSource,
                "Melee_2H_Attack_Chop",
                "Boss_Melee_2H_Attack_Chop",
                false);
            AnimationClip bossPower = EnsureClip(
                MeleeSource,
                "Melee_2H_Attack_Spin",
                "Boss_Melee_2H_Attack_Spin",
                false);
            AnimationClip bossUtility = EnsureClip(
                MeleeSource,
                "Melee_2H_Attack_Stab",
                "Boss_Melee_2H_Attack_Stab",
                false);
            AnimationClip bossGuard = EnsureClip(
                MeleeSource,
                "Melee_Block",
                "Boss_Melee_Block",
                false);

            AnimatorController healer = BuildController(
                "Healer.controller",
                heroIdle,
                heroWalk,
                heroRun,
                magicBasic,
                magicPower,
                magicUtility,
                magicGuard,
                magicPower,
                magicGuard,
                magicBasic,
                magicExit,
                heroHit,
                heroKnockback,
                heroDeath);
            AnimatorController rogue = BuildController(
                "Rogue.controller",
                heroIdle,
                heroWalk,
                heroRun,
                rogueBasic,
                roguePower,
                rogueUtility,
                rogueGuard,
                magicPower,
                magicGuard,
                magicBasic,
                magicExit,
                heroHit,
                heroKnockback,
                heroDeath);
            AnimatorController mage = BuildController(
                "Mage.controller",
                heroIdle,
                heroWalk,
                heroRun,
                magicBasic,
                magicPower,
                magicUtility,
                magicGuard,
                magicPower,
                magicGuard,
                magicBasic,
                magicExit,
                heroHit,
                heroKnockback,
                heroDeath);
            AnimatorController boss = BuildController(
                "Boss.controller",
                bossIdle,
                bossWalk,
                bossRun,
                bossBasic,
                bossPower,
                bossUtility,
                bossGuard,
                bossPower,
                bossGuard,
                bossBasic,
                bossGuard,
                bossHit,
                bossHit,
                bossDeath);

            AssetDatabase.SaveAssets();
            return new CombatantAnimationControllers(healer, rogue, mage, boss);
        }

        private static AnimationClip EnsureClip(
            string sourcePath,
            string sourceClipName,
            string destinationName,
            bool loop)
        {
            AnimationClip[] matches = AssetDatabase.LoadAllAssetsAtPath(sourcePath)
                .OfType<AnimationClip>()
                .Where(value =>
                    !value.name.StartsWith("__preview__", StringComparison.Ordinal) &&
                    (string.Equals(value.name, sourceClipName, StringComparison.Ordinal) ||
                        value.name.EndsWith("|" + sourceClipName, StringComparison.Ordinal)))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    "Expected exactly one animation clip '" + sourceClipName + "' in '" +
                    sourcePath + "', but found " + matches.Length + ".");
            }

            AnimationClip source = matches[0];
            string destinationPath = AnimationFolder + "/" + destinationName + ".anim";
            AnimationClip destination = AssetDatabase.LoadAssetAtPath<AnimationClip>(destinationPath);
            if (destination == null)
            {
                destination = UnityEngine.Object.Instantiate(source);
                destination.name = destinationName;
                AssetDatabase.CreateAsset(destination, destinationPath);
            }
            else
            {
                EditorUtility.CopySerialized(source, destination);
                destination.name = destinationName;
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(destination);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(destination, settings);
            destination.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
            EditorUtility.SetDirty(destination);
            return destination;
        }

        private static AnimatorController BuildController(
            string fileName,
            AnimationClip idle,
            AnimationClip walk,
            AnimationClip run,
            AnimationClip basic,
            AnimationClip power,
            AnimationClip utility,
            AnimationClip guard,
            AnimationClip castEnter,
            AnimationClip castLoop,
            AnimationClip castRelease,
            AnimationClip castCancel,
            AnimationClip hit,
            AnimationClip knockback,
            AnimationClip death)
        {
            string path = AnimationFolder + "/" + fileName;
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null &&
                !AssetDatabase.DeleteAsset(path))
            {
                throw new InvalidOperationException("Could not rebuild generated Animator at '" + path + "'.");
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter(
                CombatantAnimationPresenter.MoveSpeedParameter,
                AnimatorControllerParameterType.Float);
            controller.AddParameter(CombatantAnimationPresenter.BasicParameter, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(CombatantAnimationPresenter.PowerParameter, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(CombatantAnimationPresenter.UtilityParameter, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(CombatantAnimationPresenter.GuardParameter, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(
                CombatantAnimationPresenter.CastStartParameter,
                AnimatorControllerParameterType.Trigger);
            controller.AddParameter(
                CombatantAnimationPresenter.CastReleaseParameter,
                AnimatorControllerParameterType.Trigger);
            controller.AddParameter(
                CombatantAnimationPresenter.CastCancelParameter,
                AnimatorControllerParameterType.Trigger);
            controller.AddParameter(
                CombatantAnimationPresenter.ChannelPulseParameter,
                AnimatorControllerParameterType.Trigger);
            controller.AddParameter(
                CombatantAnimationPresenter.CastingParameter,
                AnimatorControllerParameterType.Bool);
            controller.AddParameter(
                CombatantAnimationPresenter.HitParameter,
                AnimatorControllerParameterType.Trigger);
            controller.AddParameter(
                CombatantAnimationPresenter.InterruptParameter,
                AnimatorControllerParameterType.Trigger);
            controller.AddParameter(
                CombatantAnimationPresenter.KnockbackParameter,
                AnimatorControllerParameterType.Trigger);
            controller.AddParameter(
                CombatantAnimationPresenter.DeadParameter,
                AnimatorControllerParameterType.Bool);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            stateMachine.name = "Combatant";

            BlendTree locomotionTree = new BlendTree
            {
                name = "LocomotionBlend",
                blendType = BlendTreeType.Simple1D,
                blendParameter = CombatantAnimationPresenter.MoveSpeedParameter,
                useAutomaticThresholds = false,
            };
            AssetDatabase.AddObjectToAsset(locomotionTree, controller);
            locomotionTree.AddChild(idle, 0f);
            locomotionTree.AddChild(walk, 0.45f);
            locomotionTree.AddChild(run, 1f);

            AnimatorState locomotion = stateMachine.AddState("Locomotion");
            locomotion.motion = locomotionTree;
            stateMachine.defaultState = locomotion;

            AnimatorState basicState = stateMachine.AddState("Basic");
            basicState.motion = basic;
            basicState.speed = 1.65f;
            AnimatorState powerState = stateMachine.AddState("Power");
            powerState.motion = power;
            powerState.speed = 1.45f;
            AnimatorState utilityState = stateMachine.AddState("Utility");
            utilityState.motion = utility;
            utilityState.speed = 2.4f;
            AnimatorState guardState = stateMachine.AddState("Guard");
            guardState.motion = guard;
            guardState.speed = 1.65f;
            AnimatorState castEnterState = stateMachine.AddState("CastEnter");
            castEnterState.motion = castEnter;
            castEnterState.speed = 1.2f;
            AnimatorState castLoopState = stateMachine.AddState("CastLoop");
            castLoopState.motion = castLoop;
            AnimatorState castReleaseState = stateMachine.AddState("CastRelease");
            castReleaseState.motion = castRelease;
            castReleaseState.speed = 1.2f;
            AnimatorState castCancelState = stateMachine.AddState("CastCancel");
            castCancelState.motion = castCancel;
            castCancelState.speed = 1.7f;
            AnimatorState hitState = stateMachine.AddState("Hit");
            hitState.motion = hit;
            hitState.speed = 1.2f;
            AnimatorState interruptState = stateMachine.AddState("Interrupt");
            interruptState.motion = knockback;
            interruptState.speed = 1.35f;
            AnimatorState knockbackState = stateMachine.AddState("Knockback");
            knockbackState.motion = knockback;
            knockbackState.speed = 1.05f;
            AnimatorState deathState = stateMachine.AddState("Death");
            deathState.motion = death;
            deathState.speed = 0.92f;

            AnimatorStateTransition deathTransition = stateMachine.AddAnyStateTransition(deathState);
            ConfigureImmediateTransition(deathTransition);
            deathTransition.canTransitionToSelf = false;
            deathTransition.AddCondition(
                AnimatorConditionMode.If,
                0f,
                CombatantAnimationPresenter.DeadParameter);

            AnimatorStateTransition castStartTransition =
                stateMachine.AddAnyStateTransition(castEnterState);
            ConfigureImmediateTransition(castStartTransition);
            castStartTransition.canTransitionToSelf = true;
            castStartTransition.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                CombatantAnimationPresenter.DeadParameter);
            castStartTransition.AddCondition(
                AnimatorConditionMode.If,
                0f,
                CombatantAnimationPresenter.CastStartParameter);

            AnimatorStateTransition castReleaseTransition =
                stateMachine.AddAnyStateTransition(castReleaseState);
            ConfigureImmediateTransition(castReleaseTransition);
            castReleaseTransition.canTransitionToSelf = true;
            castReleaseTransition.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                CombatantAnimationPresenter.DeadParameter);
            castReleaseTransition.AddCondition(
                AnimatorConditionMode.If,
                0f,
                CombatantAnimationPresenter.CastReleaseParameter);

            AnimatorStateTransition castCancelTransition =
                stateMachine.AddAnyStateTransition(castCancelState);
            ConfigureImmediateTransition(castCancelTransition);
            castCancelTransition.canTransitionToSelf = true;
            castCancelTransition.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                CombatantAnimationPresenter.DeadParameter);
            castCancelTransition.AddCondition(
                AnimatorConditionMode.If,
                0f,
                CombatantAnimationPresenter.CastCancelParameter);

            AnimatorStateTransition enterToLoop = castEnterState.AddTransition(castLoopState);
            ConfigureReturnTransition(enterToLoop, 0.82f);
            enterToLoop.AddCondition(
                AnimatorConditionMode.If,
                0f,
                CombatantAnimationPresenter.CastingParameter);

            AnimatorStateTransition hitTransition = stateMachine.AddAnyStateTransition(hitState);
            ConfigureImmediateTransition(hitTransition);
            hitTransition.canTransitionToSelf = false;
            hitTransition.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                CombatantAnimationPresenter.DeadParameter);
            hitTransition.AddCondition(
                AnimatorConditionMode.If,
                0f,
                CombatantAnimationPresenter.HitParameter);

            AnimatorStateTransition interruptTransition =
                stateMachine.AddAnyStateTransition(interruptState);
            ConfigureImmediateTransition(interruptTransition);
            interruptTransition.canTransitionToSelf = true;
            interruptTransition.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                CombatantAnimationPresenter.DeadParameter);
            interruptTransition.AddCondition(
                AnimatorConditionMode.If,
                0f,
                CombatantAnimationPresenter.InterruptParameter);

            AnimatorStateTransition knockbackTransition = stateMachine.AddAnyStateTransition(knockbackState);
            ConfigureImmediateTransition(knockbackTransition);
            knockbackTransition.canTransitionToSelf = false;
            knockbackTransition.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                CombatantAnimationPresenter.DeadParameter);
            knockbackTransition.AddCondition(
                AnimatorConditionMode.If,
                0f,
                CombatantAnimationPresenter.KnockbackParameter);

            ConfigureReturnTransition(castReleaseState.AddTransition(locomotion), 0.8f);
            ConfigureReturnTransition(castCancelState.AddTransition(locomotion), 0.72f);
            ConfigureReactionReturns(hitState, castLoopState, locomotion, 0.78f);
            ConfigureReactionReturns(interruptState, castLoopState, locomotion, 0.88f);
            ConfigureReactionReturns(knockbackState, castLoopState, locomotion, 0.82f);

            BuildUpperBodyActionLayer(
                controller,
                basic,
                power,
                utility,
                guard,
                castRelease);
            if (idle.humanMotion)
            {
                EnableIkPass(controller, 0);
            }

            EditorUtility.SetDirty(locomotionTree);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void EnableIkPass(AnimatorController controller, int layerIndex)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            if (layerIndex < 0 || layerIndex >= layers.Length)
            {
                throw new InvalidOperationException(
                    "Cannot enable animation IK pass for missing layer " + layerIndex + ".");
            }

            AnimatorControllerLayer layer = layers[layerIndex];
            layer.iKPass = true;
            layers[layerIndex] = layer;
            controller.layers = layers;
        }

        private static void ConfigureImmediateTransition(AnimatorStateTransition transition)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0.06f;
        }

        private static void AddActionTransition(
            AnimatorStateMachine stateMachine,
            AnimatorState destination,
            string triggerName)
        {
            AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(destination);
            ConfigureImmediateTransition(transition);
            transition.canTransitionToSelf = true;
            transition.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                CombatantAnimationPresenter.DeadParameter);
            transition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
        }

        private static void ConfigureReturnTransition(
            AnimatorStateTransition transition,
            float exitTime)
        {
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = true;
            transition.duration = 0.08f;
        }

        private static void ConfigureReactionReturns(
            AnimatorState reaction,
            AnimatorState castLoop,
            AnimatorState locomotion,
            float exitTime)
        {
            AnimatorStateTransition resumeCast = reaction.AddTransition(castLoop);
            ConfigureReturnTransition(resumeCast, exitTime);
            resumeCast.AddCondition(
                AnimatorConditionMode.If,
                0f,
                CombatantAnimationPresenter.CastingParameter);

            AnimatorStateTransition returnToLocomotion = reaction.AddTransition(locomotion);
            ConfigureReturnTransition(returnToLocomotion, exitTime);
            returnToLocomotion.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                CombatantAnimationPresenter.CastingParameter);
        }

        private static void BuildUpperBodyActionLayer(
            AnimatorController controller,
            AnimationClip basic,
            AnimationClip power,
            AnimationClip utility,
            AnimationClip guard,
            AnimationClip channelPulse)
        {
            AvatarMask upperBodyMask = new AvatarMask
            {
                name = "UpperBodyMask",
            };
            for (int index = 0; index < (int)AvatarMaskBodyPart.LastBodyPart; index++)
            {
                upperBodyMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)index, false);
            }

            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            AssetDatabase.AddObjectToAsset(upperBodyMask, controller);

            AnimatorStateMachine stateMachine = new AnimatorStateMachine
            {
                name = "UpperBodyActions",
            };
            AssetDatabase.AddObjectToAsset(stateMachine, controller);
            AnimatorControllerLayer layer = new AnimatorControllerLayer
            {
                name = "UpperBodyActions",
                avatarMask = upperBodyMask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 1f,
                iKPass = false,
                syncedLayerIndex = -1,
                stateMachine = stateMachine,
            };
            controller.AddLayer(layer);

            AnimatorState emptyState = stateMachine.AddState("Empty");
            stateMachine.defaultState = emptyState;
            AnimatorState basicState = stateMachine.AddState("BasicAction");
            basicState.motion = basic;
            basicState.speed = 1.75f;
            AnimatorState powerState = stateMachine.AddState("PowerAction");
            powerState.motion = power;
            powerState.speed = 1.55f;
            AnimatorState utilityState = stateMachine.AddState("UtilityAction");
            utilityState.motion = utility;
            utilityState.speed = 2.5f;
            AnimatorState guardState = stateMachine.AddState("GuardAction");
            guardState.motion = guard;
            guardState.speed = 1.85f;
            AnimatorState pulseState = stateMachine.AddState("ChannelPulse");
            pulseState.motion = channelPulse;
            pulseState.speed = 1.9f;

            AddActionTransition(
                stateMachine,
                basicState,
                CombatantAnimationPresenter.BasicParameter);
            AddActionTransition(
                stateMachine,
                powerState,
                CombatantAnimationPresenter.PowerParameter);
            AddActionTransition(
                stateMachine,
                utilityState,
                CombatantAnimationPresenter.UtilityParameter);
            AddActionTransition(
                stateMachine,
                guardState,
                CombatantAnimationPresenter.GuardParameter);
            AddActionTransition(
                stateMachine,
                pulseState,
                CombatantAnimationPresenter.ChannelPulseParameter);

            ConfigureReturnTransition(basicState.AddTransition(emptyState), 0.72f);
            ConfigureReturnTransition(powerState.AddTransition(emptyState), 0.76f);
            ConfigureReturnTransition(utilityState.AddTransition(emptyState), 0.68f);
            ConfigureReturnTransition(guardState.AddTransition(emptyState), 0.62f);
            ConfigureReturnTransition(pulseState.AddTransition(emptyState), 0.7f);

            EditorUtility.SetDirty(upperBodyMask);
            EditorUtility.SetDirty(stateMachine);
        }

        private static void EnsureAnimationFolder()
        {
            if (!AssetDatabase.IsValidFolder(AnimationFolder))
            {
                string guid = AssetDatabase.CreateFolder(AuthoringAssets.ArtFolder, "Animations");
                if (string.IsNullOrEmpty(guid))
                {
                    throw new InvalidOperationException(
                        "Could not create generated animation folder '" + AnimationFolder + "'.");
                }
            }
        }

    }

    internal readonly struct CombatantAnimationControllers
    {
        public CombatantAnimationControllers(
            RuntimeAnimatorController healer,
            RuntimeAnimatorController rogue,
            RuntimeAnimatorController mage,
            RuntimeAnimatorController boss)
        {
            Healer = healer;
            Rogue = rogue;
            Mage = mage;
            Boss = boss;
        }

        public RuntimeAnimatorController Healer { get; }

        public RuntimeAnimatorController Rogue { get; }

        public RuntimeAnimatorController Mage { get; }

        public RuntimeAnimatorController Boss { get; }
    }
}
