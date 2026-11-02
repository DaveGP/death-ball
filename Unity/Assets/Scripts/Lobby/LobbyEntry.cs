using Data.Enums;


namespace Lobby
{
	/// <summary>
	/// One local player on one machine, as shown in the lobby.
	/// </summary>
	public struct LobbyEntry
	{
		public int ClientId;

		public int LocalIndex;

		public string Name;

		public TeamSide Team;

		public PlayerType Type;
	}
}
