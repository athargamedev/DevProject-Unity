using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Network_Game.ThirdPersonController.InputSystem
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool sprint;
		public bool attack;
		public bool emote;
		public bool interact;

		[Header("Movement Settings")]
		public bool analogMovement;
		public bool inputBlocked;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;

#if ENABLE_INPUT_SYSTEM
		public void OnMove(InputValue value)
		{
			if (inputBlocked) return;
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if (inputBlocked) return;
			if(cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		public void OnJump(InputValue value)
		{
			if (inputBlocked) return;
			JumpInput(value.isPressed);
		}

		public void OnSprint(InputValue value)
		{
			if (inputBlocked) return;
			SprintInput(value.isPressed);
		}

		public void OnAttack(InputValue value)
		{
			if (inputBlocked) return;
			AttackInput(value.isPressed);
		}

		public void OnEmote(InputValue value)
		{
			if (inputBlocked) return;
			EmoteInput(value.isPressed);
		}

		public void OnInteract(InputValue value)
		{
			if (inputBlocked) return;
			InteractInput(value.isPressed);
		}
#endif


		public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
		} 

		public void LookInput(Vector2 newLookDirection)
		{
			look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			sprint = newSprintState;
		}

		public void AttackInput(bool newState) { attack = newState; }
		public void EmoteInput(bool newState)  { emote  = newState; }
		public void InteractInput(bool newState) { interact = newState; }

		// Legacy callback methods for PlayerInput SendMessages mode
		// The PlayerInput prefab is wired to call these method names (InputMove, etc.)
		// but the Input System interface uses OnMove, OnJump, etc.
		// These methods bridge the gap for SendMessages notification behavior.
		// NOTE: Also works with Unity Events mode since we added no-param versions.
		public void InputMove(Vector2 value) { 
			if (inputBlocked) return;
			MoveInput(value); 
		}
		
		// No-param version for SendMessages mode (Unity sometimes strips params)
		public void InputMove() { }
		
		public void InputLook(Vector2 value) { 
			if (inputBlocked) return;
			LookInput(value); 
		}
		
		public void InputLook() { }
		
		public void InputJump() { 
			if (inputBlocked) return;
			JumpInput(true); 
		}
		
		public void InputSprint() { 
			if (inputBlocked) return;
			SprintInput(true); 
		}

		private void OnApplicationFocus(bool hasFocus)
		{
			SetCursorState(cursorLocked);
		}

		public void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
		}

		/// <summary>
		/// Block input - call this when entering dialogue/UI
		/// </summary>
		public void BlockInput() { inputBlocked = true; }

		/// <summary>
		/// Unblock input - call this when exiting dialogue/UI
		/// </summary>
		public void UnblockInput() { inputBlocked = false; }
	}
	
}