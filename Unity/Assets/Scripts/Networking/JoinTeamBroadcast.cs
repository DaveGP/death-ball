using Data.Enums;
using FishNet.Broadcast;


namespace Networking
{
	/// <summary>
	/// Sent by a client for each of its local players, asking the server for a body on a
	/// team, of the player's preferred type if one is free.
	/// </summary>
	public struct JoinTeamBroadcast : IBroadcast
	{
		public int LocalIndex;

		public TeamSide Team;

		public PlayerType PreferredType;
	}
}
