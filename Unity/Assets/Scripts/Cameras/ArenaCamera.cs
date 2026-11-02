using System.Linq;
using Character.Input.Interfaces;
using DependencyInjection;
using Players;
using UnityEngine;


namespace Cameras
{
	/// <summary>
	/// One shared camera for every local player: a fixed angle that follows and frames
	/// the bodies this machine's players control. Stick input is relative to its yaw.
	/// </summary>
	public class ArenaCamera : MonoBehaviour, IInputView
	{
		[SerializeField]
		private float pitch = 55f;

		[SerializeField]
		private float yaw;

		[SerializeField]
		private float minDistance = 14f;

		[SerializeField]
		private float distancePerMetreSpread = 0.8f;

		[SerializeField]
		private float followSharpness = 5f;

		private LocalPlayerRegistry localPlayers;

		private Vector3 focus;

		public Quaternion ViewRotation => Quaternion.Euler(0f, yaw, 0f);

		private void Awake()
		{
			localPlayers = ServiceLocator.Get<LocalPlayerRegistry>();
		}

		private void LateUpdate()
		{
			var targets = localPlayers.Players
				.Where(p => p.Body != null)
				.Select(p => p.Body.transform.position)
				.ToArray();

			var bounds = new Bounds(targets.Length > 0 ? targets[0] : Vector3.zero, Vector3.zero);

			foreach (var target in targets)
				bounds.Encapsulate(target);

			var blend = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);

			focus = Vector3.Lerp(focus, bounds.center, blend);

			var distance = minDistance + bounds.size.magnitude * distancePerMetreSpread;

			var rotation = Quaternion.Euler(pitch, yaw, 0f);

			transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
		}
	}
}
