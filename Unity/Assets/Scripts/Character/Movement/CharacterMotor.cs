using Character.Actions.Interfaces;
using Character.Networking;
using Character.Stamina;
using Data.ScriptableObjects;
using Data.Enums;
using Data.Input;
using UnityEngine;


namespace Character.Movement
{
	using Interfaces;


	public class CharacterMotor : MonoBehaviour, ICharacterMotor
	{
		private const float RunThreshold = 0.6f;

		private const float AimDeadZone = 0.2f;

		private IMovementController movementController;

		private IActionController actionController;

		private CharacterStamina stamina;

		private CharacterStats stats;

		private CharacterNetworkController body;

		private bool isAimingShot;

		public Vector3 ShotAim { get; private set; }

		private void Awake()
		{
			movementController = GetComponent<IMovementController>();

			actionController = GetComponent<IActionController>();

			stamina = GetComponent<CharacterStamina>();

			body = GetComponent<CharacterNetworkController>();

			stats = GetComponent<MovementController>().Stats;

			if (movementController == null)
				Debug.LogError($"{nameof(movementController)} is null");

			if (actionController == null)
				Debug.LogError($"{nameof(actionController)} is null");

			if (stamina == null)
				Debug.LogError($"{nameof(stamina)} is null");
		}

		public void ProcessInput(CharacterInput characterInput)
		{
			actionController.UpdateShotCharge(characterInput.Action.HasFlag(CharacterAction.ChargeShot));

			Move(characterInput);
			Look(characterInput);
			Actions(characterInput);

			if (!actionController.IsChargingShot)
				actionController.ResetShotCharge();
		}

		// The aim starts where the player is aiming or facing, then swings at a fixed rate, so
		// keys can land between their eight directions. Move steers it if nothing is aiming.
		public CharacterInput ResolveShotAim(CharacterInput characterInput)
		{
			var action = characterInput.Action;

			var charging = action.HasFlag(CharacterAction.ChargeShot) && actionController.IsCarrying;

			if (!charging && !action.HasFlag(CharacterAction.Shoot))
			{
				isAimingShot = false;

				return characterInput;
			}

			if (!isAimingShot)
				ShotAim = AimDirection(characterInput);
			else if (charging)
				SwingShotAim(characterInput);

			isAimingShot = charging;

			characterInput.Aim = new Vector2(ShotAim.x, ShotAim.z);

			return characterInput;
		}

		private void SwingShotAim(CharacterInput characterInput)
		{
			var target = characterInput.Aim.sqrMagnitude > AimDeadZone * AimDeadZone
				? characterInput.Aim
				: characterInput.Move;

			if (target.sqrMagnitude <= AimDeadZone * AimDeadZone)
				return;

			ShotAim = Vector3.RotateTowards(
				ShotAim,
				new Vector3(target.x, 0f, target.y).normalized,
				stats.ShotAimTurnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime,
				0f);
		}

		private void Move(CharacterInput characterInput)
		{
			HandleDodge(characterInput);

			HandleGait(characterInput);

			movementController.SetCarrying(actionController.IsCarrying);

			movementController.SetChargingShot(actionController.IsChargingShot);

			movementController.SetHasted(body.IsHasted);

			movementController.Move(Vector2.ClampMagnitude(characterInput.Move, 1f));
		}

		// Only the server spends stamina; the owner just checks what the server says is left
		private void HandleDodge(CharacterInput characterInput)
		{
			if (!characterInput.Action.HasFlag(CharacterAction.Dodge) || !stamina.Has(stats.DodgeStaminaCost))
				return;

			if (movementController.Dodge(characterInput.Move) && stamina.IsServer)
				stamina.Drain(stats.DodgeStaminaCost);
		}

		private void HandleGait(CharacterInput characterInput)
		{
			var gait = CalculateGait(characterInput);

			movementController.SetGait(gait);

			// Sprinting is free while hasted
			if (gait == CharacterGait.Sprint && !body.IsHasted && stamina.IsServer)
				stamina.Drain(stats.SprintStaminaPerSecond * Time.fixedDeltaTime);
		}

		private CharacterGait CalculateGait(CharacterInput characterInput)
		{
			var input = characterInput.Move.magnitude;

			if (input == 0)
				return CharacterGait.Idle;

			if (characterInput.Action.HasFlag(CharacterAction.Sprint) && !stamina.IsEmpty && !actionController.IsChargingShot)
				return CharacterGait.Sprint;

			return input < RunThreshold
				? CharacterGait.Walk
				: CharacterGait.Run;
		}

		// Face where the player is aiming, otherwise where they're going
		private void Look(CharacterInput characterInput)
		{
			var direction = characterInput.Aim.sqrMagnitude > AimDeadZone * AimDeadZone
				? characterInput.Aim
				: characterInput.Move;

			movementController.Face(direction);
		}

		private void Actions(CharacterInput characterInput)
		{
			var characterAction = characterInput.Action;

			actionController.SetBlocking(characterAction.HasFlag(CharacterAction.Block));

			if (characterAction.HasFlag(CharacterAction.Attack))
				actionController.OnAttack();

			if (characterAction.HasFlag(CharacterAction.HeavyAttack))
				actionController.OnHeavyAttack();

			if (characterAction.HasFlag(CharacterAction.Tackle))
				actionController.OnTackle();

			if (characterAction.HasFlag(CharacterAction.Pass))
				actionController.OnPass(AimDirection(characterInput));

			if (characterAction.HasFlag(CharacterAction.Shoot))
				actionController.OnShoot(AimDirection(characterInput));

			if (characterAction.HasFlag(CharacterAction.Special))
				actionController.OnSpecial();
		}

		private Vector3 AimDirection(CharacterInput characterInput)
		{
			if (characterInput.Aim.sqrMagnitude > AimDeadZone * AimDeadZone)
				return new Vector3(characterInput.Aim.x, 0f, characterInput.Aim.y).normalized;

			return transform.forward;
		}
	}
}
