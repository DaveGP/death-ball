using Data.ScriptableObjects;
using Unity.Netcode;
using UnityEngine;


namespace Character.Stamina
{
	/// <summary>
	/// Stamina for sprinting, dodging, heavy attacks, tackles and holding a block. The server
	/// spends and regenerates it; every machine reads the synced value, so the owning client
	/// stops sprinting or dodging once the server says it has run out.
	/// </summary>
	public class CharacterStamina : NetworkBehaviour
	{
		[SerializeField]
		private CharacterStats stats;

		private readonly NetworkVariable<float> current = new();

		private float regenResumeTime;

		public float Current => current.Value;

		public float Fraction => Mathf.Clamp01(current.Value / stats.MaxStamina);

		public bool IsEmpty => current.Value <= 0f;

		private void Awake()
		{
			if (stats == null)
				Debug.LogError($"{nameof(stats)} is null", this);
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();

			if (!IsServer)
				return;

			Restore();
		}

		public bool Has(float amount)
			=> current.Value >= amount;

		public void Restore()
		{
			current.Value = stats.MaxStamina;
		}

		/// <summary>
		/// Spends the amount if there's enough, for actions that can't happen without it.
		/// </summary>
		public bool TrySpend(float amount)
		{
			if (!Has(amount))
				return false;

			Drain(amount);

			return true;
		}

		/// <summary>
		/// Takes away up to the amount, for sprinting and blocked hits, which use whatever is left.
		/// </summary>
		public void Drain(float amount)
		{
			current.Value = Mathf.Max(0f, current.Value - amount);

			regenResumeTime = Time.time + stats.StaminaRegenDelay;
		}

		private void FixedUpdate()
		{
			if (!IsSpawned || !IsServer || Time.time < regenResumeTime || current.Value >= stats.MaxStamina)
				return;

			current.Value = Mathf.Min(stats.MaxStamina, current.Value + stats.StaminaRegen * Time.fixedDeltaTime);
		}
	}
}
