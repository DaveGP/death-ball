using Data.Enums;
using Data.ScriptableObjects;
using UnityEngine;


namespace Character.Movement
{
	using Interfaces;


	public class MovementController : MonoBehaviour, IMovementController, ICharacterMovementState
	{
		public Vector3 Velocity => rigidbody.linearVelocity;

		public Vector3 LocalVelocity => transform.InverseTransformDirection(rigidbody.linearVelocity);

		public bool IsGrounded { get; private set; }

		public bool IsDodging => Time.time < dodgeEndTime;

		public CharacterGait Gait { get; private set; }

		[SerializeField] private CharacterStats stats;

		[SerializeField] private float groundCheckDistance = 0.1f;

		private Rigidbody rigidbody;

		private CapsuleCollider capsule;

		// Extract layers to their own class
		private LayerMask groundLayer;

		private float groundCheckRadius => capsule.radius * 0.9f;

		private Vector3 dodgeVelocity;

		private float dodgeEndTime;

		private float nextDodgeTime;

		private bool isCarrying;


		private void Awake()
		{
			rigidbody = GetComponent<Rigidbody>();
			capsule = GetComponent<CapsuleCollider>();

			groundLayer = LayerMask.GetMask("Ground");

			if (stats == null)
				Debug.LogError($"{nameof(stats)} is null", this);
		}

		private void FixedUpdate()
		{
			UpdateGrounded();
		}

		public void SetGait(CharacterGait gait)
		{
			Gait = gait;
		}

		public void SetCarrying(bool carrying)
		{
			isCarrying = carrying;
		}

		public void Move(Vector2 direction)
		{
			var velocity = rigidbody.linearVelocity;

			var horizontal = new Vector3(velocity.x, 0f, velocity.z);

			var target = IsDodging
				? dodgeVelocity
				: new Vector3(direction.x, 0f, direction.y) * CalculateMoveSpeed(Gait);

			var acceleration = IsDodging ? float.MaxValue : stats.Acceleration;

			horizontal = Vector3.MoveTowards(horizontal, target, acceleration * Time.fixedDeltaTime);

			velocity.x = horizontal.x;
			velocity.z = horizontal.z;

			rigidbody.linearVelocity = velocity;
		}

		private float CalculateMoveSpeed(CharacterGait characterGait)
		{
			var speed = characterGait switch
			{
				CharacterGait.Walk => stats.WalkSpeed,
				CharacterGait.Run => stats.RunSpeed,
				CharacterGait.Sprint => stats.SprintSpeed,
				_ => 0
			};

			return isCarrying ? speed * stats.CarrySpeedMultiplier : speed;
		}

		public void Face(Vector2 direction)
		{
			if (direction.sqrMagnitude < 0.0001f)
				return;

			var target = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y));

			var rotation = Quaternion.RotateTowards(
				rigidbody.rotation,
				target,
				stats.TurnSpeed * Time.fixedDeltaTime);

			rigidbody.MoveRotation(rotation);
		}

		public void Dodge(Vector2 direction)
		{
			if (!IsGrounded || Time.time < nextDodgeTime)
				return;

			var dodgeDirection = direction.sqrMagnitude > 0.0001f
				? new Vector3(direction.x, 0f, direction.y).normalized
				: -transform.forward;

			dodgeVelocity = dodgeDirection * stats.DodgeSpeed;

			dodgeEndTime = Time.time + stats.DodgeDuration;

			nextDodgeTime = Time.time + stats.DodgeCooldown;
		}

		private void UpdateGrounded()
		{
			var capsuleCenter = transform.TransformPoint(capsule.center);

			var bottomSphereCentre =
				capsuleCenter -
				transform.up * (capsule.height * 0.5f - capsule.radius);

			IsGrounded = Physics.CheckSphere(
				bottomSphereCentre - transform.up * groundCheckDistance,
				groundCheckRadius,
				groundLayer,
				QueryTriggerInteraction.Ignore
			);
		}
	}
}
