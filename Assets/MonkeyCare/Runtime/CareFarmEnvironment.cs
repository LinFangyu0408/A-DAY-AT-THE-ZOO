using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ADayAtTheZoo.Monkeys.Care
{
    // Builds the small feeding area around the start point without modifying
    // the authored Highland Lake scene. The open barn sits behind the player,
    // and the existing banana-box artwork is reused for an indoor crate stack.
    [DisallowMultipleComponent]
    public sealed class CareFarmEnvironment : MonoBehaviour
    {
        public const float MarketStandDistance = 5.65f;
        public const float WindmillScale = .72f;
        public const float BarnScale = .88f;
        public const float BarnCenterDistance = MarketStandDistance;

        Camera playerCamera;
        GameObject marketStand;
        CarePickupMarker pickupPoint;

        public CarePickupMarker PickupPoint
        {
            get
            {
                if (!pickupPoint) pickupPoint = GetComponentInChildren<CarePickupMarker>(true);
                if (!pickupPoint) pickupPoint = FindFirstObjectByType<CarePickupMarker>(FindObjectsInactive.Include);
                return pickupPoint;
            }
        }

        public static CareFarmEnvironment Create(Camera camera)
        {
            if (!camera) return null;
            var existing = FindFirstObjectByType<CareFarmEnvironment>(FindObjectsInactive.Include);
            if (existing) return existing;

            var root = new GameObject("Care Farm Environment");
            var environment = root.AddComponent<CareFarmEnvironment>();
            environment.playerCamera = camera;
            environment.Build();
            return environment;
        }

        void Build()
        {
            Transform player = playerCamera.transform;
            Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up);
            if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 groundBelowPlayer = FindGround(player.position);
            // The stand sits BEHIND the player's opening view, nudged to the
            // left: invisible until the player turns around. Because the stall
            // is now behind, its counter (+z local front) faces back toward
            // the player's start position.
            Quaternion facingPlayer = Quaternion.LookRotation(forward, Vector3.up);
            // Cross(forward, up) points left of the player's view direction.
            Vector3 left = Vector3.Cross(forward, Vector3.up).normalized;
            Vector3 standPlan = groundBelowPlayer - forward * 8f +
                                left * 3.4f;
            marketStand = FindNamed("Banana Market Stand");
            Vector3 standGround = FindGround(standPlan + Vector3.up * 1.5f,
                marketStand ? marketStand.transform : null);
            if (!marketStand)
            {
                marketStand = CreateModel(
                    "Farm/MarketStand_1",
                    "Banana Market Stand",
                    standPlan,
                    facingPlayer,
                    1f);
                if (!marketStand)
                    marketStand = CreateFallbackMarketStand(
                        standPlan,
                        facingPlayer);
            }
            if (marketStand)
            {
                // The player and the stall can sit on different spots of the
                // island. Sample the terrain again at the stall's own X/Z;
                // reusing the player's height makes the stall appear airborne
                // when it is placed behind them on a slope.
                AlignBottomToGround(marketStand, standGround.y + .015f);
                SetBoundsCenterXZ(marketStand, standPlan);
                AddStaticMeshColliders(marketStand);
                AddStallBlocker(marketStand);
                if (VisualBounds(marketStand, out var standBounds))
                    ClearVegetationInside(standBounds);
                Transform stack = FindNamed("Banana Crate Stack")?.transform;
                int crateCount = stack
                    ? stack.GetComponentsInChildren<Transform>(true)
                        .Count(item => item.name.StartsWith("Banana Crate"))
                    : 0;
                if (!stack || crateCount != 2)
                {
                    if (stack) stack.gameObject.SetActive(false);
                    stack = StackTwoBananaCrates(marketStand);
                }
                if (stack) pickupPoint = CreatePickupMarker(stack.gameObject);
            }

            Vector3 windmillPoint = groundBelowPlayer + forward * 20f +
                                    Vector3.Cross(Vector3.up, forward) * 7f;
            Vector3 windmillGround = FindGround(windmillPoint + Vector3.up * 1.5f);
            Vector3 windmillFacing = groundBelowPlayer - windmillGround;
            windmillFacing.y = 0;
            if (windmillFacing.sqrMagnitude < .01f) windmillFacing = -forward;
            var windmill = FindNamed("Background Windmill");
            if (!windmill)
            {
                windmill = CreateModel(
                    "Farm/Windmill",
                    "Background Windmill",
                    windmillGround,
                    Quaternion.LookRotation(windmillFacing.normalized, Vector3.up),
                    WindmillScale);
            }
            if (windmill) AlignBottomToGround(windmill, windmillGround.y + .01f);
        }

        GameObject CreateFallbackMarketStand(Vector3 position, Quaternion rotation)
        {
            var root = new GameObject("Banana Market Stand");
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation(position, rotation);
            var wood = RuntimeMaterial(Color.white, "MarketWood");
            var cloth = RuntimeMaterial(new Color(.94f, .42f, .12f, 1f), "MarketFabric");
            var clothLight = RuntimeMaterial(new Color(1f, .91f, .64f, 1f), "MarketFabric");

            Piece(root.transform, "Counter", new Vector3(0, 1.03f, 0),
                new Vector3(3.2f, .16f, 1.3f), wood);
            Piece(root.transform, "Counter surface", new Vector3(0, 1.13f, .02f),
                new Vector3(3.05f, .05f, 1.18f), clothLight);
            Piece(root.transform, "Front panel", new Vector3(0, .56f, .55f),
                new Vector3(3.0f, .95f, .12f), wood);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -1.52f : 1.52f;
                float z = i < 2 ? -.52f : .52f;
                Piece(root.transform, "Post " + (i + 1), new Vector3(x, 1.4f, z),
                    new Vector3(.14f, 2.8f, .14f), wood);
            }
            for (int i = 0; i < 5; i++)
            {
                Piece(root.transform, "Awning stripe " + (i + 1),
                    new Vector3((i - 2f) * .74f, 2.72f, 0),
                    new Vector3(.72f, .16f, 1.8f), i % 2 == 0 ? cloth : clothLight);
            }
            var counter = root.transform.Find("Counter surface");
            counter.name = "Counter Surface";
            return root;
        }

        static GameObject Piece(Transform parent, string name, Vector3 localPosition,
            Vector3 localScale, Material material)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localScale = localScale;
            var renderer = piece.GetComponent<Renderer>();
            if (material) renderer.sharedMaterial = material;
            return piece;
        }

        static Material RuntimeMaterial(Color color, string textureName)
        {
            Shader shader = SurfaceShader();
            if (!shader) return null;
            var material = new Material(shader) { name = "Runtime market material" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            var texture = Resources.Load<Texture2D>("Farm/" + textureName);
            if (texture)
            {
                material.mainTexture = texture;
                material.mainTextureScale = new Vector2(1.5f, 1.5f);
            }
            return material;
        }

        static Shader SurfaceShader()
        {
            bool usesScriptableRenderPipeline =
                UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
            Shader shader = Shader.Find(usesScriptableRenderPipeline
                ? "Universal Render Pipeline/Lit" : "Standard");
            if (!shader) shader = Shader.Find(usesScriptableRenderPipeline
                ? "Standard" : "Universal Render Pipeline/Lit");
            return shader;
        }

        Transform StackTwoBananaCrates(GameObject stand)
        {
            Transform source = Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "banana box");
            if (!source)
            {
                Debug.LogWarning("[Monkey Care] Could not find the banana box artwork.");
                return null;
            }

            var stackRoot = new GameObject("Banana Crate Stack").transform;
            stackRoot.SetParent(transform, false);
            if (!VisualBounds(stand, out var standBounds))
                standBounds = new Bounds(stand.transform.position, new Vector3(3.2f, 2.8f, 1.3f));
            Vector3 counterCenter = stand.transform.position;
            counterCenter.y = standBounds.min.y + 1.17f;
            var counter = stand.transform.Find("Counter Surface");
            if (counter && VisualBounds(counter.gameObject, out var surfaceBounds))
                counterCenter.y = surfaceBounds.max.y + .025f;
            stackRoot.SetPositionAndRotation(counterCenter, Quaternion.Euler(0,
                stand.transform.eulerAngles.y, 0));

            float sourceYaw = source.eulerAngles.y;
            var first = CreateCrateCopy(source, stackRoot, sourceYaw, "left");
            var second = CreateCrateCopy(source, stackRoot, sourceYaw, "right");
            float width = Mathf.Max(.2f, VisualBounds(first).size.x);
            float pitch = width + Mathf.Max(.06f, width * .10f);
            Vector3 right = stand.transform.right;
            PlaceOnY(first, counterCenter - right * (pitch * .5f), counterCenter.y + .02f);
            PlaceOnY(second, counterCenter + right * (pitch * .5f), counterCenter.y + .02f);

            if (VisualBounds(new[] { first, second }, out var stackBounds))
            {
                var collider = stackRoot.gameObject.AddComponent<BoxCollider>();
                collider.center = stackRoot.InverseTransformPoint(stackBounds.center);
                collider.size = stackBounds.size;
            }
            foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            foreach (var collider in source.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            return stackRoot;
        }

        GameObject CreateModel(string resourcePath, string objectName, Vector3 position,
            Quaternion rotation, float scale)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (!prefab)
            {
                Debug.LogWarning("[Monkey Care] Missing farm model: Resources/" + resourcePath);
                return null;
            }

            var instance = Instantiate(prefab, position, rotation, transform);
            instance.name = objectName;
            instance.transform.localScale = Vector3.one * scale;
            return instance;
        }

        void StackBananaCrates(GameObject targetBarn)
        {
            Transform source = Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "banana box");
            if (!source)
            {
                Debug.LogWarning("[Monkey Care] Could not find the scene artwork named 'banana box'.");
                return;
            }
            if (!VisualBounds(source.gameObject, out var sourceBounds))
            {
                Debug.LogWarning("[Monkey Care] The banana box has no visible renderer.");
                return;
            }

            var stackRoot = new GameObject("Banana Crate Stack");
            stackRoot.transform.SetParent(transform, false);

            Vector3 forward = targetBarn.transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (!VisualBounds(targetBarn, out var barnBounds))
                barnBounds = new Bounds(targetBarn.transform.position, Vector3.one * 4f);

            Vector3 stackCenter = barnBounds.center - forward * barnBounds.extents.z * .28f;
            Vector3 ground = FindGround(new Vector3(stackCenter.x,
                barnBounds.center.y, stackCenter.z));
            stackRoot.transform.SetPositionAndRotation(ground, targetBarn.transform.rotation);

            float sourceYaw = source.eulerAngles.y;
            var bottom = new List<GameObject>(3);
            for (int i = 0; i < 3; i++)
                bottom.Add(CreateCrateCopy(source, stackRoot.transform, sourceYaw, "bottom-" + (i + 1)));

            float width = Mathf.Max(.2f, VisualBounds(bottom[0]).size.x);
            float gap = Mathf.Max(.045f, width * .08f);
            float pitch = width + gap;

            float floorY = ground.y + .02f;
            for (int i = 0; i < bottom.Count; i++)
            {
                PlaceOnY(bottom[i], stackCenter + right * ((i - 1) * pitch), floorY);
            }

            float topY = bottom.Max(b => VisualBounds(b).max.y) + .025f;
            var top = new List<GameObject>(2);
            for (int i = 0; i < 2; i++)
                top.Add(CreateCrateCopy(source, stackRoot.transform, sourceYaw, "top-" + (i + 1)));
            for (int i = 0; i < top.Count; i++)
            {
                Vector3 p = stackCenter + right * ((i - .5f) * pitch) - forward * .035f;
                PlaceOnY(top[i], p, topY);
            }

            var crates = bottom.Concat(top).ToList();
            if (VisualBounds(crates, out var stackBounds))
            {
                var collider = stackRoot.AddComponent<BoxCollider>();
                collider.center = stackRoot.transform.InverseTransformPoint(stackBounds.center);
                collider.size = stackBounds.size;
            }

            foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            foreach (var collider in source.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            Debug.Log("[Monkey Care] Stacked 5 banana crates in the open barn.");
        }

        CarePickupMarker CreatePickupMarker(GameObject stack)
        {
            var existing = stack.GetComponentInChildren<CarePickupMarker>(true);
            if (existing) return existing;
            if (!VisualBounds(stack, out var bounds)) return null;
            // Keep the callout above the fruit while leaving it inside a normal
            // player's view when they stand directly in front of the stall.
            Vector3 markerPosition = bounds.center + Vector3.up * (bounds.extents.y - .16f) +
                                     stack.transform.forward * .75f;
            return CarePickupMarker.Create(stack.transform, markerPosition);
        }

        static void ClearVegetationInside(Bounds clearing)
        {
            clearing.Expand(new Vector3(.65f, .2f, .65f));
            var roots = new HashSet<Transform>();
            foreach (var transform in FindObjectsByType<Transform>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!IsVegetationName(transform.name)) continue;
                Transform root = transform;
                for (var cursor = transform.parent; cursor && cursor.parent; cursor = cursor.parent)
                {
                    if (!cursor.parent.name.StartsWith("03 Vegetation")) continue;
                    root = cursor;
                    break;
                }
                roots.Add(root);
            }

            int cleared = 0;
            foreach (var root in roots)
            {
                if (!root || !VisualBounds(root.gameObject, out var bounds)) continue;
                if (!bounds.Intersects(clearing)) continue;
                root.gameObject.SetActive(false);
                cleared++;
            }
            if (cleared > 0)
                Debug.Log("[Monkey Care] Cleared " + cleared +
                          " vegetation objects from the barn footprint.");
        }

        static bool IsVegetationName(string value) =>
            value.StartsWith("Tree_") || value.StartsWith("BananaTree") ||
            value.StartsWith("Bush_");

        static GameObject FindNamed(string objectName)
        {
            Transform found = FindObjectsByType<Transform>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(item => item.name == objectName);
            return found ? found.gameObject : null;
        }

        GameObject CreateCrateCopy(Transform source, Transform parent, float sourceYaw, string suffix)
        {
            var copy = Instantiate(source.gameObject, parent, false);
            copy.name = "Banana Crate " + suffix;
            copy.transform.SetParent(parent, false);
            copy.transform.localScale = source.lossyScale;
            copy.transform.rotation = Quaternion.Euler(0f, sourceYaw, 0f);
            foreach (var collider in copy.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (var body in copy.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
            foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;
            foreach (var child in copy.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = 0;
            foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = true;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return copy;
        }

        void PlaceOnY(GameObject visual, Vector3 horizontalPosition, float bottomY)
        {
            visual.transform.position = new Vector3(horizontalPosition.x, 0f, horizontalPosition.z);
            if (!VisualBounds(visual, out var bounds)) return;
            visual.transform.position += new Vector3(horizontalPosition.x - bounds.center.x, 0f,
                horizontalPosition.z - bounds.center.z);
            VisualBounds(visual, out bounds);
            visual.transform.position += Vector3.up * (bottomY - bounds.min.y);
        }

        static void AlignBottomToGround(GameObject root, float groundY)
        {
            if (!VisualBounds(root, out var bounds)) return;
            root.transform.position += Vector3.up * (groundY - bounds.min.y);
        }

        static void SetBoundsCenterXZ(GameObject root, Vector3 target)
        {
            if (!VisualBounds(root, out var bounds)) return;
            Vector3 center = bounds.center;
            root.transform.position += new Vector3(target.x - center.x, 0f, target.z - center.z);
        }

        void AddStaticMeshColliders(GameObject root)
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh || filter.GetComponent<Collider>()) continue;
                var meshCollider = filter.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = filter.sharedMesh;
                meshCollider.convex = false;
            }
        }

        // Invisible solid box filling the stall's footprint so the player
        // cannot walk into or straight through the stand area.
        void AddStallBlocker(GameObject stand)
        {
            if (!stand || stand.transform.Find("Stall Blocker")) return;
            if (!VisualBounds(stand, out var bounds)) return;
            var blocker = new GameObject("Stall Blocker");
            blocker.transform.SetParent(stand.transform, false);
            var box = blocker.AddComponent<BoxCollider>();
            float height = Mathf.Max(2.2f, bounds.size.y);
            box.center = new Vector3(bounds.center.x,
                bounds.min.y + height * .5f, bounds.center.z);
            box.size = new Vector3(bounds.size.x + .5f, height, bounds.size.z + .4f);
        }

        Vector3 FindGround(Vector3 near, Transform ignoredRoot = null)
        {
            Vector3 origin = new(near.x, playerCamera.transform.position.y + 120f, near.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 240f,
                ~((1 << 2) | (1 << 5)), QueryTriggerInteraction.Ignore);
            foreach (var hit in hits
                         .Where(h => h.normal.y >= .45f &&
                                     !h.transform.IsChildOf(playerCamera.transform.root) &&
                                     (!ignoredRoot || !h.transform.IsChildOf(ignoredRoot)))
                         .OrderBy(h => h.point.y))
            {
                return hit.point;
            }

            var terrain = Terrain.activeTerrain;
            if (terrain && terrain.terrainData)
            {
                Vector3 terrainPosition = terrain.transform.position;
                Vector3 local = near - terrainPosition;
                Vector3 size = terrain.terrainData.size;
                if (local.x >= 0f && local.z >= 0f && local.x <= size.x && local.z <= size.z)
                {
                    float y = terrain.SampleHeight(near) + terrainPosition.y;
                    return new Vector3(near.x, y, near.z);
                }
            }
            return new Vector3(near.x, playerCamera.transform.position.y - 1.6f, near.z);
        }

        static bool VisualBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bounds = default;
            bool found = false;
            foreach (var renderer in renderers)
            {
                if (!renderer) continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        static bool VisualBounds(IReadOnlyList<GameObject> roots, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (var root in roots)
            {
                if (!VisualBounds(root, out var itemBounds)) continue;
                if (!found)
                {
                    bounds = itemBounds;
                    found = true;
                }
                else bounds.Encapsulate(itemBounds);
            }
            return found;
        }

        static Bounds VisualBounds(GameObject root) =>
            VisualBounds(root, out var bounds) ? bounds : default;
    }

    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class CarePickupMarker : MonoBehaviour
    {
        public const float PickupRange = 3.2f;
        [SerializeField] Vector3 anchor;
        [SerializeField] float phase;
        [SerializeField] bool anchorReady;
        Camera playerCamera;
        Image arrow, keyBadge;
        Text label, keyLabel;
        public bool PlayerInRange { get; private set; }

        public static CarePickupMarker Create(Transform parent, Vector3 worldPosition)
        {
            var root = new GameObject("Banana Pickup Marker");
            root.transform.SetParent(parent, true);
            root.transform.position = worldPosition;
            var marker = root.AddComponent<CarePickupMarker>();
            marker.Initialize(worldPosition);
            return marker;
        }

        void OnEnable()
        {
            if (!anchorReady) Initialize(transform.position);
            else EnsureVisuals();
        }

        void Initialize(Vector3 worldPosition)
        {
            anchor = worldPosition;
            phase = Mathf.Abs(worldPosition.x * 1.73f + worldPosition.z * .91f) % Mathf.PI * 2f;
            anchorReady = true;
            EnsureVisuals();
        }

        void EnsureVisuals()
        {
            var existingCanvas = transform.Find("Pickup Marker Canvas");
            if (existingCanvas)
            {
                arrow = existingCanvas.Find("Pickup down arrow")?.GetComponent<Image>();
                label = existingCanvas.Find("Pickup text")?.GetComponent<Text>();
                keyBadge = existingCanvas.Find("Pickup key")?.GetComponent<Image>();
                keyLabel = existingCanvas.Find("Pickup key label")?.GetComponent<Text>();
                if (arrow && label && keyBadge && keyLabel)
                {
                    label.font = CareFont.LoadReadable();
                    label.fontSize = 44;
                    label.rectTransform.sizeDelta = new Vector2(220, 74);
                    keyLabel.font = CareFont.LoadReadable();
                    keyLabel.fontSize = 48;
                    keyBadge.rectTransform.sizeDelta = new Vector2(88, 88);
                    keyBadge.rectTransform.anchoredPosition = new Vector2(-122, -36);
                    keyLabel.rectTransform.sizeDelta = new Vector2(84, 84);
                    return;
                }
                if (Application.isPlaying) Destroy(existingCanvas.gameObject);
                else DestroyImmediate(existingCanvas.gameObject);
            }
            var canvas = CareUi.Canvas("Pickup Marker Canvas", transform,
                Vector3.zero, 1.10f, new Vector2(560, 220));
            var shadow = CareUiSkin.AddSliced(canvas, "Pickup label shadow", CareUiSkin.Panel,
                new Vector2(348, 94), new Vector2(4, -40), new Color(.02f, .08f, .15f, .36f));
            shadow.transform.SetAsFirstSibling();
            CareUiSkin.AddSliced(canvas, "Pickup label", CareUiSkin.Panel,
                new Vector2(340, 88), new Vector2(0, -36), new Color(1f, .96f, .82f, 1f));

            arrow = CareUiSkin.Add(canvas, "Pickup down arrow", CareUiSkin.DownArrow,
                new Vector2(112, 112), new Vector2(0, 48), new Color(1f, .71f, .12f, 1f));
            arrow.preserveAspect = true;

            var labelRect = CareUi.Rect("Pickup text", canvas, new Vector2(190, 70), new Vector2(38, -36));
            label = labelRect.gameObject.AddComponent<Text>();
            label.font = CareFont.LoadReadable();
            label.fontSize = 44;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(.04f, .13f, .25f, 1f);
            label.raycastTarget = false;
            label.text = "BANANA SUPPLY";

            keyBadge = CareUiSkin.Add(canvas, "Pickup key", CareUiSkin.RoundButton,
                new Vector2(88, 88), new Vector2(-122, -36),
                new Color(.25f, .68f, .98f, 1f));
            keyLabel = CareRoundView.Label("Pickup key label", keyBadge.transform, "A",
                new Vector2(84, 84), Vector2.zero, 48,
                TextAnchor.MiddleCenter, Color.white);
            keyBadge.gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            if (!playerCamera) playerCamera = MonkeyCareSession.PlayerCamera;
            if (!playerCamera)
            {
                var player = FindFirstObjectByType<DesktopPlayerController>(FindObjectsInactive.Include);
                if (player) playerCamera = player.GetComponentInChildren<Camera>(true);
            }
            if (!playerCamera) return;
            var session = MonkeyCareSession.Instance;
            CareRound round = session ? session.Round : null;
            Collider collider = GetComponentInParent<Collider>();
            Bounds pickupBounds = collider ? collider.bounds : new Bounds(anchor, Vector3.one * 1.2f);
            PlayerInRange = pickupBounds.SqrDistance(playerCamera.transform.position) <= PickupRange * PickupRange;
            int bananas = round != null ? round.Bananas : CareRound.StartingBananas;
            bool needsBananas = bananas < CareRound.StartingBananas;
            keyLabel.text = CareInput.ConfirmLabel;
            // Keep the A badge visible so the crate reads as an interactable
            // supply point even before the first banana is thrown.
            keyBadge.gameObject.SetActive(true);
            keyBadge.color = needsBananas
                ? new Color(.25f, .68f, .98f, 1f)
                : new Color(.42f, .51f, .57f, .92f);
            if (needsBananas && PlayerInRange)
            {
                label.text = bananas <= 0 ? "PICK UP" : "RESTOCK";
                label.rectTransform.sizeDelta = new Vector2(220, 74);
                label.rectTransform.anchoredPosition = new Vector2(46, -36);
                label.color = new Color(.06f, .34f, .18f, 1f);
                arrow.color = new Color(.40f, .94f, .53f, 1f);
            }
            else if (needsBananas)
            {
                label.text = "PRESS " + CareInput.ConfirmLabel;
                label.rectTransform.sizeDelta = new Vector2(220, 74);
                label.rectTransform.anchoredPosition = new Vector2(46, -36);
                label.color = new Color(.20f, .25f, .30f, 1f);
                arrow.color = new Color(1f, .71f, .12f, 1f);
            }
            else
            {
                label.text = PlayerInRange ? "FULL" : "BANANA SUPPLY";
                label.rectTransform.sizeDelta = new Vector2(220, 74);
                label.rectTransform.anchoredPosition = new Vector2(46, -36);
                label.color = new Color(.04f, .13f, .25f, 1f);
                arrow.color = new Color(1f, .71f, .12f, 1f);
            }
            float bob = Mathf.Sin(Time.unscaledTime * 2.5f + phase) * .07f;
            transform.position = anchor + Vector3.up * bob;
            transform.rotation = playerCamera.transform.rotation;
            float pulse = needsBananas && PlayerInRange
                ? 1f + Mathf.Sin(Time.unscaledTime * 6f) * .055f
                : 1f;
            arrow.rectTransform.localScale = Vector3.one * pulse;
        }
    }
}
