using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ADayAtTheZoo.Habitat
{
    /// <summary>
    /// Scene-level coordinator for the runtime habitat effects. It keeps the
    /// environment systems independent while giving the scene one tuning point.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    public sealed class MonkeyHabitatEffects : MonoBehaviour
    {
        public Vector3 gardenCenter = new Vector3(-4380f, 0f, 47544f);
        public Vector3 windDirection = new Vector3(0.83f, 0f, 0.56f);

        [Header("Garden")]
        [Range(4, 20)] public int gardenColumns = 12;
        [Range(3, 16)] public int gardenRows = 8;
        [Range(0.75f, 2.5f)] public float gardenSpacing = 1.35f;
        public bool swayGeneratedTrees = true;

        private static readonly string SceneName = "Monekey Mountain";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureSceneEffects()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != SceneName)
                return;
            if (FindFirstObjectByType<MonkeyHabitatEffects>() != null)
                return;

            var root = new GameObject("Monkey Habitat Effects");
            root.AddComponent<MonkeyHabitatEffects>();
        }

        private void Awake()
        {
            Build();
        }

        public void Build()
        {
            List<Transform> trees = FindGeneratedTrees();

            if (trees.Count > 0)
            {
                Vector3[] treeBases = new Vector3[trees.Count];
                for (int i = 0; i < trees.Count; i++)
                {
                    treeBases[i] = trees[i].position;
                    if (!swayGeneratedTrees)
                        continue;
                    MonkeyWindSway sway = trees[i].GetComponent<MonkeyWindSway>();
                    if (sway == null)
                        sway = trees[i].gameObject.AddComponent<MonkeyWindSway>();
                    sway.Configure(windDirection, 1.25f + i * 0.12f, 0.42f + i * 0.035f, i * 1.37f);
                }

                CreateChild<MonkeyLivingGarden>("Living Garden and Wind Seeding")
                    .Configure(gardenCenter, treeBases, windDirection);
            }
            else
            {
                CreateChild<MonkeyLivingGarden>("Living Garden and Wind Seeding")
                    .Configure(gardenCenter, new Vector3[0], windDirection);
            }

            MonkeyLivingGarden garden = GetComponentInChildren<MonkeyLivingGarden>();
            if (garden != null)
            {
                garden.columns = gardenColumns;
                garden.rows = gardenRows;
                garden.spacing = gardenSpacing;
                garden.windDirection = windDirection;
            }
        }

        private T CreateChild<T>(string childName) where T : Component
        {
            Transform existing = transform.Find(childName);
            if (existing != null && existing.TryGetComponent(out T component))
                return component;

            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            return child.AddComponent<T>();
        }

        private static List<Transform> FindGeneratedTrees()
        {
            var result = new List<Transform>();
            Transform[] transforms = FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (Transform item in transforms)
            {
                if (item.name.StartsWith("samor_Tree_") &&
                    item.GetComponent<MeshFilter>() != null)
                    result.Add(item);
            }
            return result;
        }

        private void OnValidate()
        {
            gardenColumns = Mathf.Clamp(gardenColumns, 4, 20);
            gardenRows = Mathf.Clamp(gardenRows, 3, 16);
            gardenSpacing = Mathf.Clamp(gardenSpacing, 0.75f, 2.5f);
            if (windDirection.sqrMagnitude < 0.001f)
                windDirection = Vector3.forward;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(
                gardenCenter + Vector3.up,
                windDirection.normalized * 100f);
        }
    }
}
