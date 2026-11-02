using UnityEngine;


namespace Character.Visuals
{
	/// <summary>
	/// A grey-box shockwave: a flat disc that grows to a radius and fades, then removes itself.
	/// </summary>
	public class ExpandingRing : MonoBehaviour
	{
		private const float Duration = 0.35f;

		private const float Thickness = 0.05f;

		private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

		private Material material;

		private Color colour;

		private float radius;

		private float startTime;

		public static void Spawn(Vector3 position, float radius, Color colour)
		{
			var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);

			disc.name = "Shockwave";

			Destroy(disc.GetComponent<Collider>());

			disc.transform.position = position + Vector3.up * 0.1f;

			var ring = disc.AddComponent<ExpandingRing>();

			ring.radius = radius;
			ring.colour = colour;
			ring.startTime = Time.time;

			// Its own transparent material, destroyed with it
			ring.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
			ring.material.SetFloat("_Surface", 1f);
			ring.material.SetFloat("_Blend", 0f);
			ring.material.SetOverrideTag("RenderType", "Transparent");
			ring.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
			ring.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
			ring.material.SetInt("_ZWrite", 0);
			ring.material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
			ring.material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

			disc.GetComponent<Renderer>().sharedMaterial = ring.material;

			ring.Update();
		}

		private void Update()
		{
			var progress = (Time.time - startTime) / Duration;

			if (progress >= 1f)
			{
				Destroy(gameObject);
				return;
			}

			var diameter = radius * 2f * Mathf.Sqrt(progress);

			transform.localScale = new Vector3(diameter, Thickness, diameter);

			var faded = colour;
			faded.a = 1f - progress;

			material.SetColor(BaseColor, faded);
		}

		private void OnDestroy()
		{
			Destroy(material);
		}
	}
}
