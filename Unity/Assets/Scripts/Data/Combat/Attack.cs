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

		public Attack(Hit hit, float range, float cooldown)
		{
			Hit = hit;
			Range = range;
			Cooldown = cooldown;
		}
	}
}
