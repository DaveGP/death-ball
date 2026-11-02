using UnityEngine;

namespace Data.ScriptableObjects
{
	[CreateAssetMenu(fileName = "CharacterStats", menuName = "ScriptableObjects/CharacterStats", order = 1)]
	public class CharacterStats : ScriptableObject
	{
		public float WalkSpeed = 2.5f;

		public float RunSpeed = 5.5f;

		public float SprintSpeed = 8f;

		public float CarrySpeedMultiplier = 0.85f;

		// While hasted by a special
		public float HasteSpeedMultiplier = 1.35f;

		// How long an attacker reels after hitting a fortified guard
		public float FortifyCounterStagger = 0.8f;

		// Scales the damage of light and heavy attacks and tackles
		public float MeleeDamageMultiplier = 1f;

		// Scales pass and shot speed
		public float ThrowSpeedMultiplier = 1f;

		public float Acceleration = 40f;

		public float TurnSpeed = 720f;

		public float DodgeSpeed = 12f;

		public float DodgeDuration = 0.2f;

		public float DodgeCooldown = 0.6f;

		public int MaxHealth = 200;

		// Share of damage that gets through a block
		public float BlockedDamageMultiplier = 0.1f;

		// Extra damage against someone already staggered
		public float StaggeredDamageMultiplier = 1.5f;

		public float MaxStamina = 100f;

		public float StaminaRegen = 25f;

		// Seconds after spending before stamina starts coming back
		public float StaminaRegenDelay = 0.8f;

		public float SprintStaminaPerSecond = 15f;

		public float DodgeStaminaCost = 25f;

		public float HeavyAttackStaminaCost = 20f;

		public float TackleStaminaCost = 15f;

		// Stamina a blocked hit drains, per point of the hit's damage. Running out breaks the guard.
		public float BlockStaminaPerDamage = 1f;

		public float SpecialMeterMax = 100f;

		// Special meter gained per point of damage dealt and taken
		public float SpecialChargePerDamageDealt = 0.5f;

		public float SpecialChargePerDamageTaken = 0.4f;
	}
}
