using Data.Input;


namespace Character.Movement.Interfaces
{
	public interface ICharacterMotor
	{
		/// <summary>
		/// Processes the supplied character input.
		/// </summary>
		public void ProcessInput(CharacterInput characterInput);
	}
}
