using System;
using System.Linq;
using Character.Input;
using Controls;
using Data.Enums;
using DependencyInjection;
using FishNet;
using FishNet.Transporting;
using Networking;
using SystemManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;


namespace Players
{
	/// <summary>
	/// Press any button to join. Each new device becomes a local player with its own reader.
	/// Players are made in the lobby and carried into the match, so there is one of these for
	/// the whole session; the copy in a game scene loaded from the menu removes itself.
	///
	/// When a match is ready the server asks for this machine's players, and each one asks for
	/// a body on its chosen team, of its chosen type if free. Players who join mid-match ask
	/// straight away.
	/// </summary>
	public class LocalPlayerManager : MonoBehaviour
	{
		private const string NameKey = "DeathBall.PlayerName";

		private static LocalPlayerManager instance;

		[SerializeField]
		private int maxLocalPlayers = 4;

		[SerializeField]
		private TeamSide firstTeam = TeamSide.Home;

		// Couch versus by default: P1 and P3 on one team, P2 and P4 on the other
		[SerializeField]
		private bool alternateTeams = true;

		private LocalPlayerRegistry localPlayers;

		private SystemManager systemManager;

		private IDisposable anyButtonListener;

		// Whether the server has a match waiting for players
		private bool matchWantsPlayers;

		/// <summary>
		/// The name players on this machine go by, remembered between sessions.
		/// </summary>
		public static string PlayerName
		{
			get => PlayerPrefs.GetString(NameKey, $"Player {UnityEngine.Random.Range(100, 1000)}");
			set => PlayerPrefs.SetString(NameKey, value);
		}

		private void Awake()
		{
			if (instance != null)
			{
				Destroy(gameObject);
				return;
			}

			instance = this;

			DontDestroyOnLoad(gameObject);

			localPlayers = ServiceLocator.Get<LocalPlayerRegistry>();

			systemManager = ServiceLocator.Get<SystemManager>();
		}

		// Start rather than OnEnable so the NetworkManager has woken up
		private void Start()
		{
			if (instance != this)
				return;

			anyButtonListener = InputSystem.onAnyButtonPress.Call(OnAnyButtonPress);

			InstanceFinder.ClientManager.RegisterBroadcast<RequestPlayersBroadcast>(OnRequestPlayers);

			InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionState;
		}

		private void OnDestroy()
		{
			if (instance != this)
				return;

			anyButtonListener?.Dispose();

			if (InstanceFinder.ClientManager != null)
			{
				InstanceFinder.ClientManager.UnregisterBroadcast<RequestPlayersBroadcast>(OnRequestPlayers);

				InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionState;
			}

			foreach (var player in localPlayers.Players)
				player.Dispose();
		}

		private void OnAnyButtonPress(InputControl control)
		{
			if (localPlayers.Count >= maxLocalPlayers)
				return;

			var devices = DevicesFor(control.device);

			if (IsInUse(devices))
				return;

			AddPlayer(devices);
		}

		// Keyboard and mouse join together as one player
		private static InputDevice[] DevicesFor(InputDevice device)
		{
			if (device is Keyboard or Mouse)
				return new InputDevice[] { Keyboard.current, Mouse.current }
					.Where(d => d != null)
					.ToArray();

			return new[] { device };
		}

		private bool IsInUse(InputDevice[] devices)
			=> localPlayers.Players.Any(player => player.Devices.Intersect(devices).Any());

		private void AddPlayer(InputDevice[] devices)
		{
			var index = localPlayers.Count;

			var team = alternateTeams && index % 2 == 1 ? Opposite(firstTeam) : firstTeam;

			var name = index == 0 ? PlayerName : $"{PlayerName} ({index + 1})";

			var reader = new UnityInputReader(new PlayerControls(), devices);

			var provider = new InputProvider(reader);

			systemManager.Register(provider);

			var player = new LocalPlayerController(index, name, team, devices, reader, provider)
			{
				// Spread couch players across the types, starting with the Bruiser
				PreferredType = (PlayerType)(index / 2 % 4)
			};

			localPlayers.Add(player);

			Debug.Log($"Local player {index + 1} joined {team} with {string.Join(", ", devices.Select(d => d.displayName))}");

			if (matchWantsPlayers)
				RequestBody(player);
		}

		private void OnRequestPlayers(RequestPlayersBroadcast message, Channel channel)
		{
			matchWantsPlayers = true;

			foreach (var player in localPlayers.Players)
				RequestBody(player);
		}

		// Back in the menu after leaving or losing the server
		private void OnClientConnectionState(ClientConnectionStateArgs args)
		{
			if (args.ConnectionState == LocalConnectionState.Stopped)
				matchWantsPlayers = false;
		}

		private static void RequestBody(LocalPlayerController player)
		{
			InstanceFinder.ClientManager.Broadcast(new JoinTeamBroadcast
			{
				LocalIndex = player.Index,
				Team = player.Team,
				PreferredType = player.PreferredType
			});
		}

		private static TeamSide Opposite(TeamSide side)
			=> side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
	}
}
