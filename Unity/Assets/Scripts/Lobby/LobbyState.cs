using System;
using System.Collections.Generic;
using Data.Enums;
using DependencyInjection;
using Unity.Collections;
using Unity.Netcode;
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
		private const int MaxLocalPlayers = 8;

		[SerializeField]
		private int maxNameLength = 16;

		private readonly NetworkList<LobbyEntry> entries = new();

		// Whole seconds, chosen by the host
		private readonly NetworkVariable<int> matchLength = new();

		public IEnumerable<LobbyEntry> Entries
		{
			get
			{
				foreach (var entry in entries)
					yield return entry;
			}
		}

		public int MatchLength => matchLength.Value;

		public event Action Changed;

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();

			entries.OnListChanged += OnEntriesChanged;

			matchLength.OnValueChanged += OnMatchLengthChanged;

			if (IsServer)
				NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

			ServiceLocator.Register(this);
		}

		public override void OnNetworkDespawn()
		{
			base.OnNetworkDespawn();

			entries.OnListChanged -= OnEntriesChanged;

			matchLength.OnValueChanged -= OnMatchLengthChanged;

			if (NetworkManager != null)
				NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;

			ServiceLocator.Unregister(this);
		}

		// Server only
		public void SetMatchLength(int seconds)
		{
			matchLength.Value = seconds;
		}

		/// <summary>
		/// Sends one of this machine's players to everyone.
		/// </summary>
		public void Submit(int localIndex, string playerName, TeamSide team, PlayerType type)
			=> SetEntryRpc(localIndex, new FixedString32Bytes(Shorten(playerName)), team, type);

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
		private void SetEntryRpc(int localIndex, FixedString32Bytes playerName, TeamSide team, PlayerType type, RpcParams rpcParams = default)
		{
			if (localIndex < 0 || localIndex >= MaxLocalPlayers)
				return;

			var sender = rpcParams.Receive.SenderClientId;

			var entry = new LobbyEntry
			{
				ClientId = sender,
				LocalIndex = localIndex,
				Name = playerName.IsEmpty ? new FixedString32Bytes($"Player {sender}-{localIndex + 1}") : playerName,
				Team = team,
				Type = type
			};

			var existing = IndexOf(sender, localIndex);

			if (existing < 0)
				entries.Add(entry);
			else
				entries[existing] = entry;
		}

		private string Shorten(string playerName)
		{
			var trimmed = playerName?.Trim() ?? string.Empty;

			return trimmed.Length > maxNameLength ? trimmed[..maxNameLength] : trimmed;
		}

		private int IndexOf(ulong clientId, int localIndex)
		{
			for (var i = 0; i < entries.Count; i++)
			{
				if (entries[i].ClientId == clientId && entries[i].LocalIndex == localIndex)
					return i;
			}

			return -1;
		}

		private void OnClientDisconnected(ulong clientId)
		{
			for (var i = entries.Count - 1; i >= 0; i--)
			{
				if (entries[i].ClientId == clientId)
					entries.RemoveAt(i);
			}
		}

		private void OnEntriesChanged(NetworkListEvent<LobbyEntry> change)
			=> Changed?.Invoke();

		private void OnMatchLengthChanged(int previous, int current)
			=> Changed?.Invoke();
	}
}
