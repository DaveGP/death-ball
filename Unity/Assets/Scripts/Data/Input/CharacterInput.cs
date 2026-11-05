using UnityEngine;
using Data.Enums;
using Unity.Netcode;


namespace Data.Input
{
	/// <summary>
	/// The input a character consumes each fixed tick. Move and Aim are world-space
	/// directions on the XZ plane, so humans and AI produce exactly the same thing.
	/// Sent to the server each tick for bodies a remote player drives.
	/// </summary>
	public struct CharacterInput : INetworkSerializable
	{
		public Vector2 Move;

		public Vector2 Aim;

		public CharacterAction Action;

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

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref Move);
			serializer.SerializeValue(ref Aim);
			serializer.SerializeValue(ref Action);
		}
	}
}
