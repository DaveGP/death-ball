using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using GameInput;
using Data.Enums;
using Data.Input;


namespace Character.Input
{
	using Interfaces;


	/// <summary>
	/// Reads one local player's devices. Each couch player gets their own reader and
	/// PlayerControls instance, restricted to the devices they joined with.
	/// </summary>
	public class UnityInputReader : IInputReader, IDisposable
	{
		private readonly PlayerControls playerControls;

		public UnityInputReader(PlayerControls playerControls, InputDevice[] devices)
		{
			this.playerControls = playerControls;

			playerControls.devices = new ReadOnlyArray<InputDevice>(devices);

			playerControls.Player.Enable();
		}

		public InputData GetInputData()
			=> new InputData(
				Movement,
				Aim,
				GetActions());

		public void Dispose()
		{
			playerControls.Dispose();
		}

		private Vector2 Movement => playerControls.Player.Move.ReadValue<Vector2>();

		private Vector2 Aim => playerControls.Player.Aim.ReadValue<Vector2>();

		private CharacterAction GetActions()
		{
			var player = playerControls.Player;

			var actions = CharacterAction.None;

			if (player.Sprint.IsPressed())
				actions |= CharacterAction.Sprint;

			if (player.Block.IsPressed())
				actions |= CharacterAction.Block;

			if (player.Pass.WasPressedThisFrame())
				actions |= CharacterAction.Pass;

			if (player.Shoot.IsPressed())
				actions |= CharacterAction.ChargeShot;

			// Fires on release, so a quick tap is an uncharged shot
			if (player.Shoot.WasReleasedThisFrame())
				actions |= CharacterAction.Shoot;

			if (player.Attack.WasPressedThisFrame())
				actions |= CharacterAction.Attack;

			if (player.HeavyAttack.WasPressedThisFrame())
				actions |= CharacterAction.HeavyAttack;

			if (player.Tackle.WasPressedThisFrame())
				actions |= CharacterAction.Tackle;

			if (player.Dodge.WasPressedThisFrame())
				actions |= CharacterAction.Dodge;

			if (player.Special.WasPressedThisFrame())
				actions |= CharacterAction.Special;

			if (player.SwitchPlayer.WasPressedThisFrame())
				actions |= CharacterAction.SwitchPlayer;

			return actions;
		}
	}
}
