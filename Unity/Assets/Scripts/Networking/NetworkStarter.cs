using System;
using FishNet.Managing;
using UnityEngine;


namespace Networking
{
	/// <summary>
	/// Starts the game as host so single-player and couch play run the same code as online.
	/// Launch with -client (and optionally -address) to join a host instead.
	/// Stand-in until there is a main menu and lobby.
	/// </summary>
	[RequireComponent(typeof(NetworkManager))]
	public class NetworkStarter : MonoBehaviour
	{
		[SerializeField]
		private bool startOnPlay = true;

		private NetworkManager networkManager;

		private void Awake()
		{
			networkManager = GetComponent<NetworkManager>();
		}

		private void Start()
		{
			if (!startOnPlay)
				return;

			var args = Environment.GetCommandLineArgs();

			if (Array.IndexOf(args, "-client") >= 0)
			{
				var addressIndex = Array.IndexOf(args, "-address");

				var address = addressIndex >= 0 && addressIndex + 1 < args.Length
					? args[addressIndex + 1]
					: "localhost";

				networkManager.ClientManager.StartConnection(address);

				return;
			}

			networkManager.ServerManager.StartConnection();
			networkManager.ClientManager.StartConnection();
		}
	}
}
