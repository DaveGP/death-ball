using Lobby;
using UnityEngine;
using UnityEngine.UI;

namespace Menus
{
	public sealed class LobbyPlayerRow : MonoBehaviour
	{
		[SerializeField] private Image background;
		[SerializeField] private Text playerName;
		[SerializeField] private Text role;
		[SerializeField] private Image roleIcon;
		[SerializeField] private Sprite normal;
		[SerializeField] private Sprite selected;
		[SerializeField] private Sprite[] roleIcons;

		public void Show(LobbyEntry? entry, ulong localClientId)
		{
			if (!entry.HasValue)
			{
				gameObject.SetActive(false);
				return;
			}

			var player = entry.Value;
			var isLocal = player.ClientId == localClientId;
			background.sprite = isLocal ? selected : normal;
			playerName.text = isLocal ? $"> {player.Name}" : player.Name.ToString();
			role.text = player.Type.ToString().ToUpperInvariant();
			var roleIndex = (int)player.Type;
			roleIcon.sprite = roleIndex >= 0 && roleIndex < roleIcons.Length ? roleIcons[roleIndex] : null;
			roleIcon.enabled = roleIcon.sprite != null;
			gameObject.SetActive(true);
		}
	}
}
