using Character.Networking;
using Character.Stamina;
using UnityEngine;


namespace Character.Specials
{
	/// <summary>
	/// The Runner's special. Refills stamina and gives a burst of speed with free sprinting.
	/// </summary>
	[CreateAssetMenu(fileName = "SecondWind", menuName = "ScriptableObjects/Specials/Second Wind")]
	public class SecondWindSpecial : SpecialMove
	{
		public float Duration = 5f;

		// The AI uses it to break away with the ball, or to run down the opposing carrier
		public float AIChaseDistance = 6f;

		public override bool Perform(CharacterNetworkController user)
		{
			user.GetComponent<CharacterStamina>().Restore();

			user.Haste(Duration);

			return true;
		}

		public override bool ShouldAIUse(CharacterNetworkController user)
		{
			var carrier = Carrier;

			if (carrier == user)
				return true;

			return carrier != null
			       && carrier.Team != user.Team
			       && FlatDistance(carrier.transform.position, user.transform.position) >= AIChaseDistance;
		}
	}
}
