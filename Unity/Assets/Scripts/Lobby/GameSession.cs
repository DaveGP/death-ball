using System;
using System.Linq;
using System.Threading.Tasks;
using DependencyInjection;
using Match;
using Networking;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;


namespace Lobby
{
	/// <summary>
	/// Sits on the session's NetworkManager, which lasts from the menu into the arena and back.
	/// Starts play three ways:
	///
	/// * Local - a host on the offline transport, for solo and couch play with no network at all.
	///   This is also how the browser build plays locally, since browsers can't listen for connections.
	/// * Host online - a Multiplayer Services session over Unity Relay, with a join code to share.
	/// * Join online - into someone's session by its join code.
	///
	/// Online play goes over Relay with secure WebSockets, so browser and desktop players can play
	/// together and nobody needs their own server. Opens the lobby once hosting, takes everyone into
	/// the arena when the host starts the match, and returns to the menu when the session ends.
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

		[SerializeField]
		private int maxPlayers = 8;

		private NetworkManager networkManager;

		private UnityTransport relayTransport;

		private OfflineTransport offlineTransport;

		private ISession session;

		/// <summary>
		/// The code friends type in to join, while hosting online.
		/// </summary>
		public string JoinCode => session?.Code;

		public bool IsOnline => session != null;

		// Host and joiners each pick their own Relay protocol, so both ask for secure WebSockets
		private static NetworkOptions SecureWebSockets => new() { RelayProtocol = RelayProtocol.WSS };

		/// <summary>
		/// What's happening with connecting, for the menu to show.
		/// </summary>
		public string Status { get; private set; } = string.Empty;

		public bool IsBusy { get; private set; }

		private void Awake()
		{
			networkManager = GetComponent<NetworkManager>();

			relayTransport = GetComponent<UnityTransport>();

			offlineTransport = GetComponent<OfflineTransport>();
		}

		private void Start()
		{
			networkManager.OnServerStarted += OnServerStarted;

			networkManager.OnClientStopped += OnClientStopped;
		}

		private void OnDestroy()
		{
			if (networkManager == null)
				return;

			networkManager.OnServerStarted -= OnServerStarted;

			networkManager.OnClientStopped -= OnClientStopped;
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
		/// Hosts with no network, for solo and couch play.
		/// </summary>
		public void PlayLocal()
		{
			networkManager.NetworkConfig.NetworkTransport = offlineTransport;

			Status = networkManager.StartHost() ? string.Empty : "Couldn't start";
		}

		/// <summary>
		/// Hosts an online session over Relay. Friends join with <see cref="JoinCode"/>.
		/// </summary>
		public async Task HostOnline()
		{
			await RunOnline("Creating a game...", async () =>
			{
				var options = new SessionOptions { MaxPlayers = maxPlayers }
					.WithRelayNetwork()
					.WithNetworkOptions(SecureWebSockets);

				session = await MultiplayerService.Instance.CreateSessionAsync(options);
			});
		}

		/// <summary>
		/// Joins a friend's online session by its code.
		/// </summary>
		public async Task JoinOnline(string code)
		{
			await RunOnline($"Joining {code}...", async () =>
			{
				var options = new JoinSessionOptions().WithNetworkOptions(SecureWebSockets);

				session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant(), options);
			});
		}

		/// <summary>
		/// Server only. Takes everyone from the lobby into the arena.
		/// </summary>
		public void StartMatch()
		{
			if (!networkManager.IsServer)
			{
				Debug.LogWarning("GameSession: Only the host can start the match");
				return;
			}

			ServiceLocator.Get<MatchSettings>().FromLobby = true;

			networkManager.SceneManager.LoadScene(arenaSceneName, LoadSceneMode.Single);
		}

		/// <summary>
		/// Leaves the lobby or match: ends the session if hosting, or leaves the host's.
		/// </summary>
		public async void Leave()
		{
			var leaving = session;

			session = null;

			networkManager.Shutdown();

			if (leaving != null)
			{
				try
				{
					await leaving.LeaveAsync();
				}
				catch (Exception exception)
				{
					Debug.LogWarning($"GameSession: Leaving the session failed: {exception.Message}");
				}
			}

			ReturnToMenu();
		}

		// Signs in to Unity Services if needed and switches to the Relay transport, then runs the step
		private async Task RunOnline(string status, Func<Task> step)
		{
			if (IsBusy)
				return;

			IsBusy = true;
			Status = status;

			try
			{
				await SignIn();

				networkManager.NetworkConfig.NetworkTransport = relayTransport;

				// Secure WebSockets, which browsers need and desktops can use too, so both can play together
				relayTransport.UseWebSockets = true;

				await step();

				Status = string.Empty;
			}
			catch (Exception exception)
			{
				session = null;

				Status = $"Couldn't connect: {exception.Message}";

				Debug.LogWarning($"GameSession: {exception}");
			}
			finally
			{
				IsBusy = false;
			}
		}

		// Anonymous sign-in, with a profile of its own per run so two editors on one machine
		// count as different players
		private static async Task SignIn()
		{
			if (UnityServices.State != ServicesInitializationState.Initialized)
			{
				var profile = "p" + Guid.NewGuid().ToString("N")[..20];

				await UnityServices.InitializeAsync(new InitializationOptions().SetProfile(profile));
			}

			if (!AuthenticationService.Instance.IsSignedIn)
				await AuthenticationService.Instance.SignInAnonymouslyAsync();
		}

		private void OnServerStarted()
		{
			if (UnitySceneManager.GetActiveScene().name != menuSceneName)
				return;

			var lobbyState = Instantiate(lobbyStatePrefab);

			lobbyState.Spawn(destroyWithScene: true);
		}

		// The host ended the session, the connection dropped, or we left
		private void OnClientStopped(bool wasHost)
		{
			session = null;

			ServiceLocator.Get<MatchSettings>().FromLobby = false;

			ReturnToMenu();
		}

		private void ReturnToMenu()
		{
			if (UnitySceneManager.GetActiveScene().name != menuSceneName)
				UnitySceneManager.LoadScene(menuSceneName);
		}
	}
}
