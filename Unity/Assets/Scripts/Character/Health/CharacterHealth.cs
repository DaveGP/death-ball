using Character.Actions.Interfaces;
using Character.Movement;
using Character.Movement.Interfaces;
using Character.Networking;
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

		public int CurrentHealth => currentHealth.Value;

		public int MaxHealth => stats.MaxHealth;

		public float Fraction => Mathf.Clamp01((float)currentHealth.Value / stats.MaxHealth);

		private void Awake()
		{
			body = GetComponent<CharacterNetworkController>();

			actionController = GetComponent<IActionController>();

			// The movement itself rather than the network controller's synced copy
			movementState = GetComponent<MovementController>();

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
		/// Applies a hit, unless the body dodged it or is already down.
		/// </summary>
		[Server]
		public HitResult TakeHit(Hit hit)
		{
			if (body.IsKnockedOut || movementState.IsDodging)
				return HitResult.Missed;

			var damage = (float)hit.Damage;

			var stagger = hit.Stagger;

			var blocked = actionController.IsBlocking && !hit.BreaksBlock;

			if (blocked)
			{
				damage *= stats.BlockedDamageMultiplier;
				stagger = 0f;
			}

			// Kicking someone while they're down
			if (body.IsStaggered)
				damage *= stats.StaggeredDamageMultiplier;

			currentHealth.Value = Mathf.Max(0, currentHealth.Value - Mathf.RoundToInt(damage));

			if (currentHealth.Value == 0)
				body.KnockOut();
			else if (stagger > 0f)
				body.Stagger(stagger);

			return blocked ? HitResult.Blocked : HitResult.Landed;
		}
	}
}
