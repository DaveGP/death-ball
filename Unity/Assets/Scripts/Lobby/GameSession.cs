using System.Linq;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Transporting;
using Networking;
using UnityEngine;
using UnityEngine.InputSystem;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;


namespace Lobby
{
	/// <summary>
	/// Sits on the main menu's NetworkManager, which lasts the whole session. Opens the lobby
	/// when hosting starts, takes everyone into the arena when the host starts the match, and
	/// sends a client back to the menu when it loses its connection.
	///
	/// The arena keeps its own NetworkManager so it can still be played directly in the editor.
	/// When it is loaded from here, FishNet discards that duplicate in favour of this one.
	/// </summary>
	[RequireComponent(typeof(NetworkManager))]
	public class GameSession : MonoBehaviour
	{
		[SerializeField]
		private string menuSceneName = "MainMenu";

		[SerializeField]
		private string arenaSceneName = "Arena";

		[SerializeField]
		private NetworkObject lobbyStatePrefab;

		private NetworkManager networkManager;

		private void Awake()
		{
			networkManager = GetComponent<NetworkManager>();
		}

		// Start rather than Awake so the NetworkManager has made its SceneManager
		private void Start()
		{
			// Coming back to the menu unloads FishNet's holding scenes; this processor makes new ones
			networkManager.SceneManager.SetSceneProcessor(gameObject.AddComponent<ReloadSafeSceneProcessor>());

			networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;

			networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
		}

		private void OnDestroy()
		{
			if (networkManager == null)
				return;

			networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;

			networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
		}

		// Escape or a controller's Select leaves a match
		private void Update()
		{
			if (UnitySceneManager.GetActiveScene().name == menuSceneName)
				return;

			var leave = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
			            || Gamepad.all.Any(pad => pad.selectButton.wasPressedThisFrame);

			if (leave)
				Leave();
		}

		/// <summary>
		/// Server only. Takes everyone from the lobby into the arena.
		/// </summary>
		public void StartMatch()
		{
			if (!networkManager.IsServerStarted)
			{
				Debug.LogWarning("GameSession: Only the host can start the match");
				return;
			}

			networkManager.SceneManager.LoadGlobalScenes(new SceneLoadData(arenaSceneName)
			{
				ReplaceScenes = ReplaceOption.All
			});
		}

		/// <summary>
		/// Leaves the lobby or match: stops hosting, or disconnects from the host.
		/// </summary>
		public void Leave()
		{
			if (networkManager.IsServerStarted)
				networkManager.ServerManager.StopConnection(true);

			if (networkManager.IsClientStarted)
				networkManager.ClientManager.StopConnection();
		}

		private void OnServerConnectionState(ServerConnectionStateArgs args)
		{
			if (args.ConnectionState != LocalConnectionState.Started)
				return;

			var lobbyState = networkManager.GetPooledInstantiated(lobbyStatePrefab, true);

			networkManager.ServerManager.Spawn(lobbyState);
		}

		private void OnClientConnectionState(ClientConnectionStateArgs args)
		{
			if (args.ConnectionState != LocalConnectionState.Stopped)
				return;

			if (UnitySceneManager.GetActiveScene().name == menuSceneName)
				return;

			// The menu brings its own NetworkManager, which FishNet discards in favour of this one
			UnitySceneManager.LoadScene(menuSceneName);
		}
	}
}
