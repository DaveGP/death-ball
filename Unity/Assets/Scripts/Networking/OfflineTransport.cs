using System;
using Unity.Netcode;


namespace Networking
{
	/// <summary>
	/// A transport that never touches the network, for solo and couch play. A host's own client
	/// talks to its server inside NGO without going through a transport, so all this has to do
	/// is let the server start. Browsers can't open a listening socket, so this is how local play
	/// works there.
	/// </summary>
	public class OfflineTransport : NetworkTransport
	{
		public override ulong ServerClientId => 0;

		public override void Initialize(NetworkManager networkManager = null)
		{
		}

		public override bool StartServer() => true;

		// Nothing to connect to
		public override bool StartClient() => false;

		public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
		{
		}

		public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
		{
			clientId = 0;
			payload = default;
			receiveTime = 0f;

			return NetworkEvent.Nothing;
		}

		public override void DisconnectRemoteClient(ulong clientId)
		{
		}

		public override void DisconnectLocalClient()
		{
		}

		public override ulong GetCurrentRtt(ulong clientId) => 0;

		public override void Shutdown()
		{
		}
	}
}
