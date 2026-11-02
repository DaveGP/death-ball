using Character.Networking;
using DependencyInjection;
using Match;
using UnityEngine;


namespace Character.Actions
{
	using Interfaces;


	/// <summary>
	/// Ball handling and placeholder combat. Runs on the owning client and the server,
	/// but only the server touches the ball, so clients just draw the debug rays.
	/// </summary>
	public class ActionController : MonoBehaviour, IActionController
	{
		// Potentially move these out later
		[SerializeField]
		private float attackRange = 1.5f;

		[SerializeField]
		private float tackleRange = 2.2f;

		// How far either side of facing a hit can land
		[SerializeField]
		private float strikeAngle = 60f;

		[SerializeField]
		private float stripStagger = 1f;

		[SerializeField]
		private float rayHeight = 1f;

		[SerializeField]
		private float passSpeed = 14f;

		[SerializeField]
		private float passLift = 2f;

		// Passes snap to a teammate within this angle of the aim
		[SerializeField]
		private float passAssistAngle = 35f;

		[SerializeField]
		private float shootSpeed = 22f;

		[SerializeField]
		private float shootLift = 3f;

		// Shots aimed within this angle of the goal arc to ring height
		[SerializeField]
		private float shotAssistAngle = 30f;

		private CharacterNetworkController body;

		public bool IsBlocking { get; private set; }

		public bool IsCarrying => TryGetBall(out var ball) && ball.Carrier == body;

		private void Awake()
		{
			body = GetComponent<CharacterNetworkController>();

			if (body == null)
				Debug.LogError($"{nameof(body)} is null");
		}

		public void SetBlocking(bool blocking)
		{
			IsBlocking = blocking;
		}

		// The carrier's hands are full, they can only pass, shoot or barge
		public void OnAttack()
		{
			if (IsCarrying)
				return;

			DrawAction(transform.forward, attackRange, Color.red);

			Strike(attackRange);
		}

		public void OnHeavyAttack()
		{
			if (IsCarrying)
				return;

			DrawAction(transform.forward, attackRange, Color.magenta);

			Strike(attackRange);
		}

		public void OnTackle()
		{
			DrawAction(transform.forward, tackleRange, Color.yellow);

			Strike(tackleRange);
		}

		public void OnPass(Vector3 direction)
		{
			if (!CanThrow(out var ball))
				return;

			var aim = PassDirection(direction);

			DrawAction(aim, 10f, Color.cyan);

			ball.Throw(body, aim * passSpeed + Vector3.up * passLift);
		}

		public void OnShoot(Vector3 direction)
		{
			if (!CanThrow(out var ball))
				return;

			DrawAction(direction, 20f, Color.green);

			ball.Throw(body, ShotVelocity(ball, direction));
		}

		// Aiming roughly at the goal lofts the shot to arrive at ring height. The aim
		// still decides left and right, so shots can miss.
		private Vector3 ShotVelocity(Ball ball, Vector3 direction)
		{
			var velocity = direction * shootSpeed + Vector3.up * shootLift;

			var toGoal = body.TeamController.AttackTarget.position - ball.HoldPosition(body);

			var flat = new Vector3(toGoal.x, 0f, toGoal.z);

			if (Vector3.Angle(direction, flat) > shotAssistAngle)
				return velocity;

			var time = flat.magnitude / shootSpeed;

			velocity.y = (toGoal.y - 0.5f * Physics.gravity.y * time * time) / time;

			return velocity;
		}

		public void OnSpecial()
			=> DrawAction(transform.forward, attackRange, Color.white);

		private bool CanThrow(out Ball ball)
		{
			if (body.IsServerInitialized && TryGetBall(out ball) && ball.Carrier == body)
				return true;

			ball = null;

			return false;
		}

		// Any hit on an opponent carrier in front knocks the ball loose
		private void Strike(float range)
		{
			if (!body.IsServerInitialized || !TryGetBall(out var ball))
				return;

			var carrier = ball.Carrier;

			if (carrier == null || carrier.Team == body.Team)
				return;

			var offset = carrier.transform.position - transform.position;

			offset.y = 0f;

			if (offset.magnitude > range || Vector3.Angle(transform.forward, offset) > strikeAngle)
				return;

			// The ball spills back towards whoever made the hit, and the carrier reels from it
			if (ball.Strip(-offset))
				carrier.Stagger(stripStagger);
		}

		// Aims at the teammate closest to the aim direction, leading them by their velocity
		private Vector3 PassDirection(Vector3 direction)
		{
			CharacterNetworkController target = null;

			var bestAngle = passAssistAngle;

			foreach (var teammate in body.TeamController.Bodies)
			{
				if (teammate == body)
					continue;

				var offset = teammate.transform.position - transform.position;

				offset.y = 0f;

				var angle = Vector3.Angle(direction, offset);

				if (angle >= bestAngle)
					continue;

				target = teammate;
				bestAngle = angle;
			}

			if (target == null)
				return direction;

			var distance = Vector3.Distance(target.transform.position, transform.position);

			var lead = target.transform.position + target.Velocity * (distance / passSpeed) - transform.position;

			lead.y = 0f;

			return lead.normalized;
		}

		private static bool TryGetBall(out Ball ball)
			=> ServiceLocator.TryGet(out ball);

		private void DrawAction(Vector3 direction, float range, Color color)
		{
			var origin = transform.position + Vector3.up * rayHeight;

			Debug.DrawRay(origin, direction * range, color, 0.5f);
		}
	}
}
