using UnityEngine;


namespace Character.Actions.Interfaces
{
	public interface IActionController
	{
		/// <summary>
		/// Gets whether the character is carrying the ball.
		/// </summary>
		public bool IsCarrying { get; }

		/// <summary>
		/// Gets whether the character is blocking.
		/// </summary>
		public bool IsBlocking { get; }

		/// <summary>
		/// Sets whether the character is blocking.
		/// </summary>
		public void SetBlocking(bool blocking);

		/// <summary>
		/// Triggers a light melee attack.
		/// </summary>
		public void OnAttack();

		/// <summary>
		/// Triggers a heavy melee attack.
		/// </summary>
		public void OnHeavyAttack();

		/// <summary>
		/// Triggers a tackle.
		/// </summary>
		public void OnTackle();

		/// <summary>
		/// Passes the ball in a world-space direction.
		/// </summary>
		public void OnPass(Vector3 direction);

		/// <summary>
		/// Shoots the ball in a world-space direction.
		/// </summary>
		public void OnShoot(Vector3 direction);

		/// <summary>
		/// Triggers the character's special move.
		/// </summary>
		public void OnSpecial();
	}
}
