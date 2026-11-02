using Character.Actions.Interfaces;
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

		private void Awake()
		{
			movementController = GetComponent<IMovementController>();

			actionController = GetComponent<IActionController>();

			if (movementController == null)
				Debug.LogError($"{nameof(movementController)} is null");

			if (actionController == null)
				Debug.LogError($"{nameof(actionController)} is null");
		}

		public void ProcessInput(CharacterInput characterInput)
		{
			Move(characterInput);
			Look(characterInput);
			Actions(characterInput);
		}

		private void Move(CharacterInput characterInput)
		{
			HandleDodge(characterInput);

			HandleGait(characterInput);

			movementController.Move(Vector2.ClampMagnitude(characterInput.Move, 1f));
		}

		private void HandleDodge(CharacterInput characterInput)
		{
			if (characterInput.Action.HasFlag(CharacterAction.Dodge))
				movementController.Dodge(characterInput.Move);
		}

		private void HandleGait(CharacterInput characterInput)
		{
			var gait = CalculateGait(characterInput);

			movementController.SetGait(gait);
		}

		private CharacterGait CalculateGait(CharacterInput characterInput)
		{
			var input = characterInput.Move.magnitude;

			if (input == 0)
				return CharacterGait.Idle;

			if (characterInput.Action.HasFlag(CharacterAction.Sprint))
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
