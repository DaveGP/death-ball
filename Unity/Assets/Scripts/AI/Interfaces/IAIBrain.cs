using Data.Input;


namespace AI.Interfaces
{
	public interface IAIBrain
	{
		/// <summary>
		/// Decides this tick's input, exactly as a human's controller would produce it.
		/// </summary>
		CharacterInput Think();
	}
}
