using System;
using Data.Enums;
using Data.Input;
using UnityEngine;


namespace AI
{
	using Interfaces;


	/// <summary>
	/// Placeholder brain until team tactics exist: runs at a target and attacks it when close.
	/// </summary>
	public class ChaseBrain : IAIBrain
	{
		private readonly Transform self;

		private readonly Func<Transform> getTarget;

		private readonly float attackRange;

		private readonly float attackCooldown;

		private float nextAttackTime;

		public ChaseBrain(Transform self, Func<Transform> getTarget, float attackRange = 1.5f, float attackCooldown = 1f)
		{
			this.self = self;
			this.getTarget = getTarget;
			this.attackRange = attackRange;
			this.attackCooldown = attackCooldown;
		}

		public CharacterInput Think()
		{
			var target = getTarget();

			if (target == null)
				return CharacterInput.None;

			var offset = target.position - self.position;

			var direction = new Vector2(offset.x, offset.z);

			var distance = direction.magnitude;

			if (distance < 0.01f)
				return CharacterInput.None;

			direction /= distance;

			if (distance > attackRange)
				return new CharacterInput(direction, direction, CharacterAction.None);

			if (Time.time < nextAttackTime)
				return new CharacterInput(Vector2.zero, direction, CharacterAction.None);

			nextAttackTime = Time.time + attackCooldown;

			return new CharacterInput(Vector2.zero, direction, CharacterAction.Attack);
		}
	}
}
