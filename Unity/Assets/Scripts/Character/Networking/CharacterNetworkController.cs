using System;
using Character.Actions.Interfaces;
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
using Match;
using Players;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;


namespace Character.Networking
{
	/// <summary>
	/// Runs a body's input source and sends its input to the server. Whatever drives the body,
	/// a local human, a couch player, an online player or AI, it arrives here as an input source.
	///
	/// Whoever owns a body drives it. A remote human's body is owned by their client, which moves it
	/// and sends its input to the server for actions; every other body is owned by the server, which
	/// is also the host's client. The local player index says which human on the owning machine, if
	/// any, is driving: none means AI.
	/// </summary>
	public class CharacterNetworkController : NetworkBehaviour, ICharacterMovementState
	{
		private const int NoLocalPlayer = -1;

		// Set on each type's prefab, so every machine knows it from the spawn
		[SerializeField]
		private PlayerType playerType;

		private ICharacterInputSource inputSource = NullInputSource.Instance;

		private ICharacterMotor characterMotor;

		private ICharacterMovementState movementState;

		private LocalPlayerRegistry localPlayers;

		private IActionController actionController;


		private readonly NetworkVariable<TeamSide> team = new();

		// Which of the owning machine's local players controls this body
		private readonly NetworkVariable<int> localPlayerIndex = new(NoLocalPlayer);

		// Written by whoever moves the body, for everyone else
		private readonly NetworkVariable<Vector3> networkVelocity = new(writePerm: NetworkVariableWritePermission.Owner);

		private readonly NetworkVariable<bool> networkIsGrounded = new(writePerm: NetworkVariableWritePermission.Owner);

		private readonly NetworkVariable<bool> networkIsDodging = new(writePerm: NetworkVariableWritePermission.Owner);

		private readonly NetworkVariable<CharacterGait> networkGait = new(writePerm: NetworkVariableWritePermission.Owner);

		private readonly NetworkVariable<bool> networkIsChargingShot = new(writePerm: NetworkVariableWritePermission.Owner);

		private readonly NetworkVariable<float> networkShotCharge = new(writePerm: NetworkVariableWritePermission.Owner);

		private readonly NetworkVariable<Vector3> networkShotAim = new(writePerm: NetworkVariableWritePermission.Owner);

		// Server ticks rather than time, so the owning client agrees with the server
		private readonly NetworkVariable<int> staggerEndTick = new();

		private readonly NetworkVariable<bool> knockedOut = new();

		// Timed effects from specials, in ticks like the stagger
		private readonly NetworkVariable<int> hasteEndTick = new();

		private readonly NetworkVariable<int> fortifyEndTick = new();

		public TeamSide Team => team.Value;

		public PlayerType Type => playerType;

		public bool IsHumanControlled => localPlayerIndex.Value != NoLocalPlayer;

		public int LocalPlayerIndex => localPlayerIndex.Value;

		public bool IsStaggered => IsSpawned && ServerTick < staggerEndTick.Value;

		public bool IsHasted => IsSpawned && ServerTick < hasteEndTick.Value;

		public bool IsFortified => IsSpawned && ServerTick < fortifyEndTick.Value;

		// Out for the rest of the match
		public bool IsKnockedOut => knockedOut.Value;

		private bool CanAct => !IsStaggered && !IsKnockedOut && !IsPlayFrozen;

		private bool IsPlayFrozen => ServiceLocator.TryGet(out MatchState matchState) && matchState.IsPlayFrozen;

		private int ServerTick => NetworkManager.ServerTime.Tick;

		// Server only
		public TeamController TeamController { get; private set; }

		public Vector3 Velocity =>
			IsOwner ? movementState.Velocity : networkVelocity.Value;

		public Vector3 LocalVelocity =>
			IsOwner
				? movementState.LocalVelocity
				: transform.InverseTransformDirection(networkVelocity.Value);

		public bool IsGrounded =>
			IsOwner ? movementState.IsGrounded : networkIsGrounded.Value;

		public bool IsDodging =>
			IsOwner ? movementState.IsDodging : networkIsDodging.Value;

		public CharacterGait Gait =>
			IsOwner ? movementState.Gait : networkGait.Value;

		/// <summary>
		/// Whether this body is holding the ball and charging a shot, on every machine.
		/// </summary>
		public bool IsChargingShot =>
			IsOwner ? actionController.IsChargingShot : networkIsChargingShot.Value;

		/// <summary>
		/// How charged the shot is, from 0 to 1, on every machine.
		/// </summary>
		public float ShotCharge =>
			IsOwner ? actionController.ShotCharge : networkShotCharge.Value;

		/// <summary>
		/// The world-space direction a charging shot is aimed, on every machine.
		/// </summary>
		public Vector3 ShotAim =>
			IsOwner ? characterMotor.ShotAim : networkShotAim.Value;

		/// <summary>
		/// Raised on every machine when this body shoots, with the direction and charge. Any shot
		/// counts, charged or not, including the AI's.
		/// </summary>
		public event Action<Vector3, float> ShotFired;

		private void Awake()
		{
			localPlayers = ServiceLocator.Get<LocalPlayerRegistry>();

			actionController = GetComponent<IActionController>();

			characterMotor = GetComponent<ICharacterMotor>();

			movementState = GetComponent<MovementController>();

			if (localPlayers == null)
				Debug.LogError($"{nameof(localPlayers)} is null");

			if (characterMotor == null)
				Debug.LogError($"{nameof(characterMotor)} is null");

			if (movementState == null)
				Debug.LogError($"{nameof(movementState)} is null");
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();

			localPlayerIndex.OnValueChanged += OnLocalPlayerIndexChanged;

			RefreshLocalBinding();
		}

		public override void OnNetworkDespawn()
		{
			base.OnNetworkDespawn();

			localPlayerIndex.OnValueChanged -= OnLocalPlayerIndexChanged;

			localPlayers.Release(this);
		}

		public void SetInputSource(ICharacterInputSource source)
		{
			inputSource = source ?? NullInputSource.Instance;
		}

		// Server only
		public void Initialise(TeamController teamController)
		{
			TeamController = teamController;

			team.Value = teamController.Side;
		}

		// Server only
		public void AssignHuman(ulong clientId, int localIndex)
		{
			SetInputSource(null);

			NetworkObject.ChangeOwnership(clientId);

			localPlayerIndex.Value = localIndex;
		}

		// Server only
		public void AssignAI(PlayerController aiController)
		{
			NetworkObject.RemoveOwnership();

			localPlayerIndex.Value = NoLocalPlayer;

			aiController.Bind(this);
		}

		/// <summary>
		/// Server only. Stops the body acting for a while, whoever is driving it.
		/// </summary>
		public void Stagger(float seconds)
		{
			var endTick = ServerTick + SecondsToTicks(seconds);

			// A lighter hit never cuts a longer stagger short
			if (endTick > staggerEndTick.Value)
				staggerEndTick.Value = endTick;
		}

		/// <summary>
		/// Server only. Faster movement and free sprinting for a while.
		/// </summary>
		public void Haste(float seconds)
		{
			hasteEndTick.Value = ServerTick + SecondsToTicks(seconds);
		}

		/// <summary>
		/// Server only. An unbreakable guard for a while: blocked hits do nothing and stagger the attacker.
		/// </summary>
		public void Fortify(float seconds)
		{
			fortifyEndTick.Value = ServerTick + SecondsToTicks(seconds);
		}

		/// <summary>
		/// Server only. Puts the body down for the rest of the match. It drops the ball, and a
		/// human controlling it moves to a teammate who is still standing.
		/// </summary>
		public void KnockOut()
		{
			if (knockedOut.Value)
				return;

			knockedOut.Value = true;

			TeamController.OnKnockedOut(this);
		}

		// Server only
		public void Revive()
		{
			knockedOut.Value = false;

			staggerEndTick.Value = 0;
			hasteEndTick.Value = 0;
			fortifyEndTick.Value = 0;

			GetComponent<CharacterHealth>().Restore();

			GetComponent<CharacterStamina>().Restore();

			GetComponent<CharacterSpecial>().Empty();
		}

		/// <summary>
		/// Server only. Throws the body, on whichever machine moves it.
		/// </summary>
		public void Knockback(Vector3 velocity)
			=> KnockbackRpc(velocity);

		[Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
		private void KnockbackRpc(Vector3 velocity)
		{
			GetComponent<MovementController>().ApplyKnockback(velocity);
		}

		/// <summary>
		/// Server only. Moves the body instantly, on whichever machine moves it.
		/// </summary>
		public void Teleport(Vector3 position, Quaternion rotation)
			=> TeleportRpc(position, rotation);

		[Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
		private void TeleportRpc(Vector3 position, Quaternion rotation)
		{
			var rigidbody = GetComponent<Rigidbody>();

			rigidbody.linearVelocity = Vector3.zero;
			rigidbody.position = position;
			rigidbody.rotation = rotation;

			// Everyone else snaps rather than watching the body slide across the arena
			GetComponent<NetworkTransform>().Teleport(position, rotation, transform.localScale);
		}

		/// <summary>
		/// Server only. Tells every machine this body just shot.
		/// </summary>
		public void AnnounceShot(Vector3 direction, float charge)
			=> ShotFiredRpc(direction, charge);

		[Rpc(SendTo.ClientsAndHost)]
		private void ShotFiredRpc(Vector3 direction, float charge)
		{
			ShotFired?.Invoke(direction, charge);
		}

		/// <summary>
		/// Asks the server to move this body's controller to a teammate.
		/// </summary>
		public void RequestSwitch()
		{
			if (IsOwner)
				RequestSwitchRpc();
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
		private void RequestSwitchRpc()
		{
			TeamController.SwitchFrom(this);
		}

		public override void OnGainedOwnership()
		{
			base.OnGainedOwnership();

			RefreshLocalBinding();
		}

		public override void OnLostOwnership()
		{
			base.OnLostOwnership();

			RefreshLocalBinding();
		}

		private void OnLocalPlayerIndexChanged(int previous, int next)
			=> RefreshLocalBinding();

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
			if (!IsServer)
				SetInputSource(null);
		}

		private void FixedUpdate()
		{
			if (!IsSpawned || !IsOwner)
				return;

			var characterInput = characterMotor.ResolveShotAim(inputSource.ConsumeCharacterInput());

			ProcessInput(characterInput);

			// The owner moves the body; the server runs its actions too, since it decides hits and the ball
			if (!IsServer)
				SendInputRpc(characterInput);
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
		private void SendInputRpc(CharacterInput characterInput)
		{
			ProcessInput(characterInput);
		}

		private void ProcessInput(CharacterInput characterInput)
		{
			characterMotor.ProcessInput(CanAct ? characterInput : CharacterInput.None);
		}

		private void LateUpdate()
		{
			if (!IsSpawned || !IsOwner)
				return;

			networkVelocity.Value = movementState.Velocity;
			networkIsGrounded.Value = movementState.IsGrounded;
			networkIsDodging.Value = movementState.IsDodging;
			networkGait.Value = movementState.Gait;
			networkIsChargingShot.Value = actionController.IsChargingShot;
			networkShotCharge.Value = actionController.ShotCharge;

			if (actionController.IsChargingShot)
				networkShotAim.Value = characterMotor.ShotAim;
		}

		private int SecondsToTicks(float seconds)
			=> Mathf.CeilToInt(seconds * NetworkManager.NetworkConfig.TickRate);
	}
}
