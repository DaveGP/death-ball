using System.Collections.Generic;
using UnityEngine;


namespace Hazards
{
	/// <summary>
	/// Every hazard in the current arena, so the AI can get out of their way.
	/// </summary>
	public class HazardMap
	{
		private readonly List<Hazard> hazards = new();

		public void Add(Hazard hazard)
			=> hazards.Add(hazard);

		public void Remove(Hazard hazard)
			=> hazards.Remove(hazard);

		/// <summary>
		/// Whether a position is in danger, and if so which way is out.
		/// </summary>
		public bool TryGetEscape(Vector3 position, out Vector2 direction)
		{
			foreach (var hazard in hazards)
			{
				if (!hazard.IsThreatening(position))
					continue;

				direction = hazard.EscapeDirection(position);

				return true;
			}

			direction = Vector2.zero;

			return false;
		}
	}
}
