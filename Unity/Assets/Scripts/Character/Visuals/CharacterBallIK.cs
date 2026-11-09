using UnityEngine;

namespace Character.Visuals
{
	/// <summary>Visual-only IK receiver, installed on the runtime Animator's own GameObject.</summary>
	public sealed class CharacterBallIK : MonoBehaviour
	{
		private Animator animator;
		private Transform leftHand, rightHand, leftFinger, rightFinger, leftArm, rightArm, leftForearm, rightForearm, chest, head;
		private int layer = -1;
		private int state;
		private float weight, charge, catchProgress;
		private Vector3 aim;
		private bool aiming;
		private float radius = 0.25f;

		public void Initialise(Animator source, int ballLayer)
		{
			animator = source;
			layer = ballLayer;
			if (animator.avatar == null || !animator.isHuman) return;
			leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
			rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
			leftFinger = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
			rightFinger = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
			leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
			rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
			leftForearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
			rightForearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
			chest = animator.GetBoneTransform(HumanBodyBones.Chest);
			if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Spine);
			head = animator.GetBoneTransform(HumanBodyBones.Head);
		}

		public void SetPose(int ballState, float layerWeight, float shotCharge, float receiveProgress, bool isAiming, Vector3 direction)
		{
			state = ballState;
			weight = layerWeight;
			charge = shotCharge;
			catchProgress = receiveProgress;
			aiming = isAiming;
			aim = direction;
		}

		private bool HasHands => leftHand != null && rightHand != null && leftArm != null && rightArm != null && chest != null;

		private Vector3 Palm(Transform hand, Transform finger)
			=> finger != null ? Vector3.Lerp(hand.position, finger.position, 0.65f) : hand.position;

		public bool TryGetBallPosition(float ballRadius, out Vector3 position)
		{
			radius = ballRadius;
			position = default;
			if (!HasHands || layer < 0 || weight <= 0f) return false;
			var rightPalm = Palm(rightHand, rightFinger);
			var twoHands = (Palm(leftHand, leftFinger) + rightPalm) * 0.5f;
			var oneHand = rightPalm - GripRight() * radius;
			position = state == 3 ? oneHand : twoHands;
			return true;
		}

		private Vector3 GripForward()
		{
			var forward = Vector3.ProjectOnPlane(animator.transform.forward, Vector3.up).normalized;
			if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
			if (!aiming || aim.sqrMagnitude < 0.01f) return forward;
			// The body already turns quickly. Only correct the residual, rather than twisting through 180 degrees.
			return Quaternion.AngleAxis(Mathf.Clamp(Vector3.SignedAngle(forward, aim, Vector3.up), -35f, 35f), Vector3.up) * forward;
		}

		private Vector3 GripRight() => Vector3.Cross(Vector3.up, GripForward()).normalized;

		private void OnAnimatorIK(int layerIndex)
		{
			if (layerIndex != layer || !HasHands) return;
			var forward = GripForward();
			var right = Vector3.Cross(Vector3.up, forward).normalized;
			var shoulder = (leftArm.position + rightArm.position) * 0.5f;
			var shoulderWidth = Vector3.Distance(leftArm.position, rightArm.position);
			var armLength = leftForearm != null && rightForearm != null
				? (Vector3.Distance(leftArm.position, leftForearm.position) + Vector3.Distance(leftForearm.position, leftHand.position) + Vector3.Distance(rightArm.position, rightForearm.position) + Vector3.Distance(rightForearm.position, rightHand.position)) * 0.5f
				: shoulderWidth * 1.4f;
			armLength = Mathf.Max(armLength, shoulderWidth);
			var receive = state == 1 ? 1f - Mathf.SmoothStep(0f, 1f, catchProgress) : 0f;
			var hold = shoulder - Vector3.up * (armLength * 0.3f) + forward * (armLength * Mathf.Lerp(0.65f, 0.95f, receive));
			var drawn = rightArm.position - forward * (armLength * 0.7f) + Vector3.up * (armLength * 0.35f) + right * (armLength * 0.25f);
			var pull = state == 3 ? Mathf.SmoothStep(0f, 1f, charge) : 0f;
			var centre = Vector3.Lerp(hold, drawn, pull);
			var gripWeight = state == 1 || state == 2 || state == 3 ? weight : 0f;
			SetHand(AvatarIKGoal.RightHand, rightHand, rightFinger, centre + right * radius, gripWeight);
			SetHand(AvatarIKGoal.LeftHand, leftHand, leftFinger, centre - right * radius, gripWeight * (1f - pull));
			animator.SetLookAtWeight(aiming ? weight : 0f, 0.35f, 0.7f, 0f, 0.5f);
			if (aiming && head != null) animator.SetLookAtPosition(head.position + forward * (armLength * 4f));
		}

		private void SetHand(AvatarIKGoal goal, Transform hand, Transform finger, Vector3 palmTarget, float ikWeight)
		{
			animator.SetIKPositionWeight(goal, ikWeight);
			animator.SetIKRotationWeight(goal, ikWeight * 0.5f);
			animator.SetIKPosition(goal, palmTarget - (Palm(hand, finger) - hand.position));
			animator.SetIKRotation(goal, hand.rotation);
		}
	}
}
