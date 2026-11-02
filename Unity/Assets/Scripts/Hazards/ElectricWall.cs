using Character.Networking;
using Data.Combat;
using UnityEngine;


namespace Hazards
{
	/// <summary>
	/// An electrified panel on the arena wall. While live, anyone against it is shocked and
	/// thrown back into the arena. Its forward axis points out into the arena.
	/// </summary>
	public class ElectricWall : Hazard
	{
		[SerializeField]
		private Vector2 size = new(8f, 2.5f);

		// How far from the panel the shock reaches
		[SerializeField]
		private float reach = 1f;

		[SerializeField]
		private Hit hit = new(15, 1f, true);

		[SerializeField]
		private float knockback = 8f;

		private readonly Collider[] overlaps = new Collider[16];

		protected override void ApplyEffects()
		{
			var count = Physics.OverlapBoxNonAlloc(
				ZoneCentre,
				new Vector3(size.x / 2f, size.y / 2f, reach / 2f),
				overlaps,
				transform.rotation,
				LayerMask.GetMask("Character"),
				QueryTriggerInteraction.Ignore);

			for (var i = 0; i < count; i++)
			{
				var body = overlaps[i].GetComponentInParent<CharacterNetworkController>();

				if (body != null)
					HitBody(body, hit, transform.forward * knockback + Vector3.up * 2f);
			}
		}

		public override bool IsThreatening(Vector3 position)
		{
			if (State == HazardState.Off)
				return false;

			var local = transform.InverseTransformPoint(position);

			// A margin beyond the reach, to get clear before it goes live
			return Mathf.Abs(local.x) <= size.x / 2f + 0.5f && local.z <= reach + 1f;
		}

		public override Vector2 EscapeDirection(Vector3 position)
			=> new Vector2(transform.forward.x, transform.forward.z).normalized;

		private Vector3 ZoneCentre => transform.position + transform.forward * (reach / 2f);
	}
}
