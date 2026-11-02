namespace Data.Enums
{
	public static class CharacterActionGroups
	{
		public const CharacterAction Buffered =
			CharacterAction.Pass |
			CharacterAction.Shoot |
			CharacterAction.Attack |
			CharacterAction.HeavyAttack |
			CharacterAction.Tackle |
			CharacterAction.Dodge |
			CharacterAction.Special |
			CharacterAction.SwitchPlayer;

		public const CharacterAction Continuous =
			CharacterAction.Sprint |
			CharacterAction.Block;
	}
}
