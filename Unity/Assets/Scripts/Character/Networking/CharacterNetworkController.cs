using Character.Health;
using Character.Specials;
using Character.Stamina;
using Character.Input;
using Character.Input.Interfaces;
using Character.Movement;
using Character.Movement.Interfaces;
using Data.Enums;
using Data.Input;
using DependencyInjection;
using FishNet.Component.Transforming;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Match;
using Players;
using UnityEngine;


namespace Character.Networking
{
	/// <summary>
	/// Runs a body's input source and sends its input to the server. Whatever drives the body,
	/// a local human, a couch player, an online player or AI, it arrives here as an input source.
	///
	/// Bodies driven by a human are owned by that human's connection (the host's too), and their
	/// input source is set on the owning client from its local players. AI bodies have no owner,
	/// and the server sets their input source.
	/// </summary>
	public class CharacterNetworkController : NetworkBehaviour, ICharacterMovementState
	{
		private const int NoLocalPlayer = -1;

		private ICharacterInputSource inputSource = NullInputSource.Instance;

		private ICharacterMotor characterMotor;

		private ICharacterMovementState movementState;

		private LocalPlayerRegistry localPlayers;


		private readonly SyncVar<TeamSide> team = new();

		// Which of the owning client's local players controls this body
		private readonly SyncVar<int> localPlayerIndex = new(NoLocalPlayer);

		private readonly SyncVar<Vector3> networkVelocity = new();

		private readonly SyncVar<bool> networkIsGrounded = new();

		private readonly SyncVar<bool> networkIsDodging = new();

		private readonly SyncVar<CharacterGait> networkGait = new();

		// Ticks rather than time so the owning client agrees with the server
		private readonly SyncVar<uint> staggerEndTick = new();

		private readonly SyncVar<bool> knockedOut = new();

		public TeamSide Team => team.Value;

		public bool IsHumanControlled => localPlayerIndex.Value != NoLocalPlayer;

		public int LocalPlayerIndex => localPlayerIndex.Value;

		public bool IsStaggered => IsSpawned && TimeManager.Tick < staggerEndTick.Value;

		// Out for the rest of the match
		public bool IsKnockedOut => knockedOut.Value;

		private bool CanAct => !IsStaggered && !IsKnockedOut && !IsPlayFrozen;

		private bool IsPlayFrozen => ServiceLocator.TryGet(out MatchState matchState) && matchState.IsPlayFrozen;

		// Server only
		public TeamController TeamController { get; private set; }

		public Vector3 Velocity =>
			HasInputAuthority ? movementState.Velocity : networkVelocity.Value;

		public Vector3 LocalVelocity =>
			HasInputAuthority
				? movementState.LocalVelocity
				: transform.InverseTransformDirection(networkVelocity.Value);

		public bool IsGrounded =>
			HasInputAuthority ? movementState.IsGrounded : networkIsGrounded.Value;

		public bool IsDodging =>
			HasInputAuthority ? movementState.IsDodging : networkIsDodging.Value;

		public CharacterGait Gait =>
			HasInputAuthority ? movementState.Gait : networkGait.Value;

		// The owner drives a human's body, the server drives a body nobody owns
		private bool HasInputAuthority => Owner.IsValid ? IsOwner : IsServerInitialized;

		private void Awake()
		{
			localPlayers = ServiceLocator.Get<LocalPlayerRegistry>();

			characterMotor = GetComponent<ICharacterMotor>();

			movementState = GetComponent<MovementController>();

			if (localPlayers == null)
				Debug.LogError($"{nameof(localPlayers)} is null");

			if (characterMotor == null)
				Debug.LogError($"{nameof(characterMotor)} is null");

			if (movementState == null)
				Debug.LogError($"{nameof(movementState)} is null");

			localPlayerIndex.OnChange += OnLocalPlayerIndexChanged;
		}

		public void SetInputSource(ICharacterInputSource source)
		{
			inputSource = source ?? NullInputSource.Instance;
		}

		[Server]
		public void Initialise(TeamController teamController)
		{
			TeamController = teamController;

			team.Value = teamController.Side;
		}

		[Server]
		public void AssignHuman(NetworkConnection connection, int localIndex)
		{
			SetInputSource(null);

			localPlayerIndex.Value = localIndex;

			GiveOwnership(connection);
		}

		[Server]
		public void AssignAI(PlayerController aiController)
		{
			RemoveOwnership();

			localPlayerIndex.Value = NoLocalPlayer;

			aiController.Bind(this);
		}

		/// <summary>
		/// Stops the body acting for a while, whoever is driving it.
		/// </summary>
		[Server]
		public void Stagger(float seconds)
		{
			var endTick = TimeManager.Tick + TimeManager.TimeToTicks(seconds);

			// A lighter hit never cuts a longer stagger short
			if (endTick > staggerEndTick.Value)
				staggerEndTick.Value = endTick;
		}

		/// <summary>
		/// Puts the body down for the rest of the match. It drops the ball, and a human
		/// controlling it moves to a teammate who is still standing.
		/// </summary>
		[Server]
		public void KnockOut()
		{
			if (knockedOut.Value)
				return;

			knockedOut.Value = true;

			TeamController.OnKnockedOut(this);
		}

		[Server]
		public void Revive()
		{
			knockedOut.Value = false;

			staggerEndTick.Value = 0;

			GetComponent<CharacterHealth>().Restore();

			GetComponent<CharacterStamina>().Restore();

			GetComponent<CharacterSpecial>().Empty();
		}

		/// <summary>
		/// Moves the body instantly. A human's body is moved on their own machine,
		/// because the owner has authority over its transform.
		/// </summary>
		[Server]
		public void Teleport(Vector3 position, Quaternion rotation)
		{
			if (!Owner.IsValid || Owner.IsLocalClient)
				ApplyTeleport(position, rotation);
			else
				OwnerTeleport(Owner, position, rotation);
		}

		[TargetRpc]
		private void OwnerTeleport(NetworkConnection connection, Vector3 position, Quaternion rotation)
		{
			ApplyTeleport(position, rotation);
		}

		private void ApplyTeleport(Vector3 position, Quaternion rotation)
		{
			var rigidbody = GetComponent<Rigidbody>();

			rigidbody.linearVelocity = Vector3.zero;
			rigidbody.position = position;
			rigidbody.rotation = rotation;

			transform.SetPositionAndRotation(position, rotation);

			// Everyone else snaps rather than watching the body slide across the arena
			GetComponent<NetworkTransform>().Teleport();
		}

		/// <summary>
		/// Asks the server to move this body's controller to a teammate.
		/// </summary>
		public void RequestSwitch()
		{
			if (IsOwner)
				ServerRequestSwitch();
		}

		[ServerRpc]
		private void ServerRequestSwitch()
		{
			TeamController.SwitchFrom(this);
		}

		public override void OnOwnershipClient(NetworkConnection prevOwner)
		{
			base.OnOwnershipClient(prevOwner);

			RefreshLocalBinding();
		}

		private void OnLocalPlayerIndexChanged(int previous, int next, bool asServer)
		{
			if (!asServer)
				RefreshLocalBinding();
		}

		// Ownership and the local player index can arrive in either order, so both call this
		private void RefreshLocalBinding()
		{
			if (IsOwner && localPlayerIndex.Value != NoLocalPlayer)
			{
				localPlayers.Get(localPlayerIndex.Value)?.Bind(this);
				return;
			}

			localPlayers.Release(this);

			// The server keeps the AI source it set
			if (!IsServerInitialized)
				SetInputSource(null);
		}

		private void FixedUpdate()
		{
			if (!IsSpawned || !HasInputAuthority)
				return;

			var characterInput = inputSource.ConsumeCharacterInput();

			if (IsServerInitialized)
			{
				ProcessInput(characterInput);
			}
			else
			{
				SendInputToServer(characterInput);
			}
		}

		[ServerRpc(RunLocally = true)]
		private void SendInputToServer(CharacterInput characterInput)
		{
			ProcessInput(characterInput);
		}

		private void ProcessInput(CharacterInput characterInput)
		{
			characterMotor.ProcessInput(CanAct ? characterInput : CharacterInput.None);
		}

		private void LateUpdate()
		{
			if (!IsServerInitialized)
				return;

			UpdateNetworkMovementState();
		}

		private void UpdateNetworkMovementState()
		{
			networkVelocity.Value = movementState.Velocity;
			networkIsGrounded.Value = movementState.IsGrounded;
			networkIsDodging.Value = movementState.IsDodging;
			networkGait.Value = movementState.Gait;
		}
	}
}
