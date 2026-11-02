using System.Collections.Generic;
using System.Linq;
using AI;
using Character.Networking;
using Data.Enums;
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

		private readonly List<CharacterNetworkController> bodies = new();

		private readonly Dictionary<CharacterNetworkController, AIPlayerController> aiControllers = new();

		public TeamSide Side => side;

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

				var brain = new ChaseBrain(body.transform, () => opponents.NearestBodyTo(body.transform.position));

				aiControllers[body] = new AIPlayerController(brain);

				bodies.Add(body);

				HandToAI(body);
			}
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
		/// Moves a human from their body to the nearest teammate no human controls.
		/// </summary>
		public void SwitchFrom(CharacterNetworkController body)
		{
			if (!body.IsHumanControlled)
				return;

			var target = bodies
				.Where(b => !b.IsHumanControlled)
				.OrderBy(b => (b.transform.position - body.transform.position).sqrMagnitude)
				.FirstOrDefault();

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

		public Transform NearestBodyTo(Vector3 position)
		{
			return bodies
				.Where(b => b != null)
				.OrderBy(b => (b.transform.position - position).sqrMagnitude)
				.Select(b => b.transform)
				.FirstOrDefault();
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
