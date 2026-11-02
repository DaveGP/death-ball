using Character.Health;
using Character.Networking;
using Character.Visuals;
using Data.Combat;
using DependencyInjection;
using Match;
using UnityEngine;


namespace Character.Specials
{
	/// <summary>
	/// Slams the ground, hitting every opponent nearby through their guard and knocking
	/// the ball out of a carrier's hands.
	/// </summary>
	[CreateAssetMenu(fileName = "Slam", menuName = "ScriptableObjects/Specials/Slam")]
	public class SlamSpecial : SpecialMove
	{
		public float Radius = 3.5f;

		public Hit Hit = new(12, 1f, true);

		public Color EffectColour = new(1f, 0.6f, 0.1f);

		private readonly Collider[] overlaps = new Collider[16];

		public override void Perform(CharacterNetworkController user)
		{
			var count = Physics.OverlapSphereNonAlloc(
				user.transform.position + Vector3.up,
				Radius,
				overlaps,
				LayerMask.GetMask("Character"),
				QueryTriggerInteraction.Ignore);

			ServiceLocator.TryGet(out Ball ball);

			for (var i = 0; i < count; i++)
			{
				var target = overlaps[i].GetComponentInParent<CharacterNetworkController>();

				if (target == null || target.Team == user.Team || target.IsKnockedOut)
					continue;

				// No attacker, so a special never charges its user's meter towards the next one
				var result = target.GetComponent<CharacterHealth>().TakeHit(Hit);

				// Knocked away from the slam
				if (result == HitResult.Landed && ball != null && ball.Carrier == target)
					ball.Strip(target.transform.position - user.transform.position);
			}
		}

		public override void PlayEffect(Vector3 position)
			=> ExpandingRing.Spawn(position, Radius, EffectColour);
	}
}
