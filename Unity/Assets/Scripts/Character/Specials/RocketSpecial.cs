using Character.Networking;
using DependencyInjection;
using Match;
using UnityEngine;


namespace Character.Specials
{
	/// <summary>
	/// The Thrower's special. A perfectly aimed shot at the goal ring, fast enough that anyone
	/// in its path gets hit rather than catching it. Needs the ball.
	/// </summary>
	[CreateAssetMenu(fileName = "Rocket", menuName = "ScriptableObjects/Specials/Rocket")]
	public class RocketSpecial : SpecialMove
	{
		public float Speed = 30f;

		// The AI fires it from no further out than this
		public float AIRange = 22f;

		public override bool Perform(CharacterNetworkController user)
		{
			if (!ServiceLocator.TryGet(out Ball ball) || ball.Carrier != user)
				return false;

			var release = ball.HoldPosition(user);

			var toGoal = user.TeamController.AttackTarget.position - release;

			var flat = new Vector3(toGoal.x, 0f, toGoal.z);

			var time = flat.magnitude / Speed;

			// Straight at the centre of the ring, arcing to meet it
			var velocity = flat.normalized * Speed;
			velocity.y = (toGoal.y - 0.5f * Physics.gravity.y * time * time) / time;

			ball.Throw(user, velocity);

			return true;
		}

		public override bool ShouldAIUse(CharacterNetworkController user)
			=> Carrier == user
			   && FlatDistance(user.TeamController.AttackTarget.position, user.transform.position) <= AIRange;
	}
}
