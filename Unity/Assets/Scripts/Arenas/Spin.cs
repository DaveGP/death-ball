using UnityEngine;


namespace Arenas
{
	/// <summary>
	/// Turns scenery around its pivot, such as a hologram circling above an arena. Only for looks, so it isn't
	/// networked and doesn't need to match between machines.
	/// </summary>
	public class Spin : MonoBehaviour
	{
		[SerializeField]
		private float degreesPerSecond = 12f;

		private void Update()
		{
			transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
		}
	}
}
