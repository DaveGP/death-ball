using Character.Networking;
using UnityEngine;


namespace Character.Specials
{
	/// <summary>
	/// A special move, used when the special meter is full. Each one is an asset, so player
	/// types can each be given their own.
	/// </summary>
	public abstract class SpecialMove : ScriptableObject
	{
		public string DisplayName;

		/// <summary>
		/// Server only. Carries out the move for the user.
		/// </summary>
		public abstract void Perform(CharacterNetworkController user);

		/// <summary>
		/// Every machine. Shows the move happening.
		/// </summary>
		public abstract void PlayEffect(Vector3 position);
	}
}
