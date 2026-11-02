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
	/// The Bruiser's special. Slams the ground, hitting every opponent nearby through their
	/// guard and knocking the ball out of a carrier's hands.
	/// </summary>
	[CreateAssetMenu(fileName = "Slam", menuName = "ScriptableObjects/Specials/Slam")]
	public class SlamSpecial : SpecialMove
	{
		public float Radius = 3.5f;

		public Hit Hit = new(12, 1f, true);

		public float Knockback = 7f;

		// The AI saves it for a crowd, or for the opposing carrier
		public int AICrowd = 2;

		private readonly Collider[] overlaps = new Collider[16];

		public override bool Perform(CharacterNetworkController user)
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

				if (result != HitResult.Landed)
					continue;

				// Thrown away from the slam, dropping the ball
				var away = target.transform.position - user.transform.position;
				away.y = 0f;

				target.Knockback(away.normalized * Knockback + Vector3.up * 3f);

				if (ball != null && ball.Carrier == target)
					ball.Strip(away);
			}

			return true;
		}

		public override bool ShouldAIUse(CharacterNetworkController user)
		{
			var range = Radius - 0.5f;

			return IsOpponentCarrierWithin(user, range) || OpponentsWithin(user, range) >= AICrowd;
		}

		public override void PlayEffect(Vector3 position)
			=> ExpandingRing.Spawn(position, Radius, EffectColour);
	}
}
