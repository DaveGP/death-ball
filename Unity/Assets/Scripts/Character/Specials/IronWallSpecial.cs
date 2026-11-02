using Character.Networking;
using UnityEngine;


namespace Character.Specials
{
	/// <summary>
	/// The Enforcer's special. For a while, blocked hits do nothing, even ones that normally
	/// break a guard, and whoever lands one is staggered by it.
	/// </summary>
	[CreateAssetMenu(fileName = "IronWall", menuName = "ScriptableObjects/Specials/Iron Wall")]
	public class IronWallSpecial : SpecialMove
	{
		public float Duration = 5f;

		// The AI uses it when it's in a fight
		public float AIThreatRange = 2.5f;

		public override bool Perform(CharacterNetworkController user)
		{
			user.Fortify(Duration);

			return true;
		}

		public override bool ShouldAIUse(CharacterNetworkController user)
			=> OpponentsWithin(user, AIThreatRange) >= 1;
	}
}
