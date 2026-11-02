using UnityEngine;
using Data.Enums;


namespace Data.Input
{
	/// <summary>
	/// The input a character consumes each fixed tick. Move and Aim are world-space
	/// directions on the XZ plane, so humans and AI produce exactly the same thing.
	/// </summary>
	public readonly struct CharacterInput
	{
		public readonly Vector2 Move;

		public readonly Vector2 Aim;

		public readonly CharacterAction Action;

		public CharacterInput(
			Vector2 move,
			Vector2 aim,
			CharacterAction action)
		{
			Move = move;
			Aim = aim;
			Action = action;
		}

		public static CharacterInput None
			=> new(Vector2.zero, Vector2.zero, CharacterAction.None);
	}
}
