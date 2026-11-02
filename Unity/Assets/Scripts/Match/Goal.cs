using Data.Enums;
using UnityEngine;


namespace Match
{
	/// <summary>
	/// A raised goal ring. Its forward axis points out at the field, and the ball scores
	/// by passing through the ring from that side.
	/// </summary>
	public class Goal : MonoBehaviour
	{
		// The team defending this goal
		[SerializeField]
		private TeamSide owner;

		[SerializeField]
		private float radius = 1f;

		public TeamSide Owner => owner;

		/// <summary>
		/// Whether moving from one point to the next went in through the ring. Checks the path
		/// rather than the position, so a fast shot can't skip over it between ticks.
		/// </summary>
		public bool IsThrough(Vector3 from, Vector3 to)
		{
			var centre = transform.position;
			var normal = transform.forward;

			var fromSide = Vector3.Dot(from - centre, normal);
			var toSide = Vector3.Dot(to - centre, normal);

			if (fromSide <= 0f || toSide > 0f)
				return false;

			var crossing = Vector3.Lerp(from, to, fromSide / (fromSide - toSide));

			return (crossing - centre).sqrMagnitude <= radius * radius;
		}
	}
}
