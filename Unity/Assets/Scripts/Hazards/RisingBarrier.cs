using UnityEngine;


namespace Hazards
{
	/// <summary>
	/// A block that rises out of the floor for part of each cycle, closing off that part of the
	/// arena, then sinks again. It moves the same way on every machine from the shared cycle, so
	/// a human's own machine, which moves their body, sees the same wall as the server.
	/// </summary>
	[RequireComponent(typeof(Rigidbody))]
	public class RisingBarrier : Hazard
	{
		[SerializeField]
		private float height = 2f;

		// How long it takes to rise or sink
		[SerializeField]
		private float travelTime = 0.6f;

		private Rigidbody rigidbody;

		private Vector3 loweredPosition;

		protected override void Awake()
		{
			base.Awake();

			rigidbody = GetComponent<Rigidbody>();

			rigidbody.isKinematic = true;

			// Built in its raised position; it starts below the floor
			loweredPosition = transform.position - Vector3.up * height;

			transform.position = loweredPosition;
		}

		protected override void FixedUpdate()
		{
			base.FixedUpdate();

			var raise = State switch
			{
				HazardState.Active when TimeInState > ActiveDuration - travelTime => (ActiveDuration - TimeInState) / travelTime,
				HazardState.Active => Mathf.Clamp01(TimeInState / travelTime),
				// A slight rumble out of the floor as the warning
				HazardState.Warning => 0.08f + Mathf.Sin(Time.time * 40f) * 0.03f,
				_ => 0f
			};

			rigidbody.MovePosition(loweredPosition + Vector3.up * (height * raise));
		}

		// It blocks rather than hurts
		protected override void ApplyEffects()
		{
		}

		public override bool IsThreatening(Vector3 position)
			=> false;

		public override Vector2 EscapeDirection(Vector3 position)
			=> Vector2.zero;
	}
}
