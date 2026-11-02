using Data.Input;


namespace Character.Input.Interfaces
{
	public interface ICharacterInputSource
	{
		/// <summary>
		/// Gets the latest input for this source.
		/// </summary>
		CharacterInput ConsumeCharacterInput();
	}
}