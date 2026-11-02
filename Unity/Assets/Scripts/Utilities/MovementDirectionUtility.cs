using Data.Enums;
using UnityEngine;

namespace Utilities
{
	public static class MovementDirectionUtility
	{
		private const float MovementThreshold = 0.01f;

		public static MovementDirection FromLocalVelocity(Vector3 localVelocity)
		{
			if (localVelocity.sqrMagnitude < MovementThreshold * MovementThreshold)
				return MovementDirection.None;

			var angle = Mathf.Atan2(localVelocity.x, localVelocity.z) * Mathf.Rad2Deg;
			angle = (angle + 360f) % 360f;

			var direction = Mathf.RoundToInt(angle / 45f) % 8;

			return (MovementDirection)direction;
		}
	}
}