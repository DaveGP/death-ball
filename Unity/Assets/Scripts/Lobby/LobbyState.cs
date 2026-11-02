using System;
using System.Collections.Generic;
using DependencyInjection;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;


namespace Lobby
{
	/// <summary>
	/// Who is in the lobby: every local player on every machine, with the team and type they've
	/// picked. Spawned by the GameSession while the menu is up, and gone once the match starts.
	/// Each client keeps its own players' choices and sends them here for everyone to see.
	/// </summary>
	public class LobbyState : NetworkBehaviour
	{
		// Players per machine, so a client id and local index make one key
		private const int MaxLocalPlayers = 8;

		[SerializeField]
		private int maxNameLength = 16;

		private readonly SyncDictionary<int, LobbyEntry> entries = new();

		// Whole seconds, chosen by the host
		private readonly SyncVar<int> matchLength = new();

		public IEnumerable<LobbyEntry> Entries => entries.Values;

		public int MatchLength => matchLength.Value;

		public event Action Changed;

		private void Awake()
		{
			entries.OnChange += OnEntriesChanged;

			matchLength.OnChange += (_, _, asServer) => RaiseChanged(asServer);
		}

		// Found by the menu only once this client can send to it. Registering any earlier, on the
		// host, let players already in the session submit before their messages could go through.
		public override void OnStartClient()
		{
			base.OnStartClient();

			ServiceLocator.Register(this);
		}

		public override void OnStopClient()
		{
			base.OnStopClient();

			ServiceLocator.Unregister(this);
		}

		public override void OnStartServer()
		{
			base.OnStartServer();

			ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
		}

		public override void OnStopServer()
		{
			base.OnStopServer();

			ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
		}

		[Server]
		public void SetMatchLength(int seconds)
		{
			matchLength.Value = seconds;
		}

		/// <summary>
		/// Sends one of this machine's players to everyone.
		/// </summary>
		public void Submit(int localIndex, string playerName, Data.Enums.TeamSide team, Data.Enums.PlayerType type)
		{
			SetEntry(localIndex, playerName, team, type);
		}

		[ServerRpc(RequireOwnership = false)]
		private void SetEntry(int localIndex, string playerName, Data.Enums.TeamSide team, Data.Enums.PlayerType type, NetworkConnection sender = null)
		{
			if (sender == null || localIndex < 0 || localIndex >= MaxLocalPlayers)
				return;

			entries[Key(sender.ClientId, localIndex)] = new LobbyEntry
			{
				ClientId = sender.ClientId,
				LocalIndex = localIndex,
				Name = SanitiseName(playerName, sender.ClientId, localIndex),
				Team = team,
				Type = type
			};
		}

		private string SanitiseName(string playerName, int clientId, int localIndex)
		{
			var trimmed = playerName?.Trim();

			if (string.IsNullOrEmpty(trimmed))
				return $"Player {clientId}-{localIndex + 1}";

			return trimmed.Length > maxNameLength ? trimmed[..maxNameLength] : trimmed;
		}

		private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
		{
			if (args.ConnectionState != RemoteConnectionState.Stopped)
				return;

			for (var i = 0; i < MaxLocalPlayers; i++)
				entries.Remove(Key(connection.ClientId, i));
		}

		private void OnEntriesChanged(SyncDictionaryOperation op, int key, LobbyEntry value, bool asServer)
			=> RaiseChanged(asServer);

		// The host gets every change twice, once as server and once as client
		private void RaiseChanged(bool asServer)
		{
			if (asServer && IsClientStarted)
				return;

			Changed?.Invoke();
		}

		private static int Key(int clientId, int localIndex)
			=> clientId * MaxLocalPlayers + localIndex;
	}
}
