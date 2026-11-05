using System;
using Data.Enums;
using DependencyInjection;
using Unity.Collections;
using Unity.Netcode;


namespace Match
{
	/// <summary>
	/// The networked state of a match that every machine needs to show or obey. Spawned by
	/// the server's MatchController, which makes all the decisions.
	///
	/// It is also how a client joins its players to the match: it only reaches a client once that
	/// client has loaded the arena, so the client asks for its bodies as soon as it appears.
	/// </summary>
	public class MatchState : NetworkBehaviour
	{
		private readonly NetworkVariable<int> homeScore = new();

		private readonly NetworkVariable<int> awayScore = new();

		private readonly NetworkVariable<MatchPhase> phase = new();

		// Whole seconds are plenty for the HUD and keep the clock from sending every tick
		private readonly NetworkVariable<int> secondsRemaining = new();

		/// <summary>
		/// Raised on every client for a banner: a headline, the detail under it, and how long to show it.
		/// </summary>
		public event Action<string, string, float> Announced;

		/// <summary>
		/// Server only. A client asking for a body for one of its players: client, local index, team, preferred type.
		/// </summary>
		public event Action<ulong, int, TeamSide, PlayerType> BodyRequested;

		public MatchPhase Phase => phase.Value;

		public int SecondsRemaining => secondsRemaining.Value;

		// Nobody moves once the result is in
		public bool IsPlayFrozen => phase.Value == MatchPhase.Ended;

		public int ScoreFor(TeamSide team)
			=> team == TeamSide.Home ? homeScore.Value : awayScore.Value;

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();

			ServiceLocator.Register(this);
		}

		public override void OnNetworkDespawn()
		{
			base.OnNetworkDespawn();

			ServiceLocator.Unregister(this);
		}

		/// <summary>
		/// Asks the server for a body for one of this machine's players, of its preferred type if one is free.
		/// </summary>
		public void RequestBody(int localIndex, TeamSide team, PlayerType preferredType)
			=> RequestBodyRpc(localIndex, team, preferredType);

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
		private void RequestBodyRpc(int localIndex, TeamSide team, PlayerType preferredType, RpcParams rpcParams = default)
		{
			BodyRequested?.Invoke(rpcParams.Receive.SenderClientId, localIndex, team, preferredType);
		}

		// Server only
		public void StartMatch(int seconds)
		{
			homeScore.Value = 0;
			awayScore.Value = 0;

			phase.Value = MatchPhase.Playing;

			secondsRemaining.Value = seconds;
		}

		// Server only
		public void SetSecondsRemaining(int seconds)
		{
			if (secondsRemaining.Value != seconds)
				secondsRemaining.Value = seconds;
		}

		// Server only
		public void AddGoal(TeamSide team, int points, string description, float bannerDuration)
		{
			if (team == TeamSide.Home)
				homeScore.Value += points;
			else
				awayScore.Value += points;

			Announce($"{team.ToString().ToUpper()} SCORES", $"{description}  +{points}", bannerDuration);
		}

		// Server only
		public void StartSuddenDeath(float bannerDuration)
		{
			phase.Value = MatchPhase.SuddenDeath;

			Announce("SUDDEN DEATH", "Next goal or knockout wins", bannerDuration);
		}

		// Server only
		public void EndMatch(TeamSide winner, string reason, float bannerDuration)
		{
			phase.Value = MatchPhase.Ended;

			Announce($"{winner.ToString().ToUpper()} WINS", reason, bannerDuration);
		}

		private void Announce(string headline, string detail, float duration)
			=> AnnounceRpc(new FixedString64Bytes(headline), new FixedString64Bytes(detail), duration);

		[Rpc(SendTo.ClientsAndHost)]
		private void AnnounceRpc(FixedString64Bytes headline, FixedString64Bytes detail, float duration)
		{
			Announced?.Invoke(headline.ToString(), detail.ToString(), duration);
		}
	}
}
