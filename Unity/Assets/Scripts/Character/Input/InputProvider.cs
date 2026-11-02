using UnityEngine;
using Character.Input.Interfaces;
using Data.Enums;
using Data.Input;
using DependencyInjection;
using SystemManagement.Interfaces;


namespace Character.Input
{
	/// <summary>
	/// Buffers raw input read during Update and exposes it for
	/// consumption during FixedUpdate, turned into world-space directions.
	/// </summary>
	public class InputProvider : IUpdateSystem, ICharacterInputSource
	{
		private readonly IInputReader inputReader;

		private Vector2 movement;

		private Vector2 aim;

		private CharacterAction actions;


		public InputProvider(IInputReader inputReader)
		{
			this.inputReader = inputReader;

			if (inputReader == null)
				Debug.LogError("InputProvider is null!");
		}

		public CharacterInput ConsumeCharacterInput()
		{
			var characterInput = new CharacterInput(
				ToWorld(movement),
				ToWorld(aim),
				actions);

			ClearBufferedActions();

			return characterInput;
		}

		public void RunUpdate()
		{
			ReadInput();
		}

		private void ReadInput()
		{
			var inputData = inputReader.GetInputData();

			movement = inputData.Move;

			aim = inputData.Aim;

			ClearContinuousActions();

			BufferActions(inputData.Action);
		}

		private Vector2 ToWorld(Vector2 input)
		{
			// Whichever camera the current scene registered; raw input in menus
			if (!ServiceLocator.TryGet(out IInputView inputView))
				return input;

			var world = inputView.ViewRotation * new Vector3(input.x, 0f, input.y);

			return new Vector2(world.x, world.z);
		}

		private void BufferActions(CharacterAction action)
		{
			actions |= action;
		}

		private void ClearContinuousActions()
		{
			actions &= ~CharacterActionGroups.Continuous;
		}

		private void ClearBufferedActions()
		{
			actions &= ~CharacterActionGroups.Buffered;
		}
	}
}
