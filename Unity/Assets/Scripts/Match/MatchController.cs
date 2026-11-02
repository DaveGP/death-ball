using System.Collections;
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
	/// Server side. Spawns both teams, the ball and the match state when the server starts,
	/// hands bodies to the humans that join, and scores goals. The clock and win conditions
	/// will live here too.
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

		[SerializeField]
		private NetworkObject matchStatePrefab;

		// How long the goal is celebrated before everyone resets for the kickoff
		[SerializeField]
		private float goalCelebration = 2.5f;

		private Ball ball;

		private MatchState matchState;

		private Vector3 previousBallPosition;

		private bool resettingAfterGoal;

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

				matchState = Spawn(matchStatePrefab, Vector3.zero).GetComponent<MatchState>();

				ball = Spawn(ballPrefab, ballSpawn.position).GetComponent<Ball>();

				Kickoff();
			}
			else if (args.ConnectionState == LocalConnectionState.Stopped)
			{
				foreach (var team in teams)
					team.Clear();

				StopAllCoroutines();

				ball = null;
				matchState = null;
				resettingAfterGoal = false;
			}
		}

		private NetworkObject Spawn(NetworkObject prefab, Vector3 position)
		{
			var instance = networkManager.GetPooledInstantiated(prefab, position, Quaternion.identity, true);

			networkManager.ServerManager.Spawn(instance);

			return instance;
		}

		private void FixedUpdate()
		{
			if (ball == null || resettingAfterGoal)
				return;

			var position = ball.Position;

			CheckForGoal(previousBallPosition, position);

			previousBallPosition = position;
		}

		// Only a thrown ball scores, and only in the goal the thrower's team attacks
		private void CheckForGoal(Vector3 from, Vector3 to)
		{
			var thrower = ball.Thrower;

			if (!ball.IsLoose || thrower == null)
				return;

			var team = teams.FirstOrDefault(t => t.Side == thrower.Team);

			if (team == null || !team.AttackGoal.IsThrough(from, to))
				return;

			ScoreGoal(team);
		}

		private void ScoreGoal(TeamController team)
		{
			var attackSign = Mathf.Sign(team.AttackTarget.position.z);

			// Thrown from behind the halfway line
			var longRange = ball.ThrowOrigin.z * attackSign < 0f;

			var points = longRange ? 2 : 1;

			var description = longRange ? "Long-range goal" : "Goal";

			if (ball.ThrowAssisted)
			{
				points++;
				description += " off an air pass";
			}

			matchState.AddGoal(team.Side, points, description);

			Debug.Log($"{team.Side} scored: {description} (+{points})");

			StartCoroutine(ResetAfterGoal());
		}

		private IEnumerator ResetAfterGoal()
		{
			resettingAfterGoal = true;

			ball.EndPlay();

			yield return new WaitForSeconds(goalCelebration);

			foreach (var team in teams)
				team.ResetPositions();

			Kickoff();

			resettingAfterGoal = false;
		}

		private void Kickoff()
		{
			ball.Kickoff();

			previousBallPosition = ballSpawn.position;
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
