using Character.Networking;
using UnityEngine;


namespace AI
{
	/// <summary>
	/// What the team brain wants one AI player to do: a role, plus who or where it's about.
	/// </summary>
	public readonly struct AIOrders
	{
		public readonly AIRole Role;

		// The opponent to mark or hunt, or the teammate to escort
		public readonly CharacterNetworkController Target;

		// Where to stand, for support
		public readonly Vector3 Position;

		public AIOrders(AIRole role, CharacterNetworkController target = null, Vector3 position = default)
		{
			Role = role;
			Target = target;
			Position = position;
		}
	}
}
