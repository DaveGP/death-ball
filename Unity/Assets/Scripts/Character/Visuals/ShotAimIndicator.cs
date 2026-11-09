using Character.Networking;
using UnityEngine;


namespace Character.Visuals
{
	/// <summary>
	/// A line on the floor while a body charges a shot, showing where it's aimed and how much
	/// power has built up. Everyone sees it, so defenders get a warning.
	/// </summary>
	public class ShotAimIndicator : MonoBehaviour
	{
		private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

		[SerializeField]
		private Renderer line;

		[SerializeField]
		private float height = 0.05f;

		[SerializeField]
		private float width = 0.18f;

		[SerializeField]
		private float startOffset = 0.6f;

		[SerializeField]
		private float minLength = 3f;

		[SerializeField]
		private float maxLength = 12f;

		[SerializeField]
		private Color lowPower = new(0.95f, 0.76f, 0.19f);

		[SerializeField]
		private Color fullPower = new(0.9f, 0.15f, 0.1f);

		private CharacterNetworkController body;

		private MaterialPropertyBlock block;

		private void Awake()
		{
			body = GetComponentInParent<CharacterNetworkController>();

			block = new MaterialPropertyBlock();

			line.enabled = false;
		}

		private void LateUpdate()
		{
			var charging = body.IsSpawned && body.IsChargingShot && body.ShotAim.sqrMagnitude > 0.001f;

			line.enabled = charging;

			if (!charging)
				return;

			var charge = body.ShotCharge;

			var length = Mathf.Lerp(minLength, maxLength, charge);

			var bodyPosition = body.transform.position;

			transform.SetPositionAndRotation(
				new Vector3(bodyPosition.x, bodyPosition.y + height, bodyPosition.z) + body.ShotAim * (startOffset + length / 2f),
				Quaternion.LookRotation(Vector3.down, body.ShotAim));

			transform.localScale = new Vector3(width, length, 1f);

			block.SetColor(BaseColor, Color.Lerp(lowPower, fullPower, charge));

			line.SetPropertyBlock(block);
		}
	}
}
