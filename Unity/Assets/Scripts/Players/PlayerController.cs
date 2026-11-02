using Character.Input.Interfaces;
using Character.Networking;


namespace Players
{
	/// <summary>
	/// Something that drives one body at a time. Controllers belong to a slot on a team
	/// and can move between bodies; the body only ever sees the input source.
	/// </summary>
	public abstract class PlayerController
	{
		public CharacterNetworkController Body { get; private set; }

		protected abstract ICharacterInputSource InputSource { get; }

		public void Bind(CharacterNetworkController body)
		{
			Body = body;

			body.SetInputSource(InputSource);
		}

		public void Unbind()
		{
			Body = null;
		}
	}
}
