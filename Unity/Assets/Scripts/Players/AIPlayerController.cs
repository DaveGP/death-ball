using AI.Interfaces;
using Character.Input;
using Character.Input.Interfaces;


namespace Players
{
	/// <summary>
	/// An AI driving a body. Only exists on the server.
	/// </summary>
	public class AIPlayerController : PlayerController
	{
		protected override ICharacterInputSource InputSource { get; }

		public AIPlayerController(IAIBrain brain)
		{
			InputSource = new AIInputProvider(brain);
		}
	}
}
