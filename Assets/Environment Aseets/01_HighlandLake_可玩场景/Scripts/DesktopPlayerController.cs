using UnityEngine;
using UnityEngine.InputSystem;
using ADayAtTheZoo.Monkeys.Care;

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
    [SerializeField] private float gamepadTurnSpeed = 120f;
    [SerializeField] private float gamepadLookSpeed = 80f;

    [System.NonSerialized] public bool inputLocked;

    private CharacterController controller;
    private float pitch;
    private float verticalVelocity;
    private bool xboxWasConnected;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (view == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>(true);
            view = childCamera != null ? childCamera.transform : transform;
        }
        EnablePresentation();
    }

    private void OnEnable()
    {
        EnablePresentation();
    }

    private void EnablePresentation()
    {
        if (view == null) return;
        Camera desktopCamera = view.GetComponent<Camera>();
        if (desktopCamera == null) desktopCamera = view.GetComponentInChildren<Camera>(true);
        if (desktopCamera != null) desktopCamera.enabled = true;

        AudioListener listener = view.GetComponent<AudioListener>();
        if (listener == null) listener = view.GetComponentInChildren<AudioListener>(true);
        if (listener != null) listener.enabled = true;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        Gamepad pad = Gamepad.current;
        CareXboxSnapshot xbox = CareXboxInput.Read();
        if (xbox.Connected != xboxWasConnected)
        {
            xboxWasConnected = xbox.Connected;
            Debug.Log(xbox.Connected ? "[Zoo Controls] Xbox 输入已连接" : "[Zoo Controls] Xbox 输入已断开");
        }
        if (view == null)
        {
            return;
        }

        Vector2 moveInput = Larger(pad != null ? pad.leftStick.ReadValue() : Vector2.zero, xbox.LeftStick);
        Vector2 rightStick = Larger(pad != null ? pad.rightStick.ReadValue() : Vector2.zero, xbox.RightStick);
        Vector2 lookDelta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
        if (!inputLocked && Mathf.Abs(rightStick.x) > .15f)
        {
            transform.Rotate(Vector3.up, rightStick.x * gamepadTurnSpeed * Time.deltaTime, Space.World);
        }
        if (!inputLocked && Mathf.Abs(rightStick.y) > .15f)
        {
            pitch = Mathf.Clamp(pitch - rightStick.y * gamepadLookSpeed * Time.deltaTime, -80f, 80f);
            view.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        if (!inputLocked && mouse != null && mouse.rightButton.isPressed)
        {
            transform.Rotate(Vector3.up, lookDelta.x * lookSensitivity, Space.World);
            pitch = Mathf.Clamp(pitch - lookDelta.y * lookSensitivity, -80f, 80f);
            view.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        if (!inputLocked && keyboard != null)
        {
            if (keyboard.wKey.isPressed) moveInput.y += 1f;
            if (keyboard.sKey.isPressed) moveInput.y -= 1f;
            if (keyboard.dKey.isPressed) moveInput.x += 1f;
            if (keyboard.aKey.isPressed) moveInput.x -= 1f;
        }
        if (inputLocked) moveInput = Vector2.zero;
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }

        float speed = keyboard != null && keyboard.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;
        Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(view.right, Vector3.up).normalized;
        Vector3 planarMotion = (forward * moveInput.y + right * moveInput.x) * speed;

        float wheel = !inputLocked && mouse != null ? mouse.scroll.ReadValue().y : 0;
        if (Mathf.Abs(wheel) > 0.01f)
        {
            planarMotion += forward * Mathf.Sign(wheel) * wheelStepDistance;
        }

        if (!inputLocked && mouse != null && mouse.middleButton.isPressed)
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

    static Vector2 Larger(Vector2 a, Vector2 b) =>
        b.sqrMagnitude > a.sqrMagnitude ? b : a;
}
