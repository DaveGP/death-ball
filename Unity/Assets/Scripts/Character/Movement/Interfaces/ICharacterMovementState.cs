using Data.Enums;
using UnityEngine;

namespace Character.Movement.Interfaces
{
	public interface ICharacterMovementState
	{
		Vector3 Velocity { get; }

		Vector3 LocalVelocity { get; }

		bool IsGrounded { get; }

		bool IsDodging { get; }

		CharacterGait Gait { get; }
	}
}
