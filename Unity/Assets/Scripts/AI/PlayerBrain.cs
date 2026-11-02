using Character.Networking;
using Character.Specials;
using Data.Enums;
using Data.Input;
using DependencyInjection;
using Match;
using UnityEngine;


namespace AI
{
	using Interfaces;


	/// <summary>
	/// One AI player. Carries out the role its team brain gives it, turning it into input
	/// each tick, and uses its special when the special says it's worth it.
	/// </summary>
	public class PlayerBrain : IAIBrain
	{
		private const float ShootRange = 12f;

		private const float StrikeRange = 1.6f;

		private const float ArriveDistance = 0.5f;

		// Seconds between tackles on the carrier
		private const float HuntCooldownMin = 1.5f;

		private const float HuntCooldownMax = 2.5f;

		// Seconds between swings at a marked opponent
		private const float MarkCooldownMin = 2.5f;

		private const float MarkCooldownMax = 4f;

		// A carrier dodges away from a defender this close, every so often
		private const float DodgeRange = 2f;

		private const float DodgeCooldownMin = 1.5f;

		private const float DodgeCooldownMax = 3f;

		// How far out from our ring the keeper stands
		private const float KeeperDistance = 3f;

		// How far a marker stands off between swings, goal side of their opponent
		private const float MarkDistance = 2.5f;

		// Supporters start guarding when an opponent is this close
		private const float GuardRange = 3f;

		// An escort takes on anyone this close to the carrier, and otherwise runs this far to one side
		private const float EscortRange = 5f;

		private const float EscortOffset = 2.5f;

		private const float ReactionMin = 0.15f;

		private const float ReactionMax = 0.6f;

		private const float PressureRange = 3f;

		// Width of the path a pass or shot needs clear of opponents
		private const float LaneWidth = 1.2f;

		private const float SwerveWeight = 1.5f;

		// Degrees either side of the goal, so the AI misses sometimes
		private const float ShotSpread = 8f;

		private readonly CharacterNetworkController self;

		private readonly TeamController team;

		private readonly CharacterSpecial special;

		private float nextActionTime;

		private bool inStrikeRange;

		private float nextDodgeTime;

		public PlayerBrain(CharacterNetworkController self, TeamController team)
		{
			this.self = self;
			this.team = team;

			special = self.GetComponent<CharacterSpecial>();
		}

		private Vector3 Position => self.transform.position;

		private Transform OwnGoal => team.Opponents.AttackTarget;

		public CharacterInput Think()
		{
			if (!ServiceLocator.TryGet(out Ball ball))
				return CharacterInput.None;

			// Each special knows when it's worth using
			if (special.ShouldAIUse)
				return new CharacterInput(Vector2.zero, Vector2.zero, CharacterAction.Special);

			// Whatever the plan says, the ball is ours now
			if (ball.Carrier == self)
				return Carry();

			var orders = team.Brain.OrdersFor(self);

			return orders.Role switch
			{
				AIRole.Chase => MoveTo(ball.Position, true),
				AIRole.Escort => Escort(orders.Target),
				AIRole.Hunt => Hunt(orders.Target),
				AIRole.Mark => Mark(orders.Target),
				AIRole.Keep => Keep(ball.Carrier != null ? ball.Carrier.transform.position : ball.Position),
				_ => Guarded(MoveTo(orders.Position, false))
			};
		}

		private CharacterInput Carry()
		{
			var goal = team.AttackTarget.position;

			var toGoal = Flat(goal - Position);

			var direction = toGoal.normalized;

			// In range with a clear sight of the ring, or in range and about to be flattened
			var threat = team.Opponents.NearestBodyTo(Position);

			var threatDistance = threat == null ? float.MaxValue : Flat(threat.position - Position).magnitude;

			if (toGoal.magnitude <= ShootRange
			    && (team.Brain.IsLaneClear(Position, goal, LaneWidth) || threatDistance <= PressureRange))
				return new CharacterInput(direction, Rotate(direction, Random.Range(-ShotSpread, ShotSpread)), CharacterAction.Shoot);

			if (threatDistance > PressureRange)
				return new CharacterInput(direction, direction, CharacterAction.Sprint);

			// Under pressure: pass to someone open, or swerve round the defender
			var receiver = FindReceiver();

			if (receiver != null)
				return new CharacterInput(direction, Flat(receiver.position - Position).normalized, CharacterAction.Pass);

			var away = Flat(Position - threat.position).normalized;

			var swerve = (direction + away * SwerveWeight).normalized;

			// Right on top of us: dodge clear, which also slips the hit
			if (threatDistance <= DodgeRange && Time.time >= nextDodgeTime)
			{
				nextDodgeTime = Time.time + Random.Range(DodgeCooldownMin, DodgeCooldownMax);

				return new CharacterInput(swerve, swerve, CharacterAction.Dodge);
			}

			return new CharacterInput(swerve, swerve, CharacterAction.Sprint);
		}

		// The teammate furthest towards goal with a clear passing lane, preferring throwers
		private Transform FindReceiver()
		{
			Transform best = null;

			var bestScore = float.MinValue;

			var attackSign = Mathf.Sign(team.AttackTarget.position.z);

			foreach (var teammate in team.Bodies)
			{
				if (teammate == self || teammate.IsKnockedOut)
					continue;

				var position = teammate.transform.position;

				if (!team.Brain.IsLaneClear(Position, position, LaneWidth))
					continue;

				var score = position.z * attackSign + (teammate.Type == PlayerType.Thrower ? 4f : 0f);

				if (score <= bestScore)
					continue;

				best = teammate.transform;
				bestScore = score;
			}

			return best;
		}

		// Runs alongside our carrier and takes on whoever closes in on them
		private CharacterInput Escort(CharacterNetworkController carrier)
		{
			if (carrier == null)
				return CharacterInput.None;

			var carrierPosition = carrier.transform.position;

			var threat = team.Opponents.NearestBodyTo(carrierPosition);

			if (threat != null && Flat(threat.position - carrierPosition).magnitude <= EscortRange)
				return Strike(threat, CharacterAction.Attack, MarkCooldownMin, MarkCooldownMax, true);

			var side = Position.x < carrierPosition.x ? -1f : 1f;

			return Guarded(MoveTo(carrierPosition + Vector3.right * (side * EscortOffset), true));
		}

		private CharacterInput Hunt(CharacterNetworkController carrier)
			=> carrier == null
				? CharacterInput.None
				: Strike(carrier.transform, CharacterAction.Tackle, HuntCooldownMin, HuntCooldownMax, false);

		// Stands between the play and our ring with guard up. Shots are too fast to catch,
		// so one that hits the keeper bounces off instead of scoring.
		private CharacterInput Keep(Vector3 play)
		{
			var goal = OwnGoal.position;

			var toPlay = play - goal;
			toPlay.y = 0f;

			var position = goal + toPlay.normalized * KeeperDistance;
			position.y = 0f;

			var move = MoveTo(position, true);

			var facing = Flat(toPlay).normalized;

			return new CharacterInput(move.Move, facing, move.Action | CharacterAction.Block);
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

		// Shadows an opponent from a step back on our goal's side with guard up, closing in for a
		// swing every few seconds. Standing toe to toe the whole time made fights constant.
		private CharacterInput Mark(CharacterNetworkController opponent)
		{
			if (opponent == null || opponent.IsKnockedOut)
				return Guarded(CharacterInput.None);

			var opponentPosition = opponent.transform.position;

			if (Time.time >= nextActionTime)
				return Strike(opponent.transform, CharacterAction.Attack, MarkCooldownMin, MarkCooldownMax, true);

			inStrikeRange = false;

			var goalSide = OwnGoal.position - opponentPosition;
			goalSide.y = 0f;

			var standOff = opponentPosition + goalSide.normalized * MarkDistance;

			var move = MoveTo(standOff, false);

			return new CharacterInput(move.Move, Flat(opponentPosition - Position).normalized, CharacterAction.Block);
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
