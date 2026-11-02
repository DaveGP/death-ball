using Data.Enums;
using UnityEngine;


namespace Data.Input
{
	/// <summary>
	/// Raw input read from a device, relative to the stick or keys rather than the world.
	/// </summary>
	public readonly struct InputData
	{
		public readonly Vector2 Move;

		public readonly Vector2 Aim;

		public readonly CharacterAction Action;

		public InputData(
			Vector2 move,
			Vector2 aim,
			CharacterAction action)
		{
			Move = move;
			Aim = aim;
			Action = action;
		}
	}
}
