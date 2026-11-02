using System.Linq;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using Networking;
using UnityEngine;


namespace Match
{
	/// <summary>
	/// Server side. Spawns both teams when the server starts and hands bodies to the humans
	/// that join. Scoring, the clock and win conditions will live here.
	/// </summary>
	public class MatchController : MonoBehaviour
	{
		[SerializeField]
		private NetworkManager networkManager;

		[SerializeField]
		private TeamController[] teams;

		[SerializeField]
		private NetworkObject ballPrefab;

		[SerializeField]
		private Transform ballSpawn;

		private void OnEnable()
		{
			networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
			networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
			networkManager.ServerManager.RegisterBroadcast<JoinTeamBroadcast>(OnJoinTeam);
		}

		private void OnDisable()
		{
			if (networkManager == null)
				return;

			networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
			networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
			networkManager.ServerManager.UnregisterBroadcast<JoinTeamBroadcast>(OnJoinTeam);
		}

		private void OnServerConnectionState(ServerConnectionStateArgs args)
		{
			if (args.ConnectionState == LocalConnectionState.Started)
			{
				foreach (var team in teams)
					team.SpawnBodies(networkManager);

				SpawnBall();
			}
			else if (args.ConnectionState == LocalConnectionState.Stopped)
			{
				foreach (var team in teams)
					team.Clear();
			}
		}

		private void SpawnBall()
		{
			var instance = networkManager.GetPooledInstantiated(
				ballPrefab,
				ballSpawn.position,
				Quaternion.identity,
				true);

			networkManager.ServerManager.Spawn(instance);

			instance.GetComponent<Ball>().Kickoff();
		}

		private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
		{
			if (args.ConnectionState != RemoteConnectionState.Stopped)
				return;

			foreach (var team in teams)
				team.ReleaseConnection(connection);
		}

		private void OnJoinTeam(NetworkConnection connection, JoinTeamBroadcast message, Channel channel)
		{
			var team = teams.FirstOrDefault(t => t.Side == message.Team);

			if (team == null || !team.TryAssignHuman(connection, message.LocalIndex))
				Debug.LogWarning($"No free body on {message.Team} for client {connection.ClientId} player {message.LocalIndex + 1}");
		}
	}
}
