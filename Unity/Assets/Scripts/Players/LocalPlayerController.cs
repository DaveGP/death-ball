using System;
using System.Collections.Generic;
using Character.Input;
using Character.Input.Interfaces;
using Data.Enums;
using Data.Input;
using UnityEngine.InputSystem;


namespace Players
{
	/// <summary>
	/// A human on this machine using their own devices. Handles switching teammates
	/// itself, so bodies never see the SwitchPlayer action.
	/// </summary>
	public class LocalPlayerController : PlayerController, ICharacterInputSource, IDisposable
	{
		public int Index { get; }

		public string Name { get; }

		// Chosen in the lobby
		public TeamSide Team { get; set; }

		public PlayerType PreferredType { get; set; }

		public IReadOnlyList<InputDevice> Devices { get; }

		public InputProvider InputProvider { get; }

		private readonly UnityInputReader inputReader;

		protected override ICharacterInputSource InputSource => this;

		public LocalPlayerController(
			int index,
			string name,
			TeamSide team,
			IReadOnlyList<InputDevice> devices,
			UnityInputReader inputReader,
			InputProvider inputProvider)
		{
			Index = index;
			Name = name;
			Team = team;
			Devices = devices;
			this.inputReader = inputReader;
			InputProvider = inputProvider;
		}

		public CharacterInput ConsumeCharacterInput()
		{
			var input = InputProvider.ConsumeCharacterInput();

			if (!input.Action.HasFlag(CharacterAction.SwitchPlayer))
				return input;

			Body?.RequestSwitch();

			return new CharacterInput(input.Move, input.Aim, input.Action & ~CharacterAction.SwitchPlayer);
		}

		public void Dispose()
		{
			inputReader.Dispose();
		}
	}
}
