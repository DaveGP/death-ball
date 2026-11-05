using Character.Health;
using Character.Movement;
using Character.Movement.Interfaces;
using Character.Networking;
using Character.Specials;
using Character.Stamina;
using Data.Combat;
using Data.ScriptableObjects;
using DependencyInjection;
using Match;
using UnityEngine;


namespace Character.Actions
{
	using Interfaces;


	/// <summary>
	/// Ball handling and melee. Runs on the owning client and the server, but only the
	/// server touches the ball or deals damage; the owner just lunges and draws debug rays.
	/// </summary>
	public class ActionController : MonoBehaviour, IActionController
	{
		// Potentially move these out later
		[SerializeField]
		private Attack lightAttack = new(new Hit(10, 0.25f), 1.5f, 0.4f);

		// Slow, but goes through a block
		[SerializeField]
		private Attack heavyAttack = new(new Hit(25, 0.7f, true), 1.7f, 1.2f, 7f);

		// A lunge that knocks the target off their feet. The carrier can still do this, as a barge.
		[SerializeField]
		private Attack tackle = new(new Hit(3, 0.8f), 2.2f, 1.5f, 5f);

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

		private IMovementController movementController;

		private CharacterStamina stamina;

		private CharacterSpecial special;

		private CharacterStats stats;

		private readonly Collider[] overlaps = new Collider[16];

		private int characterMask;

		private float nextLightAttackTime;

		private float nextHeavyAttackTime;

		private float nextTackleTime;

		public bool IsBlocking { get; private set; }

		private float PassSpeed => passSpeed * stats.ThrowSpeedMultiplier;

		private float ShootSpeed => shootSpeed * stats.ThrowSpeedMultiplier;

		public bool IsCarrying => TryGetBall(out var ball) && ball.Carrier == body;

		private void Awake()
		{
			body = GetComponent<CharacterNetworkController>();

			movementController = GetComponent<IMovementController>();

			stamina = GetComponent<CharacterStamina>();

			special = GetComponent<CharacterSpecial>();

			stats = GetComponent<MovementController>().Stats;

			characterMask = LayerMask.GetMask("Character");

			if (body == null)
				Debug.LogError($"{nameof(body)} is null");

			if (movementController == null)
				Debug.LogError($"{nameof(movementController)} is null");
		}

		public void SetBlocking(bool blocking)
		{
			IsBlocking = blocking;
		}

		// The carrier's hands are full, they can only pass, shoot or barge
		public void OnAttack()
		{
			if (IsCarrying || !TryUse(lightAttack, ref nextLightAttackTime, 0f))
				return;

			DrawAction(transform.forward, lightAttack.Range, Color.red);

			Strike(lightAttack);
		}

		public void OnHeavyAttack()
		{
			if (IsCarrying || !TryUse(heavyAttack, ref nextHeavyAttackTime, stats.HeavyAttackStaminaCost))
				return;

			DrawAction(transform.forward, heavyAttack.Range, Color.magenta);

			Strike(heavyAttack);
		}

		public void OnTackle()
		{
			if (!TryUse(tackle, ref nextTackleTime, stats.TackleStaminaCost))
				return;

			DrawAction(transform.forward, tackle.Range, Color.yellow);

			// Runs on whichever machine moves this body, so the lunge happens there
			movementController.Dodge(new Vector2(transform.forward.x, transform.forward.z));

			Strike(tackle);
		}

		// Both the owner and the server check the cooldown and stamina; only the server spends it
		private bool TryUse(Attack attack, ref float nextTime, float staminaCost)
		{
			if (Time.time < nextTime || !stamina.Has(staminaCost))
				return false;

			nextTime = Time.time + attack.Cooldown;

			if (body.IsServer && staminaCost > 0f)
				stamina.Drain(staminaCost);

			return true;
		}

		public void OnPass(Vector3 direction)
		{
			if (!CanThrow(out var ball))
				return;

			var aim = PassDirection(direction);

			DrawAction(aim, 10f, Color.cyan);

			ball.Throw(body, aim * PassSpeed + Vector3.up * passLift);
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
			var velocity = direction * ShootSpeed + Vector3.up * shootLift;

			var toGoal = body.TeamController.AttackTarget.position - ball.HoldPosition(body);

			var flat = new Vector3(toGoal.x, 0f, toGoal.z);

			if (Vector3.Angle(direction, flat) > shotAssistAngle)
				return velocity;

			var time = flat.magnitude / ShootSpeed;

			velocity.y = (toGoal.y - 0.5f * Physics.gravity.y * time * time) / time;

			return velocity;
		}

		public void OnSpecial()
		{
			if (body.IsServer)
				special.TryUse();
		}

		private bool CanThrow(out Ball ball)
		{
			if (body.IsServer && TryGetBall(out ball) && ball.Carrier == body)
				return true;

			ball = null;

			return false;
		}

		// Hits the nearest opponent in front. A hit that lands on the carrier knocks the ball loose.
		private void Strike(Attack attack)
		{
			if (!body.IsServer)
				return;

			var target = FindTarget(attack.Range);

			if (target == null)
				return;

			var hit = attack.Hit;
			hit.Damage = Mathf.RoundToInt(hit.Damage * stats.MeleeDamageMultiplier);

			var result = target.GetComponent<CharacterHealth>().TakeHit(hit, body);

			if (result != HitResult.Landed)
				return;

			if (attack.Knockback > 0f)
			{
				var away = target.transform.position - transform.position;
				away.y = 0f;

				target.Knockback(away.normalized * attack.Knockback);
			}

			if (!TryGetBall(out var ball) || ball.Carrier != target)
				return;

			// The ball spills back towards whoever made the hit, and the carrier reels from it
			if (ball.Strip(transform.position - target.transform.position))
				target.Stagger(stripStagger);
		}

		private CharacterNetworkController FindTarget(float range)
		{
			var count = Physics.OverlapSphereNonAlloc(
				transform.position + Vector3.up,
				range,
				overlaps,
				characterMask,
				QueryTriggerInteraction.Ignore);

			CharacterNetworkController nearest = null;

			var nearestDistance = float.MaxValue;

			for (var i = 0; i < count; i++)
			{
				var target = overlaps[i].GetComponentInParent<CharacterNetworkController>();

				if (target == null || target.Team == body.Team || target.IsKnockedOut)
					continue;

				var offset = target.transform.position - transform.position;

				offset.y = 0f;

				if (offset.magnitude > range || Vector3.Angle(transform.forward, offset) > strikeAngle)
					continue;

				if (offset.magnitude >= nearestDistance)
					continue;

				nearest = target;
				nearestDistance = offset.magnitude;
			}

			return nearest;
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

			var lead = target.transform.position + target.Velocity * (distance / PassSpeed) - transform.position;

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
