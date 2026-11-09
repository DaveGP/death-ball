using System.Collections.Generic;
using Lobby;
using UnityEngine;

namespace Menus
{
	public sealed class LobbyPlayerList : MonoBehaviour
	{
		[SerializeField] private LobbyPlayerRow rowTemplate;

		private readonly List<LobbyPlayerRow> rows = new();

		public void Show(IEnumerable<LobbyEntry> entries, ulong localClientId)
		{
			var index = 0;
			foreach (var entry in entries)
			{
				if (index == rows.Count)
					rows.Add(Instantiate(rowTemplate, rowTemplate.transform.parent));
				rows[index++].Show(entry, localClientId);
			}

			for (; index < rows.Count; index++)
				rows[index].Show(null, localClientId);
		}
	}
}
