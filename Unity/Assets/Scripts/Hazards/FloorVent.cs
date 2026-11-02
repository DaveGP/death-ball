using Character.Networking;
using Data.Combat;
using DependencyInjection;
using Match;
using UnityEngine;


namespace Hazards
{
	/// <summary>
	/// A grate in the floor that blasts upward. Anyone standing on it when it goes off is hurt
	/// and thrown, and a loose ball on it is launched into the air.
	/// </summary>
	public class FloorVent : Hazard
	{
		[SerializeField]
		private float size = 3f;

		[SerializeField]
		private float blastHeight = 3f;

		[SerializeField]
		private Hit hit = new(20, 1.2f, true);

		[SerializeField]
		private float upwardKnockback = 6f;

		[SerializeField]
		private float outwardKnockback = 4f;

		[SerializeField]
		private float ballLaunchSpeed = 12f;

		// The column of fire, shown while the vent is live
		[SerializeField]
		private GameObject blast;

		private readonly Collider[] overlaps = new Collider[16];

		protected override void ShowState()
		{
			base.ShowState();

			blast.SetActive(State == HazardState.Active);
		}

		protected override void ApplyEffects()
		{
			var halfExtents = new Vector3(size / 2f, blastHeight / 2f, size / 2f);

			var centre = transform.position + Vector3.up * (blastHeight / 2f);

			var count = Physics.OverlapBoxNonAlloc(centre, halfExtents, overlaps, Quaternion.identity, LayerMask.GetMask("Character"), QueryTriggerInteraction.Ignore);

			for (var i = 0; i < count; i++)
			{
				var body = overlaps[i].GetComponentInParent<CharacterNetworkController>();

				if (body != null)
					HitBody(body, hit, Outward(body.transform.position) * outwardKnockback + Vector3.up * upwardKnockback);
			}

			// Once, at the start of the blast
			if (TimeInState < Time.fixedDeltaTime && ServiceLocator.TryGet(out Ball ball) && IsOver(ball.Position))
				ball.Launch(Vector3.up * ballLaunchSpeed + Outward(ball.Position) * 2f);
		}

		public override bool IsThreatening(Vector3 position)
			=> State != HazardState.Off && IsOver(position, 0.5f);

		public override Vector2 EscapeDirection(Vector3 position)
		{
			var outward = Outward(position);

			return new Vector2(outward.x, outward.z);
		}

		private bool IsOver(Vector3 position, float margin = 0f)
		{
			var offset = position - transform.position;

			return Mathf.Abs(offset.x) <= size / 2f + margin && Mathf.Abs(offset.z) <= size / 2f + margin;
		}

		private Vector3 Outward(Vector3 position)
		{
			var outward = position - transform.position;
			outward.y = 0f;

			return outward.sqrMagnitude > 0.01f ? outward.normalized : Vector3.right;
		}
	}
}
