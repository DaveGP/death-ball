using UnityEngine;

namespace Data.ScriptableObjects
{
	[CreateAssetMenu(fileName = "CharacterStats", menuName = "ScriptableObjects/CharacterStats", order = 1)]
	public class CharacterStats : ScriptableObject
	{
		public float WalkSpeed = 2.5f;

		public float RunSpeed = 5.5f;

		public float SprintSpeed = 8f;

		public float Acceleration = 40f;

		public float TurnSpeed = 720f;

		public float DodgeSpeed = 12f;

		public float DodgeDuration = 0.2f;

		public float DodgeCooldown = 0.6f;
	}
}
