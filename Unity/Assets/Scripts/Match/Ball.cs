using Character.Health;
using Character.Networking;
using Data.Combat;
using DependencyInjection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;


namespace Match
{
	/// <summary>
	/// The one steel ball. Owned by the server, which decides who carries it, simulates it
	/// when loose and lets anyone close enough pick it up or catch it. Clients only see the
	/// result, through the carrier SyncVar and the NetworkTransform.
	///
	/// Also remembers the last throw, so the MatchController can tell who scored and how.
	/// </summary>
	[RequireComponent(typeof(Rigidbody))]
	public class Ball : NetworkBehaviour
	{
		[SerializeField]
		private float pickupRadius = 1f;

		// Higher than this the ball is out of reach, so lobs can go over heads
		[SerializeField]
		private float pickupMaxHeight = 2.5f;

		// Stops the thrower catching their own pass straight away
		[SerializeField]
		private float throwerPickupDelay = 0.4f;

		// Stops a carrier who was hit grabbing the ball straight back
		[SerializeField]
		private float fumbleRecoveryDelay = 1f;

		[SerializeField]
		private float knockSpeed = 3f;

		// Gives a new carrier a moment to break away, or two players just strip it back and forth
		[SerializeField]
		private float stripImmunity = 1f;

		[SerializeField]
		private Vector3 carryOffset = new(0f, 1.1f, 0.6f);

		// Thrown faster than this, the ball hits opponents instead of being caught by them
		[SerializeField]
		private float hitSpeed = 16f;

		[SerializeField]
		private Hit ballHit = new(10, 0.6f);

		// Someone the ball just hit can't be hit again this soon, so a shooter can't pin them with rebounds
		[SerializeField]
		private float rehitDelay = 1.5f;

		// Share of its speed the ball keeps when it bounces off someone
		[SerializeField]
		private float deflectionBounce = 0.3f;

		[SerializeField]
		private float kickoffLaunchSpeed = 6f;

		[SerializeField]
		private float resetHeight = -5f;

		private readonly SyncVar<CharacterNetworkController> carrier = new();

		private readonly Collider[] overlaps = new Collider[16];

		private Rigidbody rigidbody;

		private Collider ballCollider;

		private int characterMask;

		private int groundLayer;

		private Vector3 kickoffPosition;

		// Out of play between a goal and the next kickoff
		private bool isLive;

		private CharacterNetworkController blockedBody;

		private float blockedUntil;

		private float carrierSince;

		private CharacterNetworkController lastHitBody;

		private float lastHitTime = float.MinValue;

		// Whether the current carrier caught a teammate's pass before it touched the ground
		private bool carrierCaughtPass;

		private bool touchedGroundSinceThrow;

		public CharacterNetworkController Carrier => carrier.Value;

		public bool IsLoose => carrier.Value == null;

		// The physics position, which the transform trails when interpolating
		public Vector3 Position => rigidbody.position;

		// Server only. Who threw the ball now in flight, or null if it wasn't thrown
		public CharacterNetworkController Thrower { get; private set; }

		public Vector3 ThrowOrigin { get; private set; }

		// The throw came from a player who caught a pass in the air
		public bool ThrowAssisted { get; private set; }

		private void Awake()
		{
			rigidbody = GetComponent<Rigidbody>();
			ballCollider = GetComponent<Collider>();

			characterMask = LayerMask.GetMask("Character");
			groundLayer = LayerMask.NameToLayer("Ground");

			// Players catch the ball by reaching it rather than being hit by it
			Physics.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Character"));
		}

		public override void OnStartNetwork()
		{
			base.OnStartNetwork();

			ServiceLocator.Register(this);
		}

		public override void OnStopNetwork()
		{
			base.OnStopNetwork();

			ServiceLocator.Unregister(this);
		}

		public override void OnStartServer()
		{
			base.OnStartServer();

			kickoffPosition = transform.position;
		}

		public override void OnStartClient()
		{
			base.OnStartClient();

			if (!IsServerInitialized)
				rigidbody.isKinematic = true;
		}

		/// <summary>
		/// Puts the ball back at the centre and launches it into the air.
		/// </summary>
		[Server]
		public void Kickoff()
		{
			SetLoose();

			blockedBody = null;

			isLive = true;

			rigidbody.position = kickoffPosition;
			rigidbody.linearVelocity = Vector3.up * kickoffLaunchSpeed;
		}

		/// <summary>
		/// Takes the ball out of play until the next kickoff. It keeps bouncing but nobody can pick it up.
		/// </summary>
		[Server]
		public void EndPlay()
		{
			SetLoose();

			isLive = false;
		}

		[Server]
		public void Throw(CharacterNetworkController thrower, Vector3 velocity)
		{
			if (carrier.Value != thrower)
				return;

			var assisted = carrierCaughtPass;

			SetLoose();

			BlockPickup(thrower, throwerPickupDelay);

			Thrower = thrower;
			ThrowOrigin = thrower.transform.position;
			ThrowAssisted = assisted;

			touchedGroundSinceThrow = false;

			rigidbody.position = HoldPosition(thrower);
			rigidbody.linearVelocity = velocity;
		}

		/// <summary>
		/// Knocks the ball out of the carrier's hands in a world-space direction,
		/// unless they only just got it.
		/// </summary>
		[Server]
		public bool Strip(Vector3 knockDirection)
		{
			var previousCarrier = carrier.Value;

			if (previousCarrier == null || Time.time < carrierSince + stripImmunity)
				return false;

			SetLoose();

			BlockPickup(previousCarrier, fumbleRecoveryDelay);

			knockDirection.y = 0f;

			rigidbody.linearVelocity = knockDirection.normalized * knockSpeed + Vector3.up * 3f;

			return true;
		}

		/// <summary>
		/// Lets go of the ball whatever happens, for a carrier who has been knocked out.
		/// </summary>
		[Server]
		public void Drop()
		{
			var previousCarrier = carrier.Value;

			if (previousCarrier == null)
				return;

			SetLoose();

			BlockPickup(previousCarrier, fumbleRecoveryDelay);

			rigidbody.linearVelocity = Vector3.up * 3f;
		}

		/// <summary>
		/// Where a body holds the ball, and where throws leave from.
		/// </summary>
		public Vector3 HoldPosition(CharacterNetworkController body)
			=> body.transform.TransformPoint(carryOffset);

		private void FixedUpdate()
		{
			if (!IsServerInitialized)
				return;

			if (carrier.Value != null)
			{
				rigidbody.MovePosition(HoldPosition(carrier.Value));
				return;
			}

			if (rigidbody.position.y < resetHeight)
			{
				Kickoff();
				return;
			}

			if (isLive)
				TryPickup();
		}

		private void OnCollisionEnter(Collision collision)
		{
			if (collision.gameObject.layer == groundLayer)
				touchedGroundSinceThrow = true;
		}

		private void TryPickup()
		{
			if (rigidbody.position.y > pickupMaxHeight)
				return;

			var count = Physics.OverlapSphereNonAlloc(
				rigidbody.position,
				pickupRadius,
				overlaps,
				characterMask,
				QueryTriggerInteraction.Ignore);

			CharacterNetworkController nearest = null;

			var nearestDistance = float.MaxValue;

			var speed = rigidbody.linearVelocity.magnitude;

			for (var i = 0; i < count; i++)
			{
				var body = overlaps[i].GetComponentInParent<CharacterNetworkController>();

				if (body == null)
					continue;

				// Too fast to catch: it hits them, or flies past if they dodged
				if (IsHitBy(body, speed))
				{
					if (TryHit(body))
						return;

					continue;
				}

				if (!CanPickUp(body))
					continue;

				var distance = (body.transform.position - rigidbody.position).sqrMagnitude;

				if (distance >= nearestDistance)
					continue;

				nearest = body;
				nearestDistance = distance;
			}

			if (nearest != null)
				SetCarrier(nearest);
		}

		private bool IsHitBy(CharacterNetworkController body, float speed)
			=> speed >= hitSpeed
			   && Thrower != null
			   && body.Team != Thrower.Team
			   && !body.IsKnockedOut
			   && (body != lastHitBody || Time.time >= lastHitTime + rehitDelay);

		// A shot that hits someone hurts them and bounces off, and can no longer score
		private bool TryHit(CharacterNetworkController body)
		{
			if (body.GetComponent<CharacterHealth>().TakeHit(ballHit, Thrower) == HitResult.Missed)
				return false;

			Thrower = null;

			lastHitBody = body;
			lastHitTime = Time.time;

			BlockPickup(body, fumbleRecoveryDelay);

			rigidbody.linearVelocity = -rigidbody.linearVelocity * deflectionBounce + Vector3.up * 2f;

			return true;
		}

		private bool CanPickUp(CharacterNetworkController body)
			=> !body.IsKnockedOut && (body != blockedBody || Time.time >= blockedUntil);

		private void SetCarrier(CharacterNetworkController body)
		{
			carrierCaughtPass = Thrower != null
			                    && Thrower != body
			                    && Thrower.Team == body.Team
			                    && !touchedGroundSinceThrow;

			rigidbody.linearVelocity = Vector3.zero;
			rigidbody.angularVelocity = Vector3.zero;
			rigidbody.isKinematic = true;

			ballCollider.enabled = false;

			carrierSince = Time.time;

			carrier.Value = body;
		}

		// Clears the last throw too, so only a ball that was thrown can score
		private void SetLoose()
		{
			carrier.Value = null;

			Thrower = null;

			rigidbody.isKinematic = false;

			ballCollider.enabled = true;
		}

		private void BlockPickup(CharacterNetworkController body, float delay)
		{
			blockedBody = body;

			blockedUntil = Time.time + delay;
		}
	}
}
