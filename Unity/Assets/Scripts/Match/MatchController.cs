using System.Collections;
using System.Linq;
using Data.Enums;
using Data.ScriptableObjects;
using DependencyInjection;
using Unity.Netcode;
using UnityEngine;


namespace Match
{
	/// <summary>
	/// Server side. Spawns both teams, the ball and the match state, hands bodies to the humans
	/// that ask for them, and runs the match: the clock, goals, and the three ways it ends. Most
	/// points at full time wins; a draw goes to sudden death, where the next goal wins; and knocking
	/// out the whole other team wins at any point.
	///
	/// Works the same whether the arena is played directly, starting a host itself, or loaded from
	/// the lobby with the session already running.
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

		[SerializeField]
		private float teamsBanner = 3f;

		// How long the result stays up before the next match starts
		[SerializeField]
		private float resultDuration = 8f;

		private NetworkManager networkManager;

		private Ball ball;

		private MatchState matchState;

		private Vector3 previousBallPosition;

		private float timeRemaining;

		private bool resettingAfterGoal;

		private bool IsInPlay => matchState != null && matchState.Phase != MatchPhase.Ended;

		private void Start()
		{
			networkManager = NetworkManager.Singleton;

			if (networkManager == null)
				return;

			networkManager.OnServerStarted += SetUpMatch;
			networkManager.OnClientDisconnectCallback += OnClientDisconnected;

			if (networkManager.IsServer)
				SetUpMatch();
		}

		private void OnDestroy()
		{
			if (networkManager == null)
				return;

			networkManager.OnServerStarted -= SetUpMatch;
			networkManager.OnClientDisconnectCallback -= OnClientDisconnected;

			if (matchState != null)
				matchState.BodyRequested -= OnBodyRequested;
		}

		// Clients ask for their bodies through the match state, which only reaches them once they
		// have loaded the arena, so a body is never handed to a client that can't see the floor yet
		private void SetUpMatch()
		{
			if (matchState != null)
				return;

			foreach (var team in teams)
				team.SpawnBodies();

			matchState = Spawn(matchStatePrefab, Vector3.zero).GetComponent<MatchState>();
			matchState.BodyRequested += OnBodyRequested;

			ball = Spawn(ballPrefab, ballSpawn.position).GetComponent<Ball>();

			if (ServiceLocator.TryGet(out MatchSettings settings) && settings.FromLobby)
				matchLength = settings.MatchLengthSeconds;

			StartMatch();
		}

		private static NetworkObject Spawn(NetworkObject prefab, Vector3 position)
		{
			var instance = Instantiate(prefab, position, Quaternion.identity);

			// Goes with the arena when it unloads
			instance.Spawn(destroyWithScene: true);

			return instance;
		}

		private void StartMatch()
		{
			foreach (var team in teams)
				team.ResetForMatch();

			timeRemaining = matchLength;

			var (home, away) = PickTeams();

			matchState.StartMatch(Mathf.CeilToInt(matchLength), home, away, teamsBanner);

			Kickoff();
		}

		// Two different teams, new for each match
		private static (int Home, int Away) PickTeams()
		{
			var roster = TeamRoster.Load();

			if (roster == null || roster.Count < 2)
				return (MatchState.NoTeam, MatchState.NoTeam);

			var home = Random.Range(0, roster.Count);
			var away = Random.Range(0, roster.Count - 1);

			if (away >= home)
				away++;

			return (home, away);
		}

		private void FixedUpdate()
		{
			// The ball can outlive the network for a tick while the session shuts down
			if (ball == null || !ball.IsSpawned || !IsInPlay || resettingAfterGoal)
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

		private void OnClientDisconnected(ulong clientId)
		{
			if (!networkManager.IsServer)
				return;

			foreach (var team in teams)
				team.ReleaseClient(clientId);
		}

		private void OnBodyRequested(ulong clientId, int localIndex, TeamSide side, PlayerType preferredType)
		{
			var team = teams.FirstOrDefault(t => t.Side == side);

			if (team == null || !team.TryAssignHuman(clientId, localIndex, preferredType))
				Debug.LogWarning($"No free body on {side} for client {clientId} player {localIndex + 1}");
		}
	}
}
