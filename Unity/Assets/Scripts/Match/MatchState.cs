using System;
using Data.Enums;
using DependencyInjection;
using FishNet.Object;
using FishNet.Object.Synchronizing;


namespace Match
{
	/// <summary>
	/// The networked state of a match that every machine needs to show or obey. Spawned by
	/// the server's MatchController, which makes all the decisions.
	/// </summary>
	public class MatchState : NetworkBehaviour
	{
		private readonly SyncVar<int> homeScore = new();

		private readonly SyncVar<int> awayScore = new();

		private readonly SyncVar<MatchPhase> phase = new();

		// Whole seconds are plenty for the HUD and keep the clock from sending every tick
		private readonly SyncVar<int> secondsRemaining = new();

		/// <summary>
		/// Raised on every client for a banner: a headline, the detail under it, and how long to show it.
		/// </summary>
		public event Action<string, string, float> Announced;

		public MatchPhase Phase => phase.Value;

		public int SecondsRemaining => secondsRemaining.Value;

		// Nobody moves once the result is in
		public bool IsPlayFrozen => phase.Value == MatchPhase.Ended;

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
		public void StartMatch(int seconds)
		{
			homeScore.Value = 0;
			awayScore.Value = 0;

			phase.Value = MatchPhase.Playing;

			secondsRemaining.Value = seconds;
		}

		[Server]
		public void SetSecondsRemaining(int seconds)
		{
			secondsRemaining.Value = seconds;
		}

		[Server]
		public void AddGoal(TeamSide team, int points, string description, float bannerDuration)
		{
			if (team == TeamSide.Home)
				homeScore.Value += points;
			else
				awayScore.Value += points;

			Announce($"{team.ToString().ToUpper()} SCORES", $"{description}  +{points}", bannerDuration);
		}

		[Server]
		public void StartSuddenDeath(float bannerDuration)
		{
			phase.Value = MatchPhase.SuddenDeath;

			Announce("SUDDEN DEATH", "Next goal or knockout wins", bannerDuration);
		}

		[Server]
		public void EndMatch(TeamSide winner, string reason, float bannerDuration)
		{
			phase.Value = MatchPhase.Ended;

			Announce($"{winner.ToString().ToUpper()} WINS", reason, bannerDuration);
		}

		[ObserversRpc]
		private void Announce(string headline, string detail, float duration)
		{
			Announced?.Invoke(headline, detail, duration);
		}
	}
}
