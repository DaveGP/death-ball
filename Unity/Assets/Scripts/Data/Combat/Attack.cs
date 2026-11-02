using System;


namespace Data.Combat
{
	/// <summary>
	/// A melee move: the hit it deals, how far it reaches and how often it can be used.
	/// </summary>
	[Serializable]
	public struct Attack
	{
		public Hit Hit;

		public float Range;

		public float Cooldown;

		// How hard a landed hit throws the target away from the attacker
		public float Knockback;

		public Attack(Hit hit, float range, float cooldown, float knockback = 0f)
		{
			Hit = hit;
			Range = range;
			Cooldown = cooldown;
			Knockback = knockback;
		}
	}
}
