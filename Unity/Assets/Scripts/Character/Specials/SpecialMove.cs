using Character.Networking;
using DependencyInjection;
using Match;
using UnityEngine;


namespace Character.Specials
{
	/// <summary>
	/// A special move, used when the special meter is full. Each one is an asset, given to a
	/// player type by its prefab, and knows when the AI should use it.
	/// </summary>
	public abstract class SpecialMove : ScriptableObject
	{
		public string DisplayName;

		public Color EffectColour = Color.white;

		public float EffectRadius = 2f;

		/// <summary>
		/// Server only. Carries out the move for the user. Returns false if it can't be used
		/// right now, in which case the meter is kept.
		/// </summary>
		public abstract bool Perform(CharacterNetworkController user);

		/// <summary>
		/// Server only. Whether an AI with a full meter should use it now.
		/// </summary>
		public abstract bool ShouldAIUse(CharacterNetworkController user);

		/// <summary>
		/// Every machine. Shows the move happening.
		/// </summary>
		public virtual void PlayEffect(Vector3 position)
			=> Visuals.ExpandingRing.Spawn(position, EffectRadius, EffectColour);

		protected static int OpponentsWithin(CharacterNetworkController user, float range)
		{
			var count = 0;

			foreach (var opponent in user.TeamController.Opponents.Bodies)
			{
				if (!opponent.IsKnockedOut && FlatDistance(opponent.transform.position, user.transform.position) <= range)
					count++;
			}

			return count;
		}

		protected static CharacterNetworkController Carrier
			=> ServiceLocator.TryGet(out Ball ball) ? ball.Carrier : null;

		protected static bool IsOpponentCarrierWithin(CharacterNetworkController user, float range)
		{
			var carrier = Carrier;

			return carrier != null
			       && carrier.Team != user.Team
			       && FlatDistance(carrier.transform.position, user.transform.position) <= range;
		}

		protected static float FlatDistance(Vector3 a, Vector3 b)
			=> Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
	}
}
