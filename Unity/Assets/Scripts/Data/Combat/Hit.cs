using System;
using UnityEngine;


namespace Data.Combat
{
	/// <summary>
	/// One blow landing on a character: how much it hurts, how long it staggers them,
	/// and whether blocking still lets it through.
	/// </summary>
	[Serializable]
	public struct Hit
	{
		public int Damage;

		public float Stagger;

		[Tooltip("Breaks through a block, dealing full damage and stagger")]
		public bool BreaksBlock;

		public Hit(int damage, float stagger, bool breaksBlock = false)
		{
			Damage = damage;
			Stagger = stagger;
			BreaksBlock = breaksBlock;
		}
	}
}
