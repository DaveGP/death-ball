using System;
using System.Linq;
using Character.Input;
using Character.Input.Interfaces;
using Controls;
using Data.Enums;
using DependencyInjection;
using FishNet;
using Networking;
using SystemManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;


namespace Players
{
	/// <summary>
	/// Press any button to join. Each new device becomes a local player with its own
	/// reader, and asks the server for a body once the client is connected.
	/// </summary>
	public class LocalPlayerManager : MonoBehaviour
	{
		[SerializeField]
		private int maxLocalPlayers = 4;

		[SerializeField]
		private TeamSide firstTeam = TeamSide.Home;

		// Couch versus by default: P1 and P3 on one team, P2 and P4 on the other
		[SerializeField]
		private bool alternateTeams = true;

		[SerializeField]
		private MonoBehaviour inputView;

		private LocalPlayerRegistry localPlayers;

		private SystemManager systemManager;

		private IDisposable anyButtonListener;

		private void Awake()
		{
			localPlayers = ServiceLocator.Get<LocalPlayerRegistry>();

			systemManager = ServiceLocator.Get<SystemManager>();

			if (inputView is not IInputView)
				Debug.LogError($"{nameof(inputView)} must implement {nameof(IInputView)}", this);
		}

		// Start rather than OnEnable so the NetworkManager has woken up
		private void Start()
		{
			anyButtonListener = InputSystem.onAnyButtonPress.Call(OnAnyButtonPress);

			InstanceFinder.ClientManager.OnAuthenticated += OnAuthenticated;
		}

		private void OnDestroy()
		{
			anyButtonListener?.Dispose();

			if (InstanceFinder.ClientManager != null)
				InstanceFinder.ClientManager.OnAuthenticated -= OnAuthenticated;

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

			var reader = new UnityInputReader(new PlayerControls(), devices);

			var provider = new InputProvider(reader, (IInputView)inputView);

			systemManager.Register(provider);

			var player = new LocalPlayerController(index, team, devices, reader, provider);

			localPlayers.Add(player);

			Debug.Log($"Local player {index + 1} joined {team} with {string.Join(", ", devices.Select(d => d.displayName))}");

			if (InstanceFinder.ClientManager.Connection.IsAuthenticated)
				RequestBody(player);
		}

		// Players who joined before the client connected ask for their bodies now
		private void OnAuthenticated()
		{
			foreach (var player in localPlayers.Players)
				RequestBody(player);
		}

		private static void RequestBody(LocalPlayerController player)
		{
			InstanceFinder.ClientManager.Broadcast(new JoinTeamBroadcast
			{
				LocalIndex = player.Index,
				Team = player.Team
			});
		}

		private static TeamSide Opposite(TeamSide side)
			=> side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
	}
}
