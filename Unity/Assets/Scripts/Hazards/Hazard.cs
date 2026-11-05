using System.Collections.Generic;
using Character.Health;
using Character.Networking;
using Data.Combat;
using DependencyInjection;
using Unity.Netcode;
using Match;
using UnityEngine;


namespace Hazards
{
	/// <summary>
	/// An arena hazard on a repeating cycle: off, then a warning, then live. The cycle comes from
	/// the network's shared clock, so every machine shows the same state without the hazard being
	/// networked at all. Only the server deals the damage.
	///
	/// Hazards hurt both teams, so throwing an opponent into one is a tactic.
	/// </summary>
	public abstract class Hazard : MonoBehaviour
	{

		[SerializeField]
		private float period = 12f;

		[SerializeField]
		private float warning = 2f;

		[SerializeField]
		private float active = 3f;

		// Seconds into the cycle at the start, so hazards don't all go off together
		[SerializeField]
		private float offset;

		[SerializeField]
		private Renderer[] renderers;

		[SerializeField]
		private Color offColour = new(0.3f, 0.3f, 0.35f);

		[SerializeField]
		private Color warningColour = new(1f, 0.85f, 0.2f);

		[SerializeField]
		private Color activeColour = new(1f, 0.4f, 0.1f);

		private readonly Dictionary<CharacterNetworkController, float> lastHitTimes = new();

		private MaterialPropertyBlock propertyBlock;

		private HazardMap hazardMap;

		private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

		public HazardState State { get; private set; }

		/// <summary>
		/// Seconds since this state began.
		/// </summary>
		protected float TimeInState { get; private set; }

		protected float ActiveDuration => active;

		protected float WarningDuration => warning;

		protected static bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

		// The server's clock where there is one, so all machines agree
		private static double Now
		{
			get
			{
				var networkManager = NetworkManager.Singleton;

				return networkManager != null && networkManager.IsListening
					? networkManager.ServerTime.Time
					: Time.timeAsDouble;
			}
		}

		protected virtual void Awake()
		{
			propertyBlock = new MaterialPropertyBlock();
		}

		protected virtual void OnEnable()
		{
			if (ServiceLocator.TryGet(out hazardMap))
				hazardMap.Add(this);
		}

		protected virtual void OnDisable()
		{
			hazardMap?.Remove(this);
		}

		protected virtual void Update()
		{
			UpdateState();

			ShowState();
		}

		protected virtual void FixedUpdate()
		{
			UpdateState();

			if (IsServer && State == HazardState.Active)
				ApplyEffects();
		}

		private void UpdateState()
		{
			var cycle = (float)((Now + offset) % period);

			var warningStart = period - warning - active;
			var activeStart = period - active;

			if (cycle >= activeStart)
			{
				State = HazardState.Active;
				TimeInState = cycle - activeStart;
			}
			else if (cycle >= warningStart)
			{
				State = HazardState.Warning;
				TimeInState = cycle - warningStart;
			}
			else
			{
				State = HazardState.Off;
				TimeInState = cycle;
			}
		}

		protected virtual void ShowState()
		{
			var colour = State switch
			{
				// Flashes so it can't be missed
				HazardState.Warning => Mathf.Repeat(Time.time * 6f, 1f) < 0.5f ? warningColour : offColour,
				HazardState.Active => activeColour,
				_ => offColour
			};

			propertyBlock.SetColor(BaseColor, colour);

			foreach (var renderer in renderers)
				renderer.SetPropertyBlock(propertyBlock);
		}

		/// <summary>
		/// Server only, every physics tick while live.
		/// </summary>
		protected abstract void ApplyEffects();

		/// <summary>
		/// Whether standing here is about to hurt, so the AI moves away.
		/// </summary>
		public abstract bool IsThreatening(Vector3 position);

		/// <summary>
		/// Which way to go to get clear, on the XZ plane.
		/// </summary>
		public abstract Vector2 EscapeDirection(Vector3 position);

		/// <summary>
		/// Server only. Hurts and throws a body, knocking the ball loose if they had it.
		/// </summary>
		protected void HitBody(CharacterNetworkController body, Hit hit, Vector3 knockback)
		{
			if (body.IsKnockedOut)
				return;

			// Once per activation, however long they stay in it
			if (lastHitTimes.TryGetValue(body, out var lastHit) && Time.time < lastHit + active)
				return;

			lastHitTimes[body] = Time.time;

			if (body.GetComponent<CharacterHealth>().TakeHit(hit) != HitResult.Landed)
				return;

			body.Knockback(knockback);

			if (ServiceLocator.TryGet(out Ball ball) && ball.Carrier == body)
				ball.Strip(new Vector3(knockback.x, 0f, knockback.z));
		}
	}
}
