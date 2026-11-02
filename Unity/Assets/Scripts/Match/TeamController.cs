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

		[SerializeField]
		private NetworkObject characterPrefab;

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

		public void SpawnBodies(NetworkManager networkManager)
		{
			foreach (var spawnPoint in spawnPoints)
			{
				var instance = networkManager.GetPooledInstantiated(
					characterPrefab,
					spawnPoint.position,
					spawnPoint.rotation,
					true);

				networkManager.ServerManager.Spawn(instance);

				var body = instance.GetComponent<CharacterNetworkController>();

				body.Initialise(this);

				var brain = new BallBrain(body, this, spawnPoint.position.x);

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

		public void Clear()
		{
			bodies.Clear();
			aiControllers.Clear();
		}

		public bool TryAssignHuman(NetworkConnection connection, int localIndex)
		{
			if (bodies.Any(b => b.Owner == connection && b.LocalPlayerIndex == localIndex))
				return true;

			var body = bodies.FirstOrDefault(b => !b.IsHumanControlled);

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
				if (body == null || (aiOnly && body.IsHumanControlled))
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
