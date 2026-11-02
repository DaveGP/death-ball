using Data.Enums;
using UnityEngine;


namespace Character.Movement.Interfaces
{
	public interface IMovementController
	{
		/// <summary>
		/// Applies world-space movement on the XZ plane to the character.
		/// </summary>
		public void Move(Vector2 direction);

		/// <summary>
		/// Turns the character towards a world-space direction on the XZ plane.
		/// </summary>
		public void Face(Vector2 direction);

		/// <summary>
		/// Attempts to make the character dodge in a world-space direction on the XZ plane.
		/// </summary>
		public void Dodge(Vector2 direction);

		/// <summary>
		/// Sets the Character's Gait.
		/// </summary>
		public void SetGait(CharacterGait gait);

		/// <summary>
		/// Sets whether the character is slowed by carrying the ball.
		/// </summary>
		public void SetCarrying(bool carrying);
	}
}
