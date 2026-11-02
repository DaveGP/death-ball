using System;
using Character.Health;
using Character.Networking;
using Character.Specials;
using Character.Stamina;
using UnityEngine;


namespace Character.Visuals
{
	/// <summary>
	/// Grey-box bars over a body's head, turned to face the camera: health, with stamina and
	/// the special meter under it. The special bar flashes when the special is ready.
	/// </summary>
	public class StatusBars : MonoBehaviour
	{
		[Serializable]
		private struct Bar
		{
			public Transform Fill;

			public Renderer Renderer;
		}

		[SerializeField]
		private Bar health;

		[SerializeField]
		private Bar stamina;

		[SerializeField]
		private Bar special;

		[SerializeField]
		private Color healthFullColour = new(0.2f, 0.9f, 0.3f);

		[SerializeField]
		private Color healthEmptyColour = new(0.9f, 0.15f, 0.1f);

		[SerializeField]
		private Color staminaColour = new(0.95f, 0.8f, 0.2f);

		[SerializeField]
		private Color specialColour = new(0.3f, 0.6f, 1f);

		[SerializeField]
		private Color specialReadyColour = Color.white;

		private CharacterNetworkController body;

		private CharacterHealth characterHealth;

		private CharacterStamina characterStamina;

		private CharacterSpecial characterSpecial;

		private MaterialPropertyBlock propertyBlock;

		private float barWidth;

		private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

		private void Awake()
		{
			body = GetComponentInParent<CharacterNetworkController>();

			characterHealth = GetComponentInParent<CharacterHealth>();

			characterStamina = GetComponentInParent<CharacterStamina>();

			characterSpecial = GetComponentInParent<CharacterSpecial>();

			propertyBlock = new MaterialPropertyBlock();

			barWidth = health.Fill.localScale.x;
		}

		private void LateUpdate()
		{
			var visible = body.IsSpawned && !body.IsKnockedOut;

			foreach (Transform child in transform)
				child.gameObject.SetActive(visible);

			if (!visible)
				return;

			var camera = Camera.main;

			if (camera != null)
				transform.rotation = camera.transform.rotation;

			var healthFraction = characterHealth.Fraction;

			SetBar(health, healthFraction, Color.Lerp(healthEmptyColour, healthFullColour, healthFraction));

			SetBar(stamina, characterStamina.Fraction, staminaColour);

			var specialReady = characterSpecial.IsReady && Mathf.Repeat(Time.time * 4f, 1f) < 0.5f;

			SetBar(special, characterSpecial.Fraction, specialReady ? specialReadyColour : specialColour);
		}

		// Shrinks towards the left edge
		private void SetBar(Bar bar, float fraction, Color colour)
		{
			var scale = bar.Fill.localScale;
			scale.x = barWidth * fraction;
			bar.Fill.localScale = scale;

			var position = bar.Fill.localPosition;
			position.x = -(barWidth - scale.x) / 2f;
			bar.Fill.localPosition = position;

			propertyBlock.SetColor(BaseColor, colour);
			bar.Renderer.SetPropertyBlock(propertyBlock);
		}
	}
}
