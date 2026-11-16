using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using Gamepad = UnityEngine.InputSystem.Gamepad;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

namespace ADayAtTheZoo.Monkeys.Care
{
    public readonly struct CareInputSnapshot
    {
        public bool AWasPressed { get; }
        public bool BWasPressed { get; }
        public bool TriggerHeld { get; }
        public bool TriggerPressed { get; }
        public bool TriggerReleased { get; }
        public bool GripHeld { get; }
        public Vector2 LeftStick { get; }
        public Vector2 RightStick { get; }

        public CareInputSnapshot(bool a, bool b, bool triggerHeld, bool triggerPressed,
            bool triggerReleased, bool gripHeld, Vector2 leftStick, Vector2 rightStick)
        {
            AWasPressed = a;
            BWasPressed = b;
            TriggerHeld = triggerHeld;
            TriggerPressed = triggerPressed;
            TriggerReleased = triggerReleased;
            GripHeld = gripHeld;
            LeftStick = leftStick;
            RightStick = rightStick;
        }
    }

    public static class CareInput
    {
        static bool previousA, previousB, previousTrigger;

        public static CareInputSnapshot Read(bool allowDesktopFallback = true)
        {
            Gamepad pad = Gamepad.current;
            CareXboxSnapshot xbox = CareXboxInput.Read();
            XRInputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            XRInputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            bool rightXr = right.isValid;
            bool leftXr = left.isValid;
            bool rightOvr = OVRInput.IsControllerConnected(OVRInput.Controller.RTouch);
            bool leftOvr = OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);

            bool a = (pad != null && pad.buttonSouth.isPressed) || xbox.A;
            bool b = (pad != null && pad.buttonEast.isPressed) || xbox.B;
            float triggerValue = Mathf.Max(pad != null ? pad.rightTrigger.ReadValue() : 0, xbox.RightTrigger);
            float gripValue = Mathf.Max(pad != null ? pad.leftTrigger.ReadValue() : 0, xbox.LeftTrigger);

            if (rightOvr)
            {
                a |= OVRInput.Get(OVRInput.RawButton.A);
                b |= OVRInput.Get(OVRInput.RawButton.B);
                triggerValue = Mathf.Max(triggerValue, OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch));
                gripValue = Mathf.Max(gripValue, OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch));
            }
            if (rightXr)
            {
                if (right.TryGetFeatureValue(XRCommonUsages.primaryButton, out bool primary)) a |= primary;
                if (right.TryGetFeatureValue(XRCommonUsages.secondaryButton, out bool secondary)) b |= secondary;
                if (right.TryGetFeatureValue(XRCommonUsages.trigger, out float xrTrigger)) triggerValue = Mathf.Max(triggerValue, xrTrigger);
                if (right.TryGetFeatureValue(XRCommonUsages.grip, out float xrGrip)) gripValue = Mathf.Max(gripValue, xrGrip);
            }

            Vector2 leftStick = pad != null ? pad.leftStick.ReadValue() : Vector2.zero;
            Vector2 rightStick = pad != null ? pad.rightStick.ReadValue() : Vector2.zero;
            leftStick = Expand(leftStick, xbox.LeftStick);
            rightStick = Expand(rightStick, xbox.RightStick);
            if (leftOvr) leftStick = Expand(leftStick, OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch));
            if (rightOvr) rightStick = Expand(rightStick, OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch));
            if (leftXr && left.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out Vector2 xrLeft)) leftStick = Expand(leftStick, xrLeft);
            if (rightXr && right.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out Vector2 xrRight)) rightStick = Expand(rightStick, xrRight);

            bool noXrOrPad = pad == null && !xbox.Connected && !rightXr && !leftXr && !rightOvr && !leftOvr;
            if (allowDesktopFallback && noXrOrPad && Mouse.current != null)
            {
                triggerValue = Mathf.Max(triggerValue, Mouse.current.leftButton.isPressed ? 1 : 0);
                gripValue = Mathf.Max(gripValue, Mouse.current.rightButton.isPressed ? 1 : 0);
            }
            if (allowDesktopFallback && noXrOrPad && Keyboard.current != null)
            {
                a |= Keyboard.current.aKey.isPressed;
                b |= Keyboard.current.bKey.isPressed;
                Vector2 keyboardMove = Vector2.zero;
                if (Keyboard.current.wKey.isPressed) keyboardMove.y += 1;
                if (Keyboard.current.sKey.isPressed) keyboardMove.y -= 1;
                if (Keyboard.current.dKey.isPressed) keyboardMove.x += 1;
                if (Keyboard.current.aKey.isPressed) keyboardMove.x -= 1;
                leftStick = Expand(leftStick, keyboardMove);
            }
            if (allowDesktopFallback && noXrOrPad && Mouse.current != null && Mouse.current.rightButton.isPressed)
            {
                // The tutorial detects a physical look input; mouse-look must count as the right stick.
                rightStick = Expand(rightStick, Mouse.current.delta.ReadValue() * .04f);
            }

            bool trigger = triggerValue > .55f;
            bool grip = gripValue > .55f;
            bool aDown = a && !previousA;
            bool bDown = b && !previousB;
            bool triggerDown = trigger && !previousTrigger;
            bool triggerUp = !trigger && previousTrigger;
            previousA = a;
            previousB = b;
            previousTrigger = trigger;
            return new CareInputSnapshot(aDown, bDown, trigger, triggerDown, triggerUp, grip, leftStick, rightStick);
        }

        public static void ForgetEdges()
        {
            previousA = previousB = previousTrigger = false;
        }

        static Vector2 Expand(Vector2 current, Vector2 candidate) =>
            candidate.sqrMagnitude > current.sqrMagnitude ? candidate : current;
    }

    public readonly struct CareXboxSnapshot
    {
        public bool Connected { get; }
        public bool A { get; }
        public bool B { get; }
        public float LeftTrigger { get; }
        public float RightTrigger { get; }
        public Vector2 LeftStick { get; }
        public Vector2 RightStick { get; }

        public CareXboxSnapshot(bool connected, ushort buttons, byte leftTrigger, byte rightTrigger,
            short leftX, short leftY, short rightX, short rightY)
        {
            Connected = connected;
            A = connected && (buttons & 0x1000) != 0;
            B = connected && (buttons & 0x2000) != 0;
            LeftTrigger = connected ? Trigger(leftTrigger) : 0;
            RightTrigger = connected ? Trigger(rightTrigger) : 0;
            LeftStick = connected ? new Vector2(Axis(leftX), Axis(leftY)) : Vector2.zero;
            RightStick = connected ? new Vector2(Axis(rightX), Axis(rightY)) : Vector2.zero;

            if (LeftStick.sqrMagnitude > 1) LeftStick = LeftStick.normalized;
            if (RightStick.sqrMagnitude > 1) RightStick = RightStick.normalized;
        }

        static float Trigger(byte value) => value <= 30 ? 0 : Mathf.Clamp01((value - 30f) / 225f);

        static float Axis(short value)
        {
            const float deadzone = 7849;
            float magnitude = Mathf.Abs((float)value);
            if (magnitude <= deadzone) return 0;
            float normalized = (magnitude - deadzone) / (32767f - deadzone);
            return Mathf.Sign(value) * Mathf.Clamp01(normalized);
        }
    }

    public static class CareXboxInput
    {
        public static CareXboxSnapshot Read()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            for (uint index = 0; index < 4; index++)
            {
                try
                {
                    if (XInputGetState(index, out XInputState state) != 0) continue;
                    var pad = state.gamepad;
                    return new CareXboxSnapshot(true, pad.buttons, pad.leftTrigger, pad.rightTrigger,
                        pad.leftX, pad.leftY, pad.rightX, pad.rightY);
                }
                catch (DllNotFoundException)
                {
                    break;
                }
                catch (EntryPointNotFoundException)
                {
                    break;
                }
            }
#endif
            return default;
        }

        public static void SetMotorSpeeds(float left, float right)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var vibration = new XInputVibration
            {
                leftMotorSpeed = (ushort)(Mathf.Clamp01(left) * 65535),
                rightMotorSpeed = (ushort)(Mathf.Clamp01(right) * 65535)
            };
            for (uint index = 0; index < 4; index++)
            {
                try
                {
                    if (XInputSetState(index, ref vibration) == 0) break;
                }
                catch (DllNotFoundException)
                {
                    break;
                }
                catch (EntryPointNotFoundException)
                {
                    break;
                }
            }
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential)]
        struct XInputState
        {
            public uint packetNumber;
            public XInputGamepad gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct XInputGamepad
        {
            public ushort buttons;
            public byte leftTrigger;
            public byte rightTrigger;
            public short leftX;
            public short leftY;
            public short rightX;
            public short rightY;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct XInputVibration
        {
            public ushort leftMotorSpeed;
            public ushort rightMotorSpeed;
        }

        [DllImport("xinput1_4.dll")]
        static extern uint XInputGetState(uint userIndex, out XInputState state);

        [DllImport("xinput1_4.dll")]
        static extern uint XInputSetState(uint userIndex, ref XInputVibration vibration);
#endif
    }
}
