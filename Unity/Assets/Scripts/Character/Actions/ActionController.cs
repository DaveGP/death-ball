using UnityEngine;


namespace Character.Actions
{
	using Interfaces;


	/// <summary>
	/// Placeholder actions that draw debug rays until combat and the ball are built.
	/// </summary>
	public class ActionController : MonoBehaviour, IActionController
	{
		// Potentially move these out later
		[SerializeField]
		private float attackRange = 1.5f;

		[SerializeField]
		private float rayHeight = 1f;

		public bool IsBlocking { get; private set; }

		public void SetBlocking(bool blocking)
		{
			IsBlocking = blocking;
		}

		public void OnAttack()
			=> DrawAction(transform.forward, attackRange, Color.red);

		public void OnHeavyAttack()
			=> DrawAction(transform.forward, attackRange, Color.magenta);

		public void OnTackle()
			=> DrawAction(transform.forward, attackRange * 2f, Color.yellow);

		public void OnPass(Vector3 direction)
			=> DrawAction(direction, 10f, Color.cyan);

		public void OnShoot(Vector3 direction)
			=> DrawAction(direction, 20f, Color.green);

		public void OnSpecial()
			=> DrawAction(transform.forward, attackRange, Color.white);

		private void DrawAction(Vector3 direction, float range, Color color)
		{
			var origin = transform.position + Vector3.up * rayHeight;

			Debug.DrawRay(origin, direction * range, color, 0.5f);
		}
	}
}
