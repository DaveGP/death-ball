using System.Collections;
using System.Linq;
using Data.Enums;
using DependencyInjection;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Transporting;
using Networking;
using UnityEngine;


namespace Match
{
	/// <summary>
	/// Server side. Spawns both teams, the ball and the match state when the server starts,
	/// hands bodies to the humans that join, and runs the match: the clock, goals, and the
	/// three ways it ends. Most points at full time wins; a draw goes to sudden death, where
	/// the next goal wins; and knocking out the whole other team wins at any point.
	///
	/// Works the same whether the arena is played directly, starting the server itself, or
	/// loaded from the lobby with the server already running.
	/// </summary>
	public class MatchController : MonoBehaviour
	{
		[SerializeField]
		private TeamController[] teams;

		[SerializeField]
		private NetworkObject ballPrefab;

		[SerializeField]
		private Transform ballSpawn;

		[SerializeField]
		private NetworkObject matchStatePrefab;

		// Used when the arena is played directly; the lobby's match settings take over otherwise
		[SerializeField]
		private float matchLength = 300f;

		// How long the goal is celebrated before everyone resets for the kickoff. The clock stops meanwhile.
		[SerializeField]
		private float goalCelebration = 2.5f;

		[SerializeField]
		private float suddenDeathBanner = 3f;

		// How long the result stays up before the next match starts
		[SerializeField]
		private float resultDuration = 8f;

		private NetworkManager networkManager;

		// Loaded by the lobby's GameSession through FishNet, rather than played directly
		private bool loadedFromLobby;

		private Ball ball;

		private MatchState matchState;

		private Vector3 previousBallPosition;

		private float timeRemaining;

		private bool resettingAfterGoal;

		private bool IsInPlay => matchState != null && matchState.Phase != MatchPhase.Ended;

		// Start rather than OnEnable: when the arena is loaded from the menu its own
		// NetworkManager is discarded in Awake in favour of the menu's
		private void Start()
		{
			networkManager = InstanceFinder.NetworkManager;

			networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
			networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
			networkManager.ServerManager.OnAuthenticationResult += OnAuthenticationResult;
			networkManager.ServerManager.RegisterBroadcast<JoinTeamBroadcast>(OnJoinTeam);
			networkManager.SceneManager.OnClientPresenceChangeEnd += OnClientPresenceChangeEnd;

			loadedFromLobby = networkManager.GetComponent<Lobby.GameSession>() != null;

			if (networkManager.IsServerStarted)
				SetUpMatch();
		}

		private void OnDestroy()
		{
			if (networkManager == null)
				return;

			networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
			networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
			networkManager.ServerManager.OnAuthenticationResult -= OnAuthenticationResult;
			networkManager.ServerManager.UnregisterBroadcast<JoinTeamBroadcast>(OnJoinTeam);
			networkManager.SceneManager.OnClientPresenceChangeEnd -= OnClientPresenceChangeEnd;
		}

		private void SetUpMatch()
		{
			foreach (var team in teams)
				team.SpawnBodies(networkManager);

			matchState = Spawn(matchStatePrefab, Vector3.zero).GetComponent<MatchState>();

			ball = Spawn(ballPrefab, ballSpawn.position).GetComponent<Ball>();

			if (ServiceLocator.TryGet(out MatchSettings settings) && loadedFromLobby)
				matchLength = settings.MatchLengthSeconds;

			StartMatch();

			RequestPlayersFromEveryone();
		}

		// A client's players are only asked for once it has loaded the arena. A human's body is
		// moved by its owner, so one handed over while the owner was still loading fell through
		// a floor that wasn't there yet.
		private void RequestPlayersFromEveryone()
		{
			if (!loadedFromLobby)
			{
				networkManager.ServerManager.Broadcast(new RequestPlayersBroadcast());
				return;
			}

			if (!networkManager.SceneManager.SceneConnections.TryGetValue(gameObject.scene, out var connections))
				return;

			foreach (var connection in connections)
				networkManager.ServerManager.Broadcast(connection, new RequestPlayersBroadcast());
		}

		private void OnClientPresenceChangeEnd(ClientPresenceChangeEventArgs args)
		{
			if (loadedFromLobby && args.Added && args.Scene == gameObject.scene && matchState != null)
				networkManager.ServerManager.Broadcast(args.Connection, new RequestPlayersBroadcast());
		}

		// Played directly, the arena was loaded by Unity rather than FishNet, so FishNet doesn't know who
		// is in it. Match objects are only sent to clients in the arena scene, so add each one and ask
		// for its players once it connects.
		private void OnAuthenticationResult(NetworkConnection connection, bool passed)
		{
			if (!passed || loadedFromLobby)
				return;

			networkManager.SceneManager.AddConnectionToScene(connection, gameObject.scene);

			if (matchState != null)
				networkManager.ServerManager.Broadcast(connection, new RequestPlayersBroadcast());
		}

		private void OnServerConnectionState(ServerConnectionStateArgs args)
		{
			if (args.ConnectionState == LocalConnectionState.Started)
			{
				SetUpMatch();
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

			// Into the arena itself, so only clients in the arena are sent it
			networkManager.ServerManager.Spawn(instance, scene: gameObject.scene);

			return instance;
		}

		private void StartMatch()
		{
			foreach (var team in teams)
				team.ResetForMatch();

			timeRemaining = matchLength;

			matchState.StartMatch(Mathf.CeilToInt(matchLength));

			Kickoff();
		}

		private void FixedUpdate()
		{
			if (ball == null || !IsInPlay || resettingAfterGoal)
				return;

			if (CheckForKnockout())
				return;

			var position = ball.Position;

			CheckForGoal(previousBallPosition, position);

			previousBallPosition = position;

			if (matchState.Phase == MatchPhase.Playing && !resettingAfterGoal)
				RunClock();
		}

		private void RunClock()
		{
			timeRemaining -= Time.fixedDeltaTime;

			matchState.SetSecondsRemaining(Mathf.Max(0, Mathf.CeilToInt(timeRemaining)));

			if (timeRemaining <= 0f)
				OnFullTime();
		}

		private void OnFullTime()
		{
			var home = matchState.ScoreFor(TeamSide.Home);
			var away = matchState.ScoreFor(TeamSide.Away);

			if (home == away)
			{
				matchState.StartSuddenDeath(suddenDeathBanner);
				return;
			}

			EndMatch(home > away ? TeamSide.Home : TeamSide.Away, $"{Mathf.Max(home, away)} - {Mathf.Min(home, away)} at full time");
		}

		private bool CheckForKnockout()
		{
			var beaten = teams.FirstOrDefault(t => t.IsAllKnockedOut);

			if (beaten == null)
				return false;

			EndMatch(beaten.Opponents.Side, "Knockout");

			return true;
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

			Debug.Log($"{team.Side} scored: {description} (+{points})");

			if (matchState.Phase == MatchPhase.SuddenDeath)
			{
				matchState.AddGoal(team.Side, points, description, 0f);

				EndMatch(team.Side, "Sudden death goal");

				return;
			}

			matchState.AddGoal(team.Side, points, description, goalCelebration);

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

		private void EndMatch(TeamSide winner, string reason)
		{
			StopAllCoroutines();

			resettingAfterGoal = false;

			ball.EndPlay();

			matchState.EndMatch(winner, reason, resultDuration);

			Debug.Log($"{winner} won: {reason}");

			StartCoroutine(RestartAfterResult());
		}

		private IEnumerator RestartAfterResult()
		{
			yield return new WaitForSeconds(resultDuration);

			StartMatch();
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

			if (team == null || !team.TryAssignHuman(connection, message.LocalIndex, message.PreferredType))
				Debug.LogWarning($"No free body on {message.Team} for client {connection.ClientId} player {message.LocalIndex + 1}");
		}
	}
}
