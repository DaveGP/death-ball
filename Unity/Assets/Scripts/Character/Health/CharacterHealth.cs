using Character.Actions.Interfaces;
using Character.Movement;
using Character.Movement.Interfaces;
using Character.Networking;
using Character.Specials;
using Character.Stamina;
using Data.Combat;
using Data.ScriptableObjects;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;


namespace Character.Health
{
	/// <summary>
	/// A body's health, and the rules for taking a hit. Only the server deals damage;
	/// health reaching zero knocks the body out for the rest of the match.
	/// </summary>
	public class CharacterHealth : NetworkBehaviour
	{
		[SerializeField]
		private CharacterStats stats;

		private readonly SyncVar<int> currentHealth = new();

		private CharacterNetworkController body;

		private IActionController actionController;

		private ICharacterMovementState movementState;

		private CharacterStamina stamina;

		private CharacterSpecial special;

		public int CurrentHealth => currentHealth.Value;

		public int MaxHealth => stats.MaxHealth;

		public float Fraction => Mathf.Clamp01((float)currentHealth.Value / stats.MaxHealth);

		private void Awake()
		{
			body = GetComponent<CharacterNetworkController>();

			actionController = GetComponent<IActionController>();

			// The movement itself rather than the network controller's synced copy
			movementState = GetComponent<MovementController>();

			stamina = GetComponent<CharacterStamina>();

			special = GetComponent<CharacterSpecial>();

			if (stats == null)
				Debug.LogError($"{nameof(stats)} is null", this);
		}

		public override void OnStartServer()
		{
			base.OnStartServer();

			Restore();
		}

		[Server]
		public void Restore()
		{
			currentHealth.Value = stats.MaxHealth;
		}

		/// <summary>
		/// Applies a hit, unless the body dodged it or is already down. The attacker, if there
		/// is one, charges their special meter from the damage dealt, as the target does from
		/// the damage taken.
		/// </summary>
		[Server]
		public HitResult TakeHit(Hit hit, CharacterNetworkController attacker = null)
		{
			if (body.IsKnockedOut || movementState.IsDodging)
				return HitResult.Missed;

			// Iron Wall: nothing gets through the guard, and hitting it rebounds on the attacker
			if (body.IsFortified && actionController.IsBlocking)
			{
				if (attacker != null)
					attacker.Stagger(stats.FortifyCounterStagger);

				return HitResult.Blocked;
			}

			var damage = (float)hit.Damage;

			var stagger = hit.Stagger;

			// Blocking costs stamina, and there's no guard left once it runs out
			var blocked = actionController.IsBlocking && !hit.BreaksBlock && !stamina.IsEmpty;

			if (blocked)
			{
				damage *= stats.BlockedDamageMultiplier;

				stamina.Drain(hit.Damage * stats.BlockStaminaPerDamage);

				// The hit that empties it breaks the guard and staggers them anyway
				stagger = stamina.IsEmpty ? hit.Stagger : 0f;
			}

			// Kicking someone while they're down
			if (body.IsStaggered)
				damage *= stats.StaggeredDamageMultiplier;

			var dealt = Mathf.Min(currentHealth.Value, Mathf.RoundToInt(damage));

			currentHealth.Value -= dealt;

			special.Charge(dealt * stats.SpecialChargePerDamageTaken);

			if (attacker != null)
				attacker.GetComponent<CharacterSpecial>().Charge(dealt * stats.SpecialChargePerDamageDealt);

			if (currentHealth.Value == 0)
				body.KnockOut();
			else if (stagger > 0f)
				body.Stagger(stagger);

			return blocked ? HitResult.Blocked : HitResult.Landed;
		}
	}
}
