using System;
using Data.Enums;
using Unity.Collections;
using Unity.Netcode;


namespace Lobby
{
	/// <summary>
	/// One local player on one machine, as shown in the lobby.
	/// </summary>
	public struct LobbyEntry : IEquatable<LobbyEntry>, INetworkSerializable
	{
		public ulong ClientId;

		public int LocalIndex;

		public FixedString32Bytes Name;

		public TeamSide Team;

		public PlayerType Type;

		public bool Equals(LobbyEntry other)
			=> ClientId == other.ClientId
			   && LocalIndex == other.LocalIndex
			   && Name.Equals(other.Name)
			   && Team == other.Team
			   && Type == other.Type;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref ClientId);
			serializer.SerializeValue(ref LocalIndex);
			serializer.SerializeValue(ref Name);
			serializer.SerializeValue(ref Team);
			serializer.SerializeValue(ref Type);
		}
	}
}
