using Data.Input;


namespace Character.Input
{
	using Interfaces;


	/// <summary>
	/// Drives nothing. Used for bodies nobody on this machine controls, knocked out players and cutscenes.
	/// </summary>
	public class NullInputSource : ICharacterInputSource
	{
		public static readonly NullInputSource Instance = new();

		public CharacterInput ConsumeCharacterInput()
			=> CharacterInput.None;
	}
}
