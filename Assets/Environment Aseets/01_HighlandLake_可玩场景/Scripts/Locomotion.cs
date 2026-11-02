using Oculus.Interaction;
using UnityEngine;
using UnityEngine.XR;

// The base Dreamcore controls walk and teleport. The tuning below also adds
// strafing, hold-to-turn, full-stick sprint, crouch and view recentering.
[DisallowMultipleComponent]
public sealed class Locomotion : MonoBehaviour
{
    public OVRCameraRig rig;
    public LineRenderer arc;
    public Transform landingMarker;

    [Header("DreamcorePark settings")]
    public float launchSpeed = 7f;
    public float turnAngle = 30f;
    public bool smoothMoveEnabled = true;
    [Min(0)] public float moveSpeed = 1.2f;
    [Range(0.05f, 0.8f)] public float moveDeadzone = 0.2f;

    [Header("Extended stick controls")]
    [Min(1f)] public float sprintMultiplier = 2.25f;
    [Range(0.75f, 1f)] public float sprintThreshold = 0.9f;
    [Min(0f)] public float continuousTurnSpeed = 120f;
    [Min(0f)] public float turnHoldDelay = 0.18f;
    [Range(0.5f, 1f)] public float flickThreshold = 0.75f;
    public bool sprintToggleByLeftClick = true;
    public bool crouchToggleByRightClick = true;
    public bool resetViewByYButton = true;

    [Header("Crouch and teleport angles")]
    [Min(0.1f)] public float crouchDrop = 0.55f;
    [Min(0.1f)] public float crouchBlendSpeed = 2.8f;
    [Min(0f)] public float teleportPitchDown = 20f;
    [Min(0f)] public float teleportPitchUp = 25f;
    [Min(1f)] public float teleportRayDistance = 25f;

    [Header("Comfort limits")]
    [Min(0.3f)] public float bodyRadius = 0.3f;
    [Min(0.1f)] public float bodyEyeHeight = 1.7f;
    [Min(0f)] public float floorClearance = 0.24f;
    [Range(0.05f, 0.95f)] public float minGroundNormalY = 0.45f;
    [Min(0.05f)] public float groundProbeHeight = 0.45f;
    [Min(0f)] public float maxStepHeight = 0.35f;

    private bool aiming;
    private bool turnLatched;
    private bool sprintToggled;
    private bool crouchToggled;
    private bool valid;
    private float turnHoldTime;
    private float teleportStickY;
    private bool previousLeftClick;
    private bool previousRightClick;
    private bool previousYButton;
    private bool trackingSpaceCaptured;
    private Vector3 trackingSpaceBasePosition;
    private Vector3 destination;
    private readonly RaycastHit[] castHits = new RaycastHit[32];
    private readonly RaycastHit[] pointerHits = new RaycastHit[128];
    private readonly Collider[] overlaps = new Collider[32];
    private RayInteractor rightRayInteractor;
    private Renderer landingRenderer;
    private MaterialPropertyBlock landingProperties;
    private float islandMinimumFloorY = float.NegativeInfinity;
    private bool islandFloorLimitResolved;
    private const float Skin = 0.015f;
    private const float GroundProbeTolerance = 0.2f;

    private void Awake()
    {
        if (arc != null)
        {
            arc.enabled = false;
        }
        if (landingMarker != null)
        {
            landingRenderer = landingMarker.GetComponentInChildren<Renderer>(true);
            landingProperties = new MaterialPropertyBlock();
        }
    }

    private void Update()
    {
        Vector2 leftStick = ReadThumbstick(OVRInput.Controller.LTouch, XRNode.LeftHand);
        Vector2 rightStick = ReadThumbstick(OVRInput.Controller.RTouch, XRNode.RightHand);
        bool rightConnected =
            OVRInput.IsControllerConnected(OVRInput.Controller.RTouch) ||
            InputDevices.GetDeviceAtXRNode(XRNode.RightHand).isValid;
        bool leftClick = ReadThumbstickClick(
            OVRInput.Controller.LTouch,
            XRNode.LeftHand,
            ref previousLeftClick);
        bool rightClick = ReadThumbstickClick(
            OVRInput.Controller.RTouch,
            XRNode.RightHand,
            ref previousRightClick);
        bool yButton = ReadYButton(ref previousYButton);
        ApplyInput(
            leftStick,
            rightStick,
            rightConnected,
            Time.deltaTime,
            leftClick,
            rightClick,
            yButton);
    }

    private static Vector2 ReadThumbstick(OVRInput.Controller controller, XRNode node)
    {
        Vector2 value = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, controller);
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (value.sqrMagnitude < 0.0001f &&
            device.isValid &&
            device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 xrValue))
        {
            return xrValue;
        }
        return value;
    }

    private static bool ReadThumbstickClick(
        OVRInput.Controller controller,
        XRNode node,
        ref bool previous)
    {
        if (OVRInput.IsControllerConnected(controller))
        {
            return OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, controller);
        }

        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        bool pressed =
            device.isValid &&
            device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool current) &&
            current;
        bool clicked = pressed && !previous;
        previous = pressed;
        return clicked;
    }

    private static bool ReadYButton(ref bool previous)
    {
        bool currentlyPressed =
            OVRInput.Get(OVRInput.RawButton.Y) ||
            (InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).isValid &&
             InputDevices.GetDeviceAtXRNode(XRNode.LeftHand)
                 .TryGetFeatureValue(CommonUsages.secondaryButton, out bool current) &&
             current);
        bool clicked =
            OVRInput.GetDown(OVRInput.RawButton.Y) ||
            (currentlyPressed && !previous);
        previous = currentlyPressed;
        return clicked;
    }

    public void ApplyInput(
        Vector2 leftStick,
        Vector2 rightStick,
        bool rightConnected,
        float deltaTime,
        bool leftStickClicked = false,
        bool rightStickClicked = false,
        bool yButtonClicked = false)
    {
        if (rig == null || rig.centerEyeAnchor == null)
        {
            return;
        }

        EnforceIslandFloorLimit();

        if (leftStickClicked && sprintToggleByLeftClick)
        {
            sprintToggled = !sprintToggled;
        }
        if (rightStickClicked && crouchToggleByRightClick)
        {
            crouchToggled = !crouchToggled;
        }
        if (yButtonClicked && resetViewByYButton)
        {
            ResetView();
        }
        UpdateCrouch(deltaTime);

        bool nextAim = rightConnected && Mathf.Abs(rightStick.y) > 0.65f &&
                       arc != null && landingMarker != null &&
                       rig.rightControllerAnchor != null;
        teleportStickY = rightStick.y;
        float turn = rightConnected ? rightStick.x : 0f;
        if (Mathf.Abs(turn) < 0.3f)
        {
            turnLatched = false;
            turnHoldTime = 0f;
        }

        if (Mathf.Abs(turn) > flickThreshold && !turnLatched &&
            !nextAim && !aiming)
        {
            SnapTurn(Mathf.Sign(turn));
            turnLatched = true;
            turnHoldTime = deltaTime;
        }
        else if (turnLatched && !nextAim && !aiming)
        {
            turnHoldTime += deltaTime;
            if (turnHoldTime >= turnHoldDelay)
            {
                transform.RotateAround(
                    rig.centerEyeAnchor.position,
                    Vector3.up,
                    Mathf.Sign(turn) * continuousTurnSpeed *
                    Mathf.Min(deltaTime, 0.1f));
            }
        }

        bool wasAiming = aiming;
        if (aiming && !nextAim && rightConnected && valid)
        {
            TryTeleport(destination);
        }

        aiming = nextAim;
        if (arc != null)
        {
            arc.enabled = false;
        }
        if (landingMarker != null)
        {
            landingMarker.gameObject.SetActive(false);
        }

        if (!aiming)
        {
            valid = false;
            if (!wasAiming)
            {
                bool sprinting = sprintToggled ||
                                 leftStick.magnitude >= sprintThreshold;
                TrySmoothMove(
                    leftStick,
                    deltaTime,
                    sprinting ? sprintMultiplier : 1f);
            }
            return;
        }

        UpdateTeleportLanding();
    }

    private void UpdateTeleportLanding()
    {
        Transform hand = rig.rightControllerAnchor;
        Vector3 origin = hand.position;
        Vector3 direction = hand.forward;

        EnsureRightRayInteractor();
        if (rightRayInteractor != null)
        {
            rightRayInteractor.MaxRayLength =
                Mathf.Max(rightRayInteractor.MaxRayLength, teleportRayDistance);
            Ray pointerRay = rightRayInteractor.Ray;
            if (pointerRay.direction.sqrMagnitude > 0.1f)
            {
                origin = pointerRay.origin;
                direction = pointerRay.direction.normalized;
            }
        }

        valid = false;
        destination = default;

        int count = Physics.RaycastNonAlloc(
            origin,
            direction,
            pointerHits,
            teleportRayDistance,
            ~0,
            QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = pointerHits[i];
            if (hit.distance <= 0.05f ||
                OwnCollider(hit.collider) ||
                hit.distance >= nearest)
            {
                continue;
            }
            nearest = hit.distance;
            destination = hit.point;
            valid = CanStand(hit.normal) &&
                    !IsWaterSurface(hit.point) &&
                    IsLandingClear(hit.point);
        }

        SetLandingMarker(destination, valid);
    }

    private void EnsureRightRayInteractor()
    {
        if (rightRayInteractor != null ||
            rig == null ||
            rig.rightControllerAnchor == null)
        {
            return;
        }

        float nearest = float.PositiveInfinity;
        foreach (RayInteractor candidate in
                 rig.GetComponentsInChildren<RayInteractor>(true))
        {
            float distance = Vector3.Distance(
                candidate.transform.position,
                rig.rightControllerAnchor.position);
            if (distance < nearest)
            {
                nearest = distance;
                rightRayInteractor = candidate;
            }
        }
    }

    private void SetLandingMarker(Vector3 point, bool walkable)
    {
        if (landingMarker == null)
        {
            return;
        }
        if (destination == default)
        {
            landingMarker.gameObject.SetActive(false);
            return;
        }

        landingMarker.position = point + Vector3.up * 0.025f;
        landingMarker.gameObject.SetActive(true);
        Color color = walkable
            ? new Color(0.45f, 0.85f, 0.85f)
            : new Color(1f, 0.4f, 0.45f);
        if (landingRenderer != null)
        {
            landingProperties ??= new MaterialPropertyBlock();
            landingRenderer.GetPropertyBlock(landingProperties);
            landingProperties.SetColor("_Color", color);
            landingProperties.SetColor("_BaseColor", color);
            landingRenderer.SetPropertyBlock(landingProperties);
        }
    }

    public bool TryTeleport(Vector3 point)
    {
        if (rig == null || rig.centerEyeAnchor == null ||
            IsWaterSurface(point) || !IsLandingClear(point))
        {
            return false;
        }

        Vector3 offset = rig.centerEyeAnchor.position - transform.position;
        offset.y = 0f;
        Vector3 targetPosition = point + Vector3.up * floorClearance - offset;
        targetPosition.y = Mathf.Max(targetPosition.y, islandMinimumFloorY);
        transform.position = targetPosition;
        return true;
    }

    public bool IsLandingClear(Vector3 point)
    {
        return !Physics.CheckCapsule(
            point + Vector3.up * (floorClearance + 0.32f),
            point + Vector3.up * (floorClearance + bodyEyeHeight - 0.15f),
            bodyRadius,
            ~0,
            QueryTriggerInteraction.Ignore);
    }

    public bool TrySmoothMove(
        Vector2 stick,
        float deltaTime,
        float speedMultiplier = 1f)
    {
        if (!smoothMoveEnabled || rig == null || rig.centerEyeAnchor == null ||
            aiming || deltaTime <= 0f || Time.timeScale <= 0f)
        {
            return false;
        }

        float amount = Mathf.Clamp01(stick.magnitude);
        if (amount <= moveDeadzone)
        {
            return false;
        }

        Vector3 headForward = Vector3.ProjectOnPlane(rig.centerEyeAnchor.forward, Vector3.up);
        if (headForward.sqrMagnitude < 0.001f)
        {
            headForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }
        headForward.Normalize();

        Vector2 input = stick.normalized * Mathf.InverseLerp(moveDeadzone, 1f, amount);
        Vector3 requested = (
            headForward * input.y +
            Vector3.Cross(Vector3.up, headForward) * input.x) *
            Mathf.Max(0f, moveSpeed * speedMultiplier) *
            Mathf.Min(deltaTime, 0.1f);

        int steps = Mathf.Max(1, Mathf.CeilToInt(requested.magnitude / 0.1f));
        Vector3 before = transform.position;
        for (int i = 0; i < steps; i++)
        {
            Vector3 feet = Feet();
            if (!TryFindGround(feet, out RaycastHit currentGround))
            {
                break;
            }

            Vector3 movement = Sweep(
                feet,
                requested / steps,
                currentGround.collider);
            if (!TryFindGround(feet + movement, out RaycastHit ground) ||
                ground.point.y - feet.y > maxStepHeight ||
                !BodyClear(
                    ground.point + Vector3.up * floorClearance,
                    ground.collider))
            {
                break;
            }
            Vector3 targetPosition = ground.point + Vector3.up * floorClearance;
            targetPosition.y = Mathf.Max(targetPosition.y, islandMinimumFloorY);
            transform.position = targetPosition;
        }

        return (transform.position - before).sqrMagnitude > 0.00000001f;
    }

    private Vector3 Feet()
    {
        Vector3 feet = rig.centerEyeAnchor.position;
        feet.y = transform.position.y - floorClearance;
        return feet;
    }

    private bool TryFindGround(Vector3 feet, out RaycastHit ground)
    {
        ground = default;
        float probeDistance =
            groundProbeHeight + maxStepHeight + GroundProbeTolerance;
        Vector3 origin = feet + Vector3.up * probeDistance;
        int count = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            castHits,
            probeDistance,
            ~0,
            QueryTriggerInteraction.Ignore);
        if (count == castHits.Length)
        {
            return false;
        }

        float closest = float.PositiveInfinity;
        bool supported = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = castHits[i];
            if (OwnCollider(hit.collider) || hit.distance >= closest)
            {
                continue;
            }
            closest = hit.distance;
            ground = hit;
            supported = CanStand(hit.normal);
        }
        return supported;
    }

    private bool CanStand(Vector3 normal)
    {
        return normal.y >= minGroundNormalY;
    }

    private bool BodyClear(Vector3 feet, Collider groundCollider)
    {
        int count = Physics.OverlapCapsuleNonAlloc(
            feet + Vector3.up * 0.32f,
            feet + Vector3.up * (bodyEyeHeight - 0.15f),
            bodyRadius,
            overlaps,
            ~0,
            QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length)
        {
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            if (!OwnCollider(overlaps[i]) && overlaps[i] != groundCollider)
            {
                return false;
            }
        }
        return true;
    }

    private Vector3 Sweep(
        Vector3 feet,
        Vector3 displacement,
        Collider groundCollider)
    {
        Vector3 result = Vector3.zero;
        Vector3 remaining = displacement;
        for (int pass = 0;
             pass < 3 && remaining.sqrMagnitude > 0.00000001f;
             pass++)
        {
            float distance = remaining.magnitude;
            Vector3 direction = remaining / distance;
            int count = Physics.CapsuleCastNonAlloc(
                feet + result + Vector3.up * 0.32f,
                feet + result + Vector3.up * (bodyEyeHeight - 0.15f),
                bodyRadius,
                direction,
                castHits,
                distance + Skin,
                ~0,
                QueryTriggerInteraction.Ignore);
            if (count == castHits.Length)
            {
                break;
            }

            float nearest = float.PositiveInfinity;
            Vector3 normal = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = castHits[i];
                if (!OwnCollider(hit.collider) &&
                    hit.collider != groundCollider &&
                    hit.distance < nearest &&
                    Vector3.Dot(direction, hit.normal) < -0.001f)
                {
                    nearest = hit.distance;
                    normal = hit.normal;
                }
            }

            if (float.IsPositiveInfinity(nearest))
            {
                result += remaining;
                break;
            }

            Vector3 advance = direction * Mathf.Clamp(nearest - Skin, 0f, distance);
            result += advance;
            remaining = Vector3.ProjectOnPlane(remaining - advance, normal);
            remaining.y = 0f;
        }
        return result;
    }

    private bool OwnCollider(Collider collider)
    {
        return collider != null && collider.transform.IsChildOf(transform);
    }

    private void SnapTurn(float direction)
    {
        transform.RotateAround(
            rig.centerEyeAnchor.position,
            Vector3.up,
            direction * turnAngle);
    }

    private void EnforceIslandFloorLimit()
    {
        if (!islandFloorLimitResolved)
        {
            islandFloorLimitResolved = true;
            foreach (GameObject candidate in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                if (candidate.name != "Island_1" ||
                    !candidate.scene.IsValid() ||
                    !candidate.scene.isLoaded)
                {
                    continue;
                }

                Collider islandCollider = candidate.GetComponent<Collider>();
                if (islandCollider != null)
                {
                    islandMinimumFloorY =
                        islandCollider.bounds.max.y + floorClearance;
                }
                break;
            }
        }

        if (transform.position.y < islandMinimumFloorY)
        {
            Vector3 position = transform.position;
            position.y = islandMinimumFloorY;
            transform.position = position;
        }
    }

    public void ResetView()
    {
        if (OVRManager.display != null)
        {
            OVRManager.display.RecenterPose();
        }
    }

    private void UpdateCrouch(float deltaTime)
    {
        if (rig == null || rig.trackingSpace == null)
        {
            return;
        }
        if (!trackingSpaceCaptured)
        {
            trackingSpaceBasePosition = rig.trackingSpace.localPosition;
            trackingSpaceCaptured = true;
        }

        Vector3 localPosition = rig.trackingSpace.localPosition;
        float targetY =
            trackingSpaceBasePosition.y - (crouchToggled ? crouchDrop : 0f);
        localPosition.y = Mathf.MoveTowards(
            localPosition.y,
            targetY,
            crouchBlendSpeed * Mathf.Max(0f, deltaTime));
        rig.trackingSpace.localPosition = localPosition;
    }

    private static bool IsWaterSurface(Vector3 point)
    {
        float lake = Mathf.Sqrt(
            (point.x / 23f) * (point.x / 23f) +
            ((point.z - 7f) / 19f) * ((point.z - 7f) / 19f));
        return lake < 1.02f || point.y < -0.12f;
    }

    private void OnDisable()
    {
        CancelAim();
        turnLatched = false;
        turnHoldTime = 0f;
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus)
        {
            CancelAim();
            turnLatched = false;
        }
    }

    private void CancelAim()
    {
        aiming = false;
        valid = false;
        if (arc != null)
        {
            arc.enabled = false;
        }
        if (landingMarker != null)
        {
            landingMarker.gameObject.SetActive(false);
        }
    }
}
