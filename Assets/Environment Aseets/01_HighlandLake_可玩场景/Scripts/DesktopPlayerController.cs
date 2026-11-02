using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class DesktopPlayerController : MonoBehaviour
{
    [SerializeField] private Transform view;
    [SerializeField] private float walkSpeed = 4.5f;
    [SerializeField] private float sprintSpeed = 7.5f;
    [SerializeField] private float lookSensitivity = 0.12f;
    [SerializeField] private float middleDragDistance = 0.012f;
    [SerializeField] private float wheelStepDistance = 0.65f;
    [SerializeField] private float gravity = -20f;

    private CharacterController controller;
    private float pitch;
    private float verticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (view == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>(true);
            view = childCamera != null ? childCamera.transform : transform;
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard == null || mouse == null || view == null)
        {
            return;
        }

        Vector2 lookDelta = mouse.delta.ReadValue();
        if (mouse.rightButton.isPressed)
        {
            transform.Rotate(Vector3.up, lookDelta.x * lookSensitivity, Space.World);
            pitch = Mathf.Clamp(pitch - lookDelta.y * lookSensitivity, -80f, 80f);
            view.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        Vector2 moveInput = Vector2.zero;
        if (keyboard.wKey.isPressed) moveInput.y += 1f;
        if (keyboard.sKey.isPressed) moveInput.y -= 1f;
        if (keyboard.dKey.isPressed) moveInput.x += 1f;
        if (keyboard.aKey.isPressed) moveInput.x -= 1f;
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }

        float speed = keyboard.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;
        Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(view.right, Vector3.up).normalized;
        Vector3 planarMotion = (forward * moveInput.y + right * moveInput.x) * speed;

        float wheel = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(wheel) > 0.01f)
        {
            planarMotion += forward * Mathf.Sign(wheel) * wheelStepDistance;
        }

        if (mouse.middleButton.isPressed)
        {
            planarMotion += right * (lookDelta.x * middleDragDistance);
            planarMotion += forward * (lookDelta.y * middleDragDistance);
        }

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }
        verticalVelocity += gravity * Time.deltaTime;
        planarMotion.y = verticalVelocity;
        controller.Move(planarMotion * Time.deltaTime);
    }
}
