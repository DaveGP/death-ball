using Character.Health;
using Character.Networking;
using UnityEngine;


namespace Character.Visuals
{
	/// <summary>
	/// A grey-box health bar over a body's head, turned to face the camera.
	/// </summary>
	public class HealthBar : MonoBehaviour
	{
		[SerializeField]
		private Transform fill;

		[SerializeField]
		private Renderer fillRenderer;

		[SerializeField]
		private Color fullColour = new(0.2f, 0.9f, 0.3f);

		[SerializeField]
		private Color emptyColour = new(0.9f, 0.15f, 0.1f);

		private CharacterHealth health;

		private CharacterNetworkController body;

		private MaterialPropertyBlock propertyBlock;

		private float fillWidth;

		private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

		private void Awake()
		{
			health = GetComponentInParent<CharacterHealth>();

			body = GetComponentInParent<CharacterNetworkController>();

			propertyBlock = new MaterialPropertyBlock();

			fillWidth = fill.localScale.x;
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

			var fraction = health.Fraction;

			// Shrinks towards the left edge
			var scale = fill.localScale;
			scale.x = fillWidth * fraction;
			fill.localScale = scale;

			var position = fill.localPosition;
			position.x = -(fillWidth - scale.x) / 2f;
			fill.localPosition = position;

			propertyBlock.SetColor(BaseColor, Color.Lerp(emptyColour, fullColour, fraction));
			fillRenderer.SetPropertyBlock(propertyBlock);
		}
	}
}
