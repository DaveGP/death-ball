using Character.Networking;
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

		[SerializeField]
		private float kickoffLaunchSpeed = 6f;

		[SerializeField]
		private float resetHeight = -5f;

		private readonly SyncVar<CharacterNetworkController> carrier = new();

		private readonly Collider[] overlaps = new Collider[16];

		private Rigidbody rigidbody;

		private Collider ballCollider;

		private int characterMask;

		private Vector3 kickoffPosition;

		private CharacterNetworkController lastThrower;

		private float throwerPickupTime;

		private float carrierSince;

		public CharacterNetworkController Carrier => carrier.Value;

		public bool IsLoose => carrier.Value == null;

		private void Awake()
		{
			rigidbody = GetComponent<Rigidbody>();
			ballCollider = GetComponent<Collider>();

			characterMask = LayerMask.GetMask("Character");

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

			lastThrower = null;

			rigidbody.position = kickoffPosition;
			rigidbody.linearVelocity = Vector3.up * kickoffLaunchSpeed;
		}

		[Server]
		public void Throw(CharacterNetworkController thrower, Vector3 velocity)
		{
			if (carrier.Value != thrower)
				return;

			SetLoose();

			BlockPickup(thrower, throwerPickupDelay);

			rigidbody.position = CarryPosition(thrower);
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

		private void FixedUpdate()
		{
			if (!IsServerInitialized)
				return;

			if (carrier.Value != null)
			{
				rigidbody.MovePosition(CarryPosition(carrier.Value));
				return;
			}

			if (rigidbody.position.y < resetHeight)
			{
				Kickoff();
				return;
			}

			TryPickup();
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

			for (var i = 0; i < count; i++)
			{
				var body = overlaps[i].GetComponentInParent<CharacterNetworkController>();

				if (body == null || !CanPickUp(body))
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

		private bool CanPickUp(CharacterNetworkController body)
			=> body != lastThrower || Time.time >= throwerPickupTime;

		private void SetCarrier(CharacterNetworkController body)
		{
			rigidbody.linearVelocity = Vector3.zero;
			rigidbody.angularVelocity = Vector3.zero;
			rigidbody.isKinematic = true;

			ballCollider.enabled = false;

			carrierSince = Time.time;

			carrier.Value = body;
		}

		private void SetLoose()
		{
			carrier.Value = null;

			rigidbody.isKinematic = false;

			ballCollider.enabled = true;
		}

		private void BlockPickup(CharacterNetworkController body, float delay)
		{
			lastThrower = body;

			throwerPickupTime = Time.time + delay;
		}

		private Vector3 CarryPosition(CharacterNetworkController body)
			=> body.transform.TransformPoint(carryOffset);
	}
}
