using Data.Input;
using UnityEngine;


namespace Character.Movement.Interfaces
{
	public interface ICharacterMotor
	{
		/// <summary>
		/// Processes the supplied character input.
		/// </summary>
		public void ProcessInput(CharacterInput characterInput);

		/// <summary>
		/// Gets the world-space direction a charged shot is aimed, on the machine that moves this body.
		/// </summary>
		public Vector3 ShotAim { get; }

		/// <summary>
		/// Owner only. Works out where a shot is aimed and writes it into the input, so the
		/// server fires exactly where the owner saw it.
		/// </summary>
		public CharacterInput ResolveShotAim(CharacterInput characterInput);
	}
}
