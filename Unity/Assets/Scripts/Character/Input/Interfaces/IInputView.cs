using UnityEngine;


namespace Character.Input.Interfaces
{
	public interface IInputView
	{
		/// <summary>
		/// Gets the yaw that stick input is relative to, usually the camera's.
		/// </summary>
		Quaternion ViewRotation { get; }
	}
}
