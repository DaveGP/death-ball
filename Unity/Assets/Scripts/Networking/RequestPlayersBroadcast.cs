using FishNet.Broadcast;


namespace Networking
{
	/// <summary>
	/// Sent by the server once a match is ready, asking a client to join each of its local
	/// players to a team.
	/// </summary>
	public struct RequestPlayersBroadcast : IBroadcast
	{
	}
}
