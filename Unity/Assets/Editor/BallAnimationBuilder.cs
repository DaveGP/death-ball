using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Builders
{
	/// <summary>Original muscle curves; never samples or copies a licensed animation.</summary>
	public static class BallAnimationBuilder
	{
		public const string Folder = "Assets/Animations/Ball";
		public const float CatchDuration = 0.32f;
		public const float ReleaseDuration = 0.25f;

		public static void AddLayer(AnimatorController controller)
		{
			Directory.CreateDirectory(Folder);
			AssetDatabase.Refresh();
			var hold = Pose(0.15f, 0.5f, -0.55f, 0.5f, -0.55f, 0f);
			var reach = Pose(0.35f, 0.8f, 0.05f, 0.8f, 0.05f, 0f);
			var ready = Pose(0.25f, 0.55f, -0.6f, 0.15f, -0.75f, -0.08f);
			var drawn = Pose(0.45f, 0.65f, -0.5f, -0.65f, -0.7f, -0.3f);
			var forward = Pose(0.45f, 0.6f, -0.25f, 0.9f, 0.65f, 0.18f);
			var follow = Pose(0.05f, 0.35f, -0.45f, 0.65f, 0.8f, 0.12f);
			var catchClip = Clip("BallCatch", CatchDuration, false, new[] { 0f, 0.07f, 0.22f, CatchDuration }, new[] { reach, reach, hold, hold });
			var holdClip = Clip("BallHold", 1f, true, new[] { 0f, 0.5f, 1f }, new[] { hold, Pose(0.17f, 0.51f, -0.54f, 0.51f, -0.54f, 0f), hold });
			var windClip = Clip("BallWindUp", 1f, false, new[] { 0f, 1f }, new[] { ready, drawn });
			var releaseClip = Clip("BallRelease", ReleaseDuration, false, new[] { 0f, 0.08f, ReleaseDuration }, new[] { ready, forward, follow });
			var maskPath = Folder + "/BallUpperBody.mask";
			var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
			if (mask == null)
			{
				mask = new AvatarMask { name = "BallUpperBody" };
				AssetDatabase.CreateAsset(mask, maskPath);
			}
			for (var i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
				mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, i != (int)AvatarMaskBodyPart.Root && i != (int)AvatarMaskBodyPart.LeftLeg && i != (int)AvatarMaskBodyPart.RightLeg && i != (int)AvatarMaskBodyPart.LeftFootIK && i != (int)AvatarMaskBodyPart.RightFootIK);
			EditorUtility.SetDirty(mask);

			controller.AddParameter("BallState", AnimatorControllerParameterType.Int);
			controller.AddParameter("BallCharge", AnimatorControllerParameterType.Float);
			controller.AddLayer("Ball");
			var layers = controller.layers;
			var layer = layers[layers.Length - 1];
			layer.avatarMask = mask;
			layer.blendingMode = AnimatorLayerBlendingMode.Override;
			layer.defaultWeight = 0f;
			layer.iKPass = true;
			controller.layers = layers;
			var machine = layer.stateMachine;
			var clips = new[] { holdClip, catchClip, holdClip, windClip, releaseClip };
			var names = new[] { "None", "Catch", "Hold", "WindUp", "Release" };
			for (var i = 0; i < clips.Length; i++)
			{
				var state = machine.AddState(names[i]);
				state.motion = clips[i];
				state.writeDefaultValues = false;
				if (i == 0) machine.defaultState = state;
				if (i == 3)
				{
					state.timeParameter = "BallCharge";
					state.timeParameterActive = true;
				}
				var transition = machine.AddAnyStateTransition(state);
				transition.AddCondition(AnimatorConditionMode.Equals, i, "BallState");
				transition.hasExitTime = false;
				transition.hasFixedDuration = true;
				transition.duration = i == 1 ? 0.035f : 0.06f;
				transition.canTransitionToSelf = false;
				transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
				transition.orderedInterruption = false;
			}
			EditorUtility.SetDirty(controller);
		}

		private static Dictionary<string, float> Pose(float lift, float leftForward, float leftElbow, float rightForward, float rightElbow, float twist)
		{
			var pose = new Dictionary<string, float>();
			foreach (var muscle in HumanTrait.MuscleName)
			{
				if (muscle.StartsWith("Spine") || muscle.StartsWith("Chest") || muscle.StartsWith("UpperChest") || muscle.StartsWith("Neck") || muscle.StartsWith("Head") || muscle.StartsWith("Left Shoulder") || muscle.StartsWith("Right Shoulder") || muscle.Contains(" Arm ") || muscle.Contains(" Forearm ") || muscle.Contains(" Hand "))
					pose[muscle] = 0f;
			}
			pose["Left Arm Down-Up"] = lift;
			pose["Right Arm Down-Up"] = lift;
			pose["Left Arm Front-Back"] = leftForward;
			pose["Right Arm Front-Back"] = rightForward;
			pose["Left Forearm Stretch"] = leftElbow;
			pose["Right Forearm Stretch"] = rightElbow;
			pose["Left Arm Twist In-Out"] = -0.3f;
			pose["Right Arm Twist In-Out"] = -0.3f;
			pose["Chest Twist Left-Right"] = twist;
			pose["UpperChest Twist Left-Right"] = twist * 0.5f;
			pose["Chest Front-Back"] = -0.03f;
			foreach (var side in new[] { "Left", "Right" })
				foreach (var finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
				{
					for (var joint = 1; joint <= 3; joint++)
						pose[side + "Hand." + finger + "." + joint + " Stretched"] = finger == "Thumb" ? -0.15f : -0.4f;
					pose[side + "Hand." + finger + ".Spread"] = 0f;
				}
			return pose;
		}

		private static AnimationClip Clip(string name, float duration, bool loop, float[] times, Dictionary<string, float>[] poses)
		{
			var path = Folder + "/" + name + ".anim";
			var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
			if (clip == null)
			{
				clip = new AnimationClip { name = name };
				AssetDatabase.CreateAsset(clip, path);
			}
			clip.ClearCurves();
			clip.frameRate = 30f;
			foreach (var muscle in poses[0].Keys)
			{
				var curve = new AnimationCurve();
				for (var i = 0; i < times.Length; i++) curve.AddKey(new Keyframe(times[i], poses[i][muscle]));
				for (var i = 0; i < curve.length; i++)
				{
					AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
					AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
				}
				AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), muscle), curve);
			}
			var settings = AnimationUtility.GetAnimationClipSettings(clip);
			settings.loopTime = loop;
			settings.startTime = 0f;
			settings.stopTime = duration;
			settings.keepOriginalPositionXZ = true;
			settings.keepOriginalPositionY = true;
			settings.keepOriginalOrientation = true;
			AnimationUtility.SetAnimationClipSettings(clip, settings);
			EditorUtility.SetDirty(clip);
			return clip;
		}
	}
}
