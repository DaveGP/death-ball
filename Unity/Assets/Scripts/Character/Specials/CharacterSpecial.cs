using Character.Networking;
using Data.ScriptableObjects;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;


namespace Character.Specials
{
	/// <summary>
	/// The special meter, charged on the server by dealing and taking damage, and the move
	/// it unleashes when full.
	/// </summary>
	public class CharacterSpecial : NetworkBehaviour
	{
		[SerializeField]
		private CharacterStats stats;

		[SerializeField]
		private SpecialMove move;

		private readonly SyncVar<float> meter = new();

		private CharacterNetworkController body;

		public float Fraction => Mathf.Clamp01(meter.Value / stats.SpecialMeterMax);

		public bool IsReady => meter.Value >= stats.SpecialMeterMax;

		public SpecialMove Move => move;

		// Server only
		public bool ShouldAIUse => IsReady && move.ShouldAIUse(body);

		private void Awake()
		{
			body = GetComponent<CharacterNetworkController>();

			if (stats == null)
				Debug.LogError($"{nameof(stats)} is null", this);

			if (move == null)
				Debug.LogError($"{nameof(move)} is null", this);
		}

		[Server]
		public void Charge(float amount)
		{
			meter.Value = Mathf.Min(stats.SpecialMeterMax, meter.Value + amount);
		}

		[Server]
		public void Empty()
		{
			meter.Value = 0f;
		}

		[Server]
		public bool TryUse()
		{
			if (!IsReady || !move.Perform(body))
				return false;

			meter.Value = 0f;

			PlayEffect(transform.position);

			return true;
		}

		[ObserversRpc]
		private void PlayEffect(Vector3 position)
		{
			move.PlayEffect(position);
		}
	}
}
