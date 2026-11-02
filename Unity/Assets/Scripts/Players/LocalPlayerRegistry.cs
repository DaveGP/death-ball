using System;
using System.Collections.Generic;
using Character.Networking;


namespace Players
{
	/// <summary>
	/// The humans playing on this machine, indexed by the order they joined.
	/// </summary>
	public class LocalPlayerRegistry
	{
		private readonly List<LocalPlayerController> players = new();

		public IReadOnlyList<LocalPlayerController> Players => players;

		public event Action<LocalPlayerController> PlayerAdded;

		public int Count => players.Count;

		public void Add(LocalPlayerController player)
		{
			players.Add(player);

			PlayerAdded?.Invoke(player);
		}

		public LocalPlayerController Get(int index)
			=> index >= 0 && index < players.Count ? players[index] : null;

		/// <summary>
		/// Unbinds whichever local player was driving a body this machine no longer controls.
		/// </summary>
		public void Release(CharacterNetworkController body)
		{
			foreach (var player in players)
			{
				if (player.Body == body)
					player.Unbind();
			}
		}
	}
}
