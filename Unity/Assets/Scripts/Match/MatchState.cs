using System;
using Data.Enums;
using DependencyInjection;
using FishNet.Object;
using FishNet.Object.Synchronizing;


namespace Match
{
	/// <summary>
	/// The networked state of a match that every machine needs to show. Spawned by the
	/// server's MatchController, which makes all the decisions.
	/// </summary>
	public class MatchState : NetworkBehaviour
	{
		private readonly SyncVar<int> homeScore = new();

		private readonly SyncVar<int> awayScore = new();

		/// <summary>
		/// Raised on every client when a goal goes in: the scoring team, points and what kind of goal.
		/// </summary>
		public event Action<TeamSide, int, string> GoalScored;

		public int ScoreFor(TeamSide team)
			=> team == TeamSide.Home ? homeScore.Value : awayScore.Value;

		public override void OnStartNetwork()
		{
			base.OnStartNetwork();

			ServiceLocator.Register(this);
		}

		public override void OnStopNetwork()
		{
			base.OnStopNetwork();

			ServiceLocator.Unregister(this);
		}

		[Server]
		public void AddGoal(TeamSide team, int points, string description)
		{
			if (team == TeamSide.Home)
				homeScore.Value += points;
			else
				awayScore.Value += points;

			ShowGoal(team, points, description);
		}

		[ObserversRpc]
		private void ShowGoal(TeamSide team, int points, string description)
		{
			GoalScored?.Invoke(team, points, description);
		}
	}
}
