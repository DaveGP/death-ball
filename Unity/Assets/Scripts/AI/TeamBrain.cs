using System.Collections.Generic;
using System.Linq;
using Character.Networking;
using Data.Enums;
using DependencyInjection;
using Match;
using UnityEngine;


namespace AI
{
	/// <summary>
	/// Server side. Plans a team's play a few times a second by giving each AI player a role.
	///
	/// Loose ball: one player chases it, a keeper drops back if the ball is in our half, and the
	/// rest get into support spots. Our ball: the carrier carries, one escort fights for them,
	/// and the rest get open ahead. Their ball: one hunts the carrier, one keeps goal, and the
	/// rest each mark a different opponent, pass targets first.
	///
	/// Every role is picked by a cost: distance, less a bonus for player types suited to it,
	/// less a bonus for whoever already has the role, so roles don't flicker between players.
	/// Humans aren't given roles, but their place in the play is taken into account.
	/// </summary>
	public class TeamBrain
	{
		private const float ReplanInterval = 0.25f;

		// How much closer a new player must be to take a role off its current holder
		private const float Hysteresis = 2f;

		private const float ArenaHalfWidth = 12f;

		// How far in from each goal support spots can go
		private const float GoalMargin = 2f;

		private readonly TeamController team;

		private readonly Dictionary<CharacterNetworkController, AIOrders> orders = new();

		private readonly Dictionary<CharacterNetworkController, AIOrders> previousOrders = new();

		private readonly List<CharacterNetworkController> available = new();

		private float nextPlanTime;

		public TeamBrain(TeamController team)
		{
			this.team = team;
		}

		private float AttackSign => Mathf.Sign(team.AttackTarget.position.z);

		private Vector3 OwnGoal => team.Opponents.AttackTarget.position;

		public AIOrders OrdersFor(CharacterNetworkController body)
			=> orders.TryGetValue(body, out var bodyOrders)
				? bodyOrders
				: new AIOrders(AIRole.Support, position: body.transform.position);

		public void Tick()
		{
			if (Time.time < nextPlanTime)
				return;

			nextPlanTime = Time.time + ReplanInterval;

			Plan();
		}

		/// <summary>
		/// Whether nobody on the other team stands within the given width of the straight line between two points.
		/// </summary>
		public bool IsLaneClear(Vector3 from, Vector3 to, float width)
		{
			foreach (var opponent in team.Opponents.Bodies)
			{
				if (opponent.IsKnockedOut)
					continue;

				if (DistanceToSegment(Flat(opponent.transform.position), Flat(from), Flat(to)) < width)
					return false;
			}

			return true;
		}

		private void Plan()
		{
			previousOrders.Clear();

			foreach (var pair in orders)
				previousOrders[pair.Key] = pair.Value;

			orders.Clear();

			available.Clear();
			available.AddRange(team.Bodies.Where(b => !b.IsKnockedOut && !b.IsHumanControlled));

			if (available.Count == 0 || !ServiceLocator.TryGet(out Ball ball))
				return;

			var carrier = ball.Carrier;

			if (carrier == null)
				PlanLooseBall(ball.Position);
			else if (carrier.Team == team.Side)
				PlanAttack(carrier);
			else
				PlanDefence(carrier);
		}

		private void PlanLooseBall(Vector3 ballPosition)
		{
			Assign(AIRole.Chase, b => Distance(b, ballPosition) - ChaseBonus(b.Type));

			// Somebody stays home when the ball is in our half
			if (ballPosition.z * AttackSign < 0f)
				Assign(AIRole.Keep, b => Distance(b, OwnGoal) - KeepBonus(b.Type));

			AssignSupport(ballPosition);
		}

		private void PlanAttack(CharacterNetworkController carrier)
		{
			if (available.Remove(carrier))
				orders[carrier] = new AIOrders(AIRole.Carry);

			Assign(AIRole.Escort, b => Distance(b, carrier.transform.position) - EscortBonus(b.Type), carrier);

			AssignSupport(carrier.transform.position);
		}

		private void PlanDefence(CharacterNetworkController carrier)
		{
			var carrierPosition = carrier.transform.position;

			Assign(AIRole.Hunt, b => Distance(b, carrierPosition) - HuntBonus(b.Type), carrier);

			Assign(AIRole.Keep, b => Distance(b, OwnGoal) - KeepBonus(b.Type));

			// Their likeliest receivers first: throwers, then whoever is furthest towards our goal
			var receivers = team.Opponents.Bodies
				.Where(o => o != carrier && !o.IsKnockedOut)
				.OrderByDescending(o => o.Type == PlayerType.Thrower)
				.ThenBy(o => Distance(o, OwnGoal))
				.ToList();

			foreach (var receiver in receivers)
			{
				if (available.Count == 0)
					break;

				Assign(AIRole.Mark, b => Distance(b, receiver.transform.position), receiver);
			}

			// More of us than them to mark: help hunt
			foreach (var body in available.ToList())
			{
				orders[body] = new AIOrders(AIRole.Mark, carrier);
				available.Remove(body);
			}
		}

		// Fans the rest out across lanes ahead of the play, throwers furthest forward to receive,
		// enforcers furthest back to cover
		private void AssignSupport(Vector3 play)
		{
			var lanes = new List<float> { -9f, -3f, 3f, 9f };

			foreach (var body in available.OrderBy(b => b.transform.position.x).ToList())
			{
				var x = body.transform.position.x;

				var lane = lanes.OrderBy(l => Mathf.Abs(l - x)).First();
				lanes.Remove(lane);

				var z = play.z + AttackSign * SupportDepth(body.Type);

				var position = new Vector3(
					Mathf.Clamp(lane, -ArenaHalfWidth, ArenaHalfWidth),
					0f,
					ClampToPitch(z));

				orders[body] = new AIOrders(AIRole.Support, position: position);
			}

			available.Clear();
		}

		// Gives the role to the available player with the lowest cost
		private void Assign(AIRole role, System.Func<CharacterNetworkController, float> cost, CharacterNetworkController target = null)
		{
			CharacterNetworkController best = null;

			var bestCost = float.MaxValue;

			foreach (var body in available)
			{
				var bodyCost = cost(body);

				if (previousOrders.TryGetValue(body, out var previous) && previous.Role == role)
					bodyCost -= Hysteresis;

				if (bodyCost >= bestCost)
					continue;

				best = body;
				bestCost = bodyCost;
			}

			if (best == null)
				return;

			orders[best] = new AIOrders(role, target);

			available.Remove(best);
		}

		private float ClampToPitch(float z)
		{
			var limit = Mathf.Abs(team.AttackTarget.position.z) - GoalMargin;

			return Mathf.Clamp(z, -limit, limit);
		}

		// Metres taken off a player's distance when choosing who gets a role
		private static float ChaseBonus(PlayerType type)
			=> type == PlayerType.Runner ? 3f : 0f;

		// Hunting means going in for tackles, which suits the sturdy types
		private static float HuntBonus(PlayerType type)
			=> type switch
			{
				PlayerType.Bruiser => 3f,
				PlayerType.Enforcer => 1f,
				PlayerType.Thrower => -4f,
				_ => 0f
			};

		private static float KeepBonus(PlayerType type)
			=> type switch
			{
				PlayerType.Enforcer => 8f,
				PlayerType.Bruiser => 2f,
				_ => -4f
			};

		private static float EscortBonus(PlayerType type)
			=> type switch
			{
				PlayerType.Bruiser => 4f,
				PlayerType.Enforcer => 2f,
				_ => -4f
			};

		private static float SupportDepth(PlayerType type)
			=> type switch
			{
				PlayerType.Thrower => 10f,
				PlayerType.Runner => 7f,
				PlayerType.Bruiser => 3f,
				_ => 0f
			};

		private static float Distance(CharacterNetworkController body, Vector3 position)
			=> Vector2.Distance(Flat(body.transform.position), Flat(position));

		private static Vector2 Flat(Vector3 vector)
			=> new(vector.x, vector.z);

		private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
		{
			var segment = end - start;

			var t = segment.sqrMagnitude < 0.0001f
				? 0f
				: Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude);

			return Vector2.Distance(point, start + segment * t);
		}
	}
}
