using Character.Networking;
using Data.Enums;
using Data.Input;
using DependencyInjection;
using Match;
using UnityEngine;


namespace AI
{
	using Interfaces;


	/// <summary>
	/// Simple ball-aware brain until team tactics exist. The AI nearest a loose ball goes for it,
	/// the carrier runs at the opponents' goal and shoots, the AI nearest an opponent carrier
	/// hunts them down, and everyone else supports in their lane or marks an opponent.
	/// </summary>
	public class BallBrain : IAIBrain
	{
		private const float ShootRange = 12f;

		private const float StrikeRange = 1.6f;

		private const float SupportDistance = 6f;

		private const float ArriveDistance = 0.5f;

		private const float ActionCooldown = 0.8f;

		private const float PressureRange = 3f;

		private const float OpenDistance = 4f;

		private const float SwerveWeight = 1.5f;

		private readonly CharacterNetworkController self;

		private readonly TeamController team;

		private readonly float laneX;

		private float nextActionTime;

		public BallBrain(CharacterNetworkController self, TeamController team, float laneX)
		{
			this.self = self;
			this.team = team;
			this.laneX = laneX;
		}

		private Vector3 Position => self.transform.position;

		// The arena is centred on the origin, so the goal's side gives the direction of attack
		private float AttackSign => Mathf.Sign(team.AttackTarget.position.z);

		public CharacterInput Think()
		{
			if (!ServiceLocator.TryGet(out Ball ball))
				return Mark();

			var carrier = ball.Carrier;

			if (carrier == self)
				return AttackGoal();

			if (carrier == null)
				return IsNearestAI(ball.transform.position)
					? MoveTo(ball.transform.position, true)
					: Support(ball.transform.position);

			if (carrier.Team == self.Team)
				return Support(carrier.transform.position);

			return IsNearestAI(carrier.transform.position)
				? Strike(carrier.transform, CharacterAction.Tackle)
				: Mark();
		}

		private bool IsNearestAI(Vector3 position)
			=> team.NearestBodyTo(position, aiOnly: true) == self.transform;

		private CharacterInput AttackGoal()
		{
			var toGoal = Flat(team.AttackTarget.position - Position);

			var direction = toGoal.normalized;

			if (toGoal.magnitude <= ShootRange)
				return new CharacterInput(direction, direction, CharacterAction.Shoot);

			var threat = team.Opponents.NearestBodyTo(Position);

			if (threat == null || Flat(threat.position - Position).magnitude > PressureRange)
				return new CharacterInput(direction, direction, CharacterAction.Sprint);

			// Under pressure: get rid of it, or swerve round the defender
			var openTeammate = FindOpenTeammate();

			if (openTeammate != null)
			{
				var toTeammate = Flat(openTeammate.position - Position).normalized;

				return new CharacterInput(direction, toTeammate, CharacterAction.Pass);
			}

			var away = Flat(Position - threat.position).normalized;

			var swerve = (direction + away * SwerveWeight).normalized;

			return new CharacterInput(swerve, swerve, CharacterAction.Sprint);
		}

		// The furthest forward teammate with no opponent near them
		private Transform FindOpenTeammate()
		{
			Transform best = null;

			var bestProgress = float.MinValue;

			foreach (var teammate in team.Bodies)
			{
				if (teammate == self)
					continue;

				var position = teammate.transform.position;

				var marker = team.Opponents.NearestBodyTo(position);

				if (marker != null && Flat(marker.position - position).magnitude < OpenDistance)
					continue;

				var progress = position.z * AttackSign;

				if (progress <= bestProgress)
					continue;

				best = teammate.transform;
				bestProgress = progress;
			}

			return best;
		}

		// Keep to our own lane, a little ahead of the play
		private CharacterInput Support(Vector3 play)
		{
			var position = new Vector3(laneX, 0f, play.z + AttackSign * SupportDistance);

			return MoveTo(position, false);
		}

		private CharacterInput Mark()
		{
			var opponent = team.Opponents.NearestBodyTo(Position);

			return opponent == null
				? CharacterInput.None
				: Strike(opponent, CharacterAction.Attack);
		}

		private CharacterInput Strike(Transform target, CharacterAction strike)
		{
			var offset = Flat(target.position - Position);

			var direction = offset.normalized;

			if (offset.magnitude > StrikeRange)
				return new CharacterInput(direction, direction, CharacterAction.Sprint);

			if (Time.time < nextActionTime)
				return new CharacterInput(Vector2.zero, direction, CharacterAction.None);

			nextActionTime = Time.time + ActionCooldown;

			return new CharacterInput(Vector2.zero, direction, strike);
		}

		private CharacterInput MoveTo(Vector3 position, bool sprint)
		{
			var offset = Flat(position - Position);

			if (offset.magnitude < ArriveDistance)
				return CharacterInput.None;

			var direction = offset.normalized;

			return new CharacterInput(direction, direction, sprint ? CharacterAction.Sprint : CharacterAction.None);
		}

		private static Vector2 Flat(Vector3 vector)
			=> new(vector.x, vector.z);
	}
}
