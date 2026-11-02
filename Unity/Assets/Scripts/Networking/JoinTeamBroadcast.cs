using Data.Enums;
using FishNet.Broadcast;


namespace Networking
{
	/// <summary>
	/// Sent by a client for each of its local players, asking the server for a body on a team.
	/// </summary>
	public struct JoinTeamBroadcast : IBroadcast
	{
		public int LocalIndex;

		public TeamSide Team;
	}
}
