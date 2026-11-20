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

        public static bool IsPlayStationController
        {
            get
            {
                if (LooksLikePlayStation(Gamepad.current)) return true;
                foreach (var device in InputSystem.devices)
                    if (device.added && LooksLikePlayStation(device)) return true;
                return false;
            }
        }
        // The current UI follows the reference screenshots and uses Xbox-style
        // letter badges. PlayStation face buttons still map to the same actions:
        // Cross = A and Circle = B. The badge set can be switched at runtime
        // from the settings strip without touching layout or wiring.
        public static string ConfirmLabel => PadStyle == PadLayout.PlayStation ? "✕" : "A";
        public static string BackLabel => PadStyle == PadLayout.PlayStation ? "○" : "B";
        // Trigger badges keep the letters either way (RT/R2 both read as RT on
        // the plates, so no relabeling is needed for them).

        public enum PadLayout { Xbox, PlayStation }
        // Live style used by every key badge; the auto-detector picks it on
        // the first input frame and again whenever a device is plugged in or
        // removed, because a DualSense can enumerate late (XR retries rescan
        // USB and re-create the HID device several seconds into the session).
        public static PadLayout PadStyle { get; private set; } = PadLayout.Xbox;
        // A manual flip on the settings chip wins: the auto-detector then
        // stops touching the glyphs for the rest of the session.
        public static bool StylePinned { get; private set; }
        static bool autoStyleApplied;
        static CareInput()
        {
            // Re-detect whenever the device list changes: the DualSense often
            // reconnects after the XR loader churned USB, and without this the
            // badges stayed on Xbox letters even though the pad was there.
            InputSystem.onDeviceChange += OnDeviceChanged;
        }
        static void OnDeviceChanged(UnityEngine.InputSystem.InputDevice device, InputDeviceChange change)
        {
            if (change != InputDeviceChange.Added && change != InputDeviceChange.Reconnected &&
                change != InputDeviceChange.Removed && change != InputDeviceChange.Disconnected) return;
            ApplyAutoStyle();
        }
        /// <summary>Detect-and-apply without touching a manually pinned style.</summary>
        public static void ApplyAutoStyle()
        {
            if (StylePinned) return;
            SetPadStyle(AutoDetectStyle());
        }
        /// <summary>Swap the badge glyphs (A/B ⇄ ✕/○). Returns the new style.</summary>
        public static PadLayout SetPadStyle(PadLayout style)
        {
            PadStyle = style;
            var renamed = LabelsChanged;
            renamed?.Invoke();
            return style;
        }
        /// <summary>Manual choice from the settings chip: pins so auto-detect stops overriding it.</summary>
        public static PadLayout PinStyle(PadLayout style)
        {
            StylePinned = true;
            return SetPadStyle(style);
        }
        /// <summary> Fired after the glyph set changed, so every on-screen badge relabels itself.</summary>
        public static event Action LabelsChanged;

        /// <summary>Display name of the pad that currently drives the badges ("" when none).</summary>
        public static string PadDisplayName
        {
            get
            {
                var pad = Gamepad.current;
                if (pad == null && Gamepad.all.Count > 0) pad = Gamepad.all[0];
                return pad != null ? pad.displayName : string.Empty;
            }
        }

        /// <summary>
        /// One-line launch report of the input device, so "which version is the
        /// game actually running" can be answered from Editor.log without a
        /// headset: VR + Touch, PS5 pad, Xbox pad, or plain PC keyboard.
        /// </summary>
        public static string DescribeDevice()
        {
            if (IsPlayStationController)
                return "PS5 · " + (PadDisplayName.Length > 0 ? PadDisplayName : "PlayStation pad");
            if (Gamepad.all.Count > 0)
                return "Xbox · " + (PadDisplayName.Length > 0 ? PadDisplayName : "gamepad");
            return "PC · keyboard + mouse";
        }
        // Best guess from whatever is connected (auto-pick on the settings strip).
        public static PadLayout AutoDetectStyle()
        {
            if (IsPlayStationController) return PadLayout.PlayStation;
            foreach (var pad in Gamepad.all)
                if (pad != null && (pad.layout.Contains("DualSense") || pad.layout.Contains("DualShock") || LooksLikePlayStation(pad))) return PadLayout.PlayStation;
            return PadLayout.Xbox;
        }
        /// <summary>Raise the relabel event for every live badge (initial paint too).</summary>
        public static void RefreshLabels() => LabelsChanged?.Invoke();
        public static bool AnyGamepadConnected => Gamepad.all.Count > 0;
        public static Vector2 GamepadLeftStick => ReadGamepadStick(true);
        public static Vector2 GamepadRightStick => ReadGamepadStick(false);

        public static CareInputSnapshot Read(bool allowDesktopFallback = true)
        {
            // One-shot auto-detect on the first input frame: the DualSense is
            // usually enumerated by then, so the HUD starts on ✕/○ instead of
            // waiting for the user to find the settings chip. Plugging a pad
            // in later re-detects via onDeviceChange.
            if (!autoStyleApplied)
            {
                autoStyleApplied = true;
                ApplyAutoStyle();
            }
            CareXboxSnapshot xbox = CareXboxInput.Read();
            XRInputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            XRInputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            // Hand-tracking devices occupy the same XR nodes as controllers but
            // carry no A/B buttons. Treating them as controllers used to switch
            // the keyboard fallback off completely, so a headset running on hand
            // tracking alone could not even start the round.
            bool rightXr = right.isValid && !IsHandTrackingDevice(right);
            bool leftXr = left.isValid && !IsHandTrackingDevice(left);
            bool rightOvr = OVRInput.IsControllerConnected(OVRInput.Controller.RTouch);
            bool leftOvr = OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);

            bool gamepadConnected = false;
            bool a = xbox.A, b = xbox.B;
            float triggerValue = xbox.RightTrigger;
            float gripValue = xbox.LeftTrigger;
            foreach (Gamepad pad in Gamepad.all)
            {
                gamepadConnected = true;
                a |= pad.buttonSouth.isPressed;
                b |= pad.buttonEast.isPressed;
                triggerValue = Mathf.Max(triggerValue, pad.rightTrigger.ReadValue());
                gripValue = Mathf.Max(gripValue, pad.leftTrigger.ReadValue());
            }

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

            Vector2 leftStick = GamepadLeftStick;
            Vector2 rightStick = GamepadRightStick;
            leftStick = Expand(leftStick, xbox.LeftStick);
            rightStick = Expand(rightStick, xbox.RightStick);
            if (leftOvr) leftStick = Expand(leftStick, OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch));
            if (rightOvr) rightStick = Expand(rightStick, OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch));
            if (leftXr && left.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out Vector2 xrLeft)) leftStick = Expand(leftStick, xrLeft);
            if (rightXr && right.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out Vector2 xrRight)) rightStick = Expand(rightStick, xrRight);

            bool noXrOrPad = !gamepadConnected && !xbox.Connected &&
                             !rightXr && !leftXr && !rightOvr && !leftOvr;
            if (allowDesktopFallback && noXrOrPad && Mouse.current != null)
            {
                triggerValue = Mathf.Max(triggerValue, Mouse.current.leftButton.isPressed ? 1 : 0);
                gripValue = Mathf.Max(gripValue, Mouse.current.rightButton.isPressed ? 1 : 0);
            }
            if (allowDesktopFallback && noXrOrPad && Keyboard.current != null)
            {
                // Space also stands in for A so desktop preview matches the
                // "PRESS A TO START" prompt either way.
                a |= Keyboard.current.aKey.isPressed || Keyboard.current.spaceKey.isPressed;
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

        // OpenXR reports tracked hands on the same nodes as Touch controllers.
        // Their device name always carries "Hand" ("Oculus Hand (R)", "Hands
        // (Right)"), while controllers are named "Oculus Touch Controller".
        static bool IsHandTrackingDevice(XRInputDevice device)
        {
            if (!device.isValid) return false;
            string name = device.name ?? string.Empty;
            return Contains(name, "Hand");
        }

        static bool LooksLikePlayStation(UnityEngine.InputSystem.InputDevice device)
        {
            if (device == null) return false;
            string layout = device.layout ?? string.Empty;
            string display = device.displayName ?? string.Empty;
            // Raw HID entries can appear before the gamepad layout is matched,
            // so the product string is checked as well (DualSense productId
            // 0x0CE6 / DualShock 0x05C4 both show up in the description).
            // InputDeviceDescription is a struct, so no null propagation.
            string product = device.description.product ?? string.Empty;
            return Contains(layout, "DualSense") || Contains(layout, "DualShock") ||
                   Contains(display, "DualSense") || Contains(display, "DualShock") ||
                   Contains(product, "DualSense") || Contains(product, "DualShock") ||
                   Contains(display, "PS5") || Contains(display, "PS4") ||
                   Contains(display, "PlayStation") || Contains(product, "PlayStation");
        }
        static bool Contains(string value, string token) =>
            value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        static Vector2 ReadGamepadStick(bool left)
        {
            Vector2 value = Vector2.zero;
            foreach (Gamepad pad in Gamepad.all)
                value = Expand(value, left ? pad.leftStick.ReadValue() : pad.rightStick.ReadValue());
            return value;
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
