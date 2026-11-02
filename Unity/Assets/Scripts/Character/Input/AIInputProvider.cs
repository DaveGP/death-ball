using AI.Interfaces;
using Data.Input;


namespace Character.Input
{
	using Interfaces;


	public class AIInputProvider : ICharacterInputSource
	{
		private readonly IAIBrain brain;

		public AIInputProvider(IAIBrain brain)
		{
			this.brain = brain;
		}

		public CharacterInput ConsumeCharacterInput()
			=> brain.Think();
	}
}
