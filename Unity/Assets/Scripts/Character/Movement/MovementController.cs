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

		public bool IsKnockedBack => Time.time < knockbackEndTime;

		public CharacterGait Gait { get; private set; }

		public CharacterStats Stats => stats;

		[SerializeField] private CharacterStats stats;

		[SerializeField] private float groundCheckDistance = 0.1f;

		private const float KnockbackDuration = 0.25f;

		private Rigidbody rigidbody;

		private CapsuleCollider capsule;

		// Extract layers to their own class
		private LayerMask groundLayer;

		private float groundCheckRadius => capsule.radius * 0.9f;

		private Vector3 dodgeVelocity;

		private float dodgeEndTime;

		private float nextDodgeTime;

		private bool isCarrying;

		private bool isHasted;

		private Vector3 knockbackVelocity;

		private float knockbackEndTime;


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

		public void SetHasted(bool hasted)
		{
			isHasted = hasted;
		}

		/// <summary>
		/// Throws the character, overriding their own movement for a moment.
		/// </summary>
		public void ApplyKnockback(Vector3 velocity)
		{
			knockbackVelocity = new Vector3(velocity.x, 0f, velocity.z);

			knockbackEndTime = Time.time + KnockbackDuration;

			if (velocity.y > 0f)
			{
				var current = rigidbody.linearVelocity;
				current.y = Mathf.Max(current.y, velocity.y);
				rigidbody.linearVelocity = current;
			}
		}

		public void Move(Vector2 direction)
		{
			var velocity = rigidbody.linearVelocity;

			if (IsKnockedBack)
			{
				velocity.x = knockbackVelocity.x;
				velocity.z = knockbackVelocity.z;

				rigidbody.linearVelocity = velocity;

				return;
			}

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

			if (isCarrying)
				speed *= stats.CarrySpeedMultiplier;

			if (isHasted)
				speed *= stats.HasteSpeedMultiplier;

			return speed;
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

		public bool Dodge(Vector2 direction)
		{
			if (!IsGrounded || Time.time < nextDodgeTime)
				return false;

			var dodgeDirection = direction.sqrMagnitude > 0.0001f
				? new Vector3(direction.x, 0f, direction.y).normalized
				: -transform.forward;

			dodgeVelocity = dodgeDirection * stats.DodgeSpeed;

			dodgeEndTime = Time.time + stats.DodgeDuration;

			nextDodgeTime = Time.time + stats.DodgeCooldown;

			return true;
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
