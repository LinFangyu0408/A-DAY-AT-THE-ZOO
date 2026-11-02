using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[AddComponentMenu("Chibi Monkey/Hi Ray Interaction")]
public sealed class ChibiMonkeyHiRayInteraction : MonoBehaviour
{
    private const string HiStateName = "Hi";
    private const string AnimationParameterName = "animation";
    private const int HiAnimationValue = 13;

    [Header("Ray")]
    [Tooltip("Leave empty to use the right controller, right hand, or main camera automatically.")]
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private float maxRayDistance = 30f;
    [SerializeField] private LayerMask raycastLayers = ~0;

    [Header("Animation")]
    [SerializeField] private string hiStateName = HiStateName;
    [SerializeField] private string animationParameterName = AnimationParameterName;
    [SerializeField] private int hiAnimationValue = HiAnimationValue;
    [SerializeField] private float crossFadeDuration = 0.1f;

    private InputAction activateAction;
    private bool activationRequested;

    private void Awake()
    {
        EnsureMonkeyColliders();
        ResolveRayOrigin();
    }

    private void OnEnable()
    {
        activateAction = new InputAction(
            "ChibiMonkeyHi",
            InputActionType.Button,
            expectedControlType: "Button");

        activateAction.AddBinding("<XRController>/trigger");
        activateAction.AddBinding("<XRController>/{PrimaryAction}");
        activateAction.AddBinding("<Keyboard>/enter");
        activateAction.AddBinding("<Mouse>/leftButton");
        activateAction.performed += OnActivatePerformed;
        activateAction.Enable();
    }

    private void OnDisable()
    {
        if (activateAction == null)
        {
            return;
        }

        activateAction.performed -= OnActivatePerformed;
        activateAction.Disable();
        activateAction.Dispose();
        activateAction = null;
        activationRequested = false;
    }

    private void Update()
    {
        if (!activationRequested)
        {
            return;
        }

        activationRequested = false;
        ResolveRayOrigin();

        if (rayOrigin == null)
        {
            Debug.LogWarning("[Chibi Monkey] Ray origin was not found.", this);
            return;
        }

        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                maxRayDistance,
                raycastLayers,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        Animator targetAnimator = ResolveHiAnimator(hit.collider);
        if (targetAnimator != null)
        {
            PlayHi(targetAnimator);
        }
    }

    private void OnActivatePerformed(InputAction.CallbackContext context)
    {
        activationRequested = true;
    }

    private void PlayHi(Animator animator)
    {
        int hiStateHash = Animator.StringToHash(hiStateName);
        if (!animator.HasState(0, hiStateHash))
        {
            Debug.LogWarning(
                $"[Chibi Monkey] '{animator.name}' has no animation state named '{hiStateName}'.",
                animator);
            return;
        }

        int parameterHash = Animator.StringToHash(animationParameterName);
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == parameterHash &&
                parameter.type == AnimatorControllerParameterType.Int)
            {
                animator.SetInteger(parameterHash, hiAnimationValue);
                break;
            }
        }

        animator.CrossFadeInFixedTime(
            hiStateHash,
            crossFadeDuration,
            layer: 0,
            fixedTimeOffset: 0f);
    }

    private static Animator ResolveHiAnimator(Collider hitCollider)
    {
        Animator[] animators = hitCollider.GetComponentsInParent<Animator>(true);
        foreach (Animator animator in animators)
        {
            if (animator.HasState(0, Animator.StringToHash(HiStateName)))
            {
                return animator;
            }
        }

        return null;
    }

    private void ResolveRayOrigin()
    {
        if (rayOrigin != null)
        {
            return;
        }

        string[] preferredNames =
        {
            "RightControllerAnchor",
            "RightHandAnchor",
            "RightHandOnControllerAnchor",
            "CenterEyeAnchor",
        };

        Transform[] transforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (string preferredName in preferredNames)
        {
            foreach (Transform candidate in transforms)
            {
                if (candidate.name == preferredName)
                {
                    rayOrigin = candidate;
                    return;
                }
            }
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            rayOrigin = mainCamera.transform;
        }
    }

    private static void EnsureMonkeyColliders()
    {
        Animator[] animators = FindObjectsByType<Animator>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int hiStateHash = Animator.StringToHash(HiStateName);
        foreach (Animator animator in animators)
        {
            if (!animator.HasState(0, hiStateHash) ||
                animator.GetComponentInChildren<Collider>(true) != null)
            {
                continue;
            }

            Renderer[] renderers = animator.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                continue;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 lossyScale = animator.transform.lossyScale;
            BoxCollider box = animator.gameObject.AddComponent<BoxCollider>();
            box.center = animator.transform.InverseTransformPoint(bounds.center);
            box.size = new Vector3(
                bounds.size.x / Mathf.Max(Mathf.Abs(lossyScale.x), 0.0001f),
                bounds.size.y / Mathf.Max(Mathf.Abs(lossyScale.y), 0.0001f),
                bounds.size.z / Mathf.Max(Mathf.Abs(lossyScale.z), 0.0001f));
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureSceneInteraction()
    {
        ChibiMonkeyHiRayInteraction existing =
            FindFirstObjectByType<ChibiMonkeyHiRayInteraction>();
        if (existing != null)
        {
            return;
        }

        GameObject interactionObject = new GameObject("Chibi Monkey Hi Interaction");
        interactionObject.AddComponent<ChibiMonkeyHiRayInteraction>();
    }
}
