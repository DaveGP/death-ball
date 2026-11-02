using System;


namespace Data.Enums
{
	[Flags]
	public enum CharacterAction
	{
		None = 0,

		Sprint = 1 << 0,

		Block = 1 << 1,

		Pass = 1 << 2,

		Shoot = 1 << 3,

		Attack = 1 << 4,

		HeavyAttack = 1 << 5,

		Tackle = 1 << 6,

		Dodge = 1 << 7,

		Special = 1 << 8,

		SwitchPlayer = 1 << 9
	}
}
