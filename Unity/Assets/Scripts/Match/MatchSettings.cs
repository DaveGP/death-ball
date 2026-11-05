namespace Match
{
	/// <summary>
	/// Match options the host picks in the lobby. Read by the server's MatchController when a match starts.
	/// </summary>
	public class MatchSettings
	{
		public static readonly int[] MatchLengths = { 180, 300, 420 };

		public int MatchLengthSeconds { get; set; } = 300;

		// Whether this match was started from the lobby, rather than by playing the arena directly
		public bool FromLobby { get; set; }
	}
}
