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

		// Seconds between tackles on the carrier
		private const float HuntCooldownMin = 1.5f;

		private const float HuntCooldownMax = 2.5f;

		// Seconds between swings at a marked opponent
		private const float MarkCooldownMin = 2.5f;

		private const float MarkCooldownMax = 4f;

		// How far out from our ring the keeper stands
		private const float KeeperDistance = 3f;

		// How far a marker stands off between swings
		private const float MarkDistance = 2.5f;

		// Supporters start guarding when an opponent is this close
		private const float GuardRange = 3f;

		private const float ReactionMin = 0.15f;

		private const float ReactionMax = 0.6f;

		private const float PressureRange = 3f;

		private const float OpenDistance = 4f;

		private const float SwerveWeight = 1.5f;

		// Degrees either side of the goal, so the AI misses sometimes
		private const float ShotSpread = 8f;

		private readonly CharacterNetworkController self;

		private readonly TeamController team;

		private readonly float laneX;

		private float nextActionTime;

		private bool inStrikeRange;

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

			if (IsNearestAI(carrier.transform.position))
				return Strike(carrier.transform, CharacterAction.Tackle, HuntCooldownMin, HuntCooldownMax, false);

			return IsNearestAI(OwnGoal.position)
				? Keep(carrier.transform.position)
				: Mark();
		}

		private Transform OwnGoal => team.Opponents.AttackTarget;

		private bool IsNearestAI(Vector3 position)
			=> team.NearestBodyTo(position, aiOnly: true) == self.transform;

		// Stands between the carrier and our ring with guard up. Shots are too fast to catch,
		// so one that hits the keeper bounces off instead of scoring.
		private CharacterInput Keep(Vector3 carrierPosition)
		{
			var goal = OwnGoal.position;

			var toCarrier = carrierPosition - goal;
			toCarrier.y = 0f;

			var position = goal + toCarrier.normalized * KeeperDistance;
			position.y = 0f;

			var move = MoveTo(position, true);

			var facing = Flat(toCarrier).normalized;

			return new CharacterInput(move.Move, facing, move.Action | CharacterAction.Block);
		}

		private CharacterInput AttackGoal()
		{
			var toGoal = Flat(team.AttackTarget.position - Position);

			var direction = toGoal.normalized;

			if (toGoal.magnitude <= ShootRange)
				return new CharacterInput(direction, Rotate(direction, Random.Range(-ShotSpread, ShotSpread)), CharacterAction.Shoot);

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

			return Guarded(MoveTo(position, false));
		}

		// Supporters aren't looking for a fight, but they cover up when someone is on them and
		// hit back if they get close. Without this, whichever team had the ball took a beating
		// it never answered, and one side snowballed to a knockout every match.
		private CharacterInput Guarded(CharacterInput input)
		{
			var threat = team.Opponents.NearestBodyTo(Position);

			if (threat == null)
				return input;

			var distance = Flat(threat.position - Position).magnitude;

			if (distance <= StrikeRange)
				return Strike(threat, CharacterAction.Attack, MarkCooldownMin, MarkCooldownMax, true);

			if (distance <= GuardRange)
				return new CharacterInput(input.Move, input.Aim, input.Action | CharacterAction.Block);

			return input;
		}

		// Shadows an opponent from a step back with guard up, closing in for a swing every few
		// seconds. Standing toe to toe the whole time made fights constant and matches short.
		private CharacterInput Mark()
		{
			var opponent = team.Opponents.NearestBodyTo(Position);

			if (opponent == null)
				return CharacterInput.None;

			if (Time.time >= nextActionTime)
				return Strike(opponent, CharacterAction.Attack, MarkCooldownMin, MarkCooldownMax, true);

			inStrikeRange = false;

			var away = Position - opponent.position;
			away.y = 0f;

			var standOff = opponent.position + away.normalized * MarkDistance;

			var toOpponent = Flat(opponent.position - Position).normalized;

			var move = MoveTo(standOff, false);

			return new CharacterInput(move.Move, toOpponent, CharacterAction.Block);
		}

		private CharacterInput Strike(Transform target, CharacterAction strike, float cooldownMin, float cooldownMax, bool guard)
		{
			var offset = Flat(target.position - Position);

			var direction = offset.normalized;

			if (offset.magnitude > StrikeRange)
			{
				inStrikeRange = false;

				return new CharacterInput(direction, direction, CharacterAction.Sprint);
			}

			// A random beat before the first swing, so two AIs meeting don't swing on the same tick
			// and leave the fight to whichever the server happens to process first
			if (!inStrikeRange)
			{
				inStrikeRange = true;

				nextActionTime = Mathf.Max(nextActionTime, Time.time + Random.Range(ReactionMin, ReactionMax));
			}

			if (Time.time < nextActionTime)
				return new CharacterInput(Vector2.zero, direction, guard ? CharacterAction.Block : CharacterAction.None);

			nextActionTime = Time.time + Random.Range(cooldownMin, cooldownMax);

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

		private static Vector2 Rotate(Vector2 vector, float degrees)
		{
			var radians = degrees * Mathf.Deg2Rad;

			var cos = Mathf.Cos(radians);
			var sin = Mathf.Sin(radians);

			return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
		}
	}
}
