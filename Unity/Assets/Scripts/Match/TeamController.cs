using System.Collections.Generic;
using System.Linq;
using AI;
using Character.Networking;
using Data.Enums;
using DependencyInjection;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using Players;
using UnityEngine;


namespace Match
{
	/// <summary>
	/// Server side. Spawns a team's bodies and decides who drives each one: every body has its own
	/// AI controller, which steps aside while a human controls that body.
	/// </summary>
	public class TeamController : MonoBehaviour
	{
		[SerializeField]
		private TeamSide side;

		// One player type prefab per spawn point, in the same order
		[SerializeField]
		private NetworkObject[] lineup;

		[SerializeField]
		private Transform[] spawnPoints;

		[SerializeField]
		private TeamController opponents;

		// The goal this team shoots at
		[SerializeField]
		private Goal attackGoal;

		private readonly List<CharacterNetworkController> bodies = new();

		private readonly Dictionary<CharacterNetworkController, AIPlayerController> aiControllers = new();

		public TeamSide Side => side;

		public TeamController Opponents => opponents;

		public Goal AttackGoal => attackGoal;

		public Transform AttackTarget => attackGoal.transform;

		public IReadOnlyList<CharacterNetworkController> Bodies => bodies;

		// Server only
		public TeamBrain Brain { get; private set; }

		public bool IsAllKnockedOut => bodies.Count > 0 && bodies.All(b => b.IsKnockedOut);

		public void SpawnBodies(NetworkManager networkManager)
		{
			Brain = new TeamBrain(this);

			for (var i = 0; i < spawnPoints.Length; i++)
			{
				var spawnPoint = spawnPoints[i];

				var instance = networkManager.GetPooledInstantiated(
					lineup[i % lineup.Length],
					spawnPoint.position,
					spawnPoint.rotation,
					true);

				// Into the arena itself, so only clients in the arena are sent it
				networkManager.ServerManager.Spawn(instance, scene: gameObject.scene);

				var body = instance.GetComponent<CharacterNetworkController>();

				body.Initialise(this);

				var brain = new PlayerBrain(body, this);

				aiControllers[body] = new AIPlayerController(brain);

				bodies.Add(body);

				HandToAI(body);
			}
		}

		/// <summary>
		/// Puts every body back on its spawn point, for the kickoff after a goal.
		/// </summary>
		public void ResetPositions()
		{
			for (var i = 0; i < bodies.Count && i < spawnPoints.Length; i++)
				bodies[i].Teleport(spawnPoints[i].position, spawnPoints[i].rotation);
		}

		/// <summary>
		/// Gets everyone up and back on their spawn points for a new match.
		/// </summary>
		public void ResetForMatch()
		{
			foreach (var body in bodies)
				body.Revive();

			ResetPositions();
		}

		public void OnKnockedOut(CharacterNetworkController body)
		{
			if (ServiceLocator.TryGet(out Ball ball) && ball.Carrier == body)
				ball.Drop();

			if (body.IsHumanControlled)
				SwitchFrom(body);
		}

		private void FixedUpdate()
		{
			Brain?.Tick();
		}

		public void Clear()
		{
			Brain = null;

			bodies.Clear();
			aiControllers.Clear();
		}

		/// <summary>
		/// Gives a human a body no other human has, of their preferred type if that one is free.
		/// </summary>
		public bool TryAssignHuman(NetworkConnection connection, int localIndex, PlayerType preferredType)
		{
			if (bodies.Any(b => b.Owner == connection && b.LocalPlayerIndex == localIndex))
				return true;

			var free = bodies.Where(b => !b.IsHumanControlled && !b.IsKnockedOut).ToList();

			var body = free.FirstOrDefault(b => b.Type == preferredType) ?? free.FirstOrDefault();

			if (body == null)
				return false;

			HandToHuman(body, connection, localIndex);

			return true;
		}

		/// <summary>
		/// Moves a human from their body to the teammate no human controls that is nearest the ball.
		/// </summary>
		public void SwitchFrom(CharacterNetworkController body)
		{
			if (!body.IsHumanControlled)
				return;

			var focus = ServiceLocator.TryGet(out Ball ball)
				? ball.transform.position
				: body.transform.position;

			var target = NearestBodyTo(focus, aiOnly: true)?.GetComponent<CharacterNetworkController>();

			if (target == null)
				return;

			var connection = body.Owner;

			var localIndex = body.LocalPlayerIndex;

			HandToAI(body);

			HandToHuman(target, connection, localIndex);
		}

		public void ReleaseConnection(NetworkConnection connection)
		{
			foreach (var body in bodies.Where(b => b.Owner == connection))
				HandToAI(body);
		}

		// Called by every AI each tick, so no LINQ here
		public Transform NearestBodyTo(Vector3 position, bool aiOnly = false)
		{
			Transform nearest = null;

			var nearestDistance = float.MaxValue;

			foreach (var body in bodies)
			{
				if (body == null || body.IsKnockedOut || (aiOnly && body.IsHumanControlled))
					continue;

				var distance = (body.transform.position - position).sqrMagnitude;

				if (distance >= nearestDistance)
					continue;

				nearest = body.transform;
				nearestDistance = distance;
			}

			return nearest;
		}

		private void HandToHuman(CharacterNetworkController body, NetworkConnection connection, int localIndex)
		{
			aiControllers[body].Unbind();

			body.AssignHuman(connection, localIndex);
		}

		private void HandToAI(CharacterNetworkController body)
		{
			body.AssignAI(aiControllers[body]);
		}
	}
}
