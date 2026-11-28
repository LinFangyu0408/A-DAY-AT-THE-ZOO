using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace ADayAtTheZoo.PC.Editor
{
    public static partial class ZooDayTwoBuilder
    {
        static void BuildNavigation(ZooDayTwoSession session)
        {
            var navigation = session.gameObject.AddComponent<ZooDayTwoNavigation>();
            session.navigation = navigation;
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(navigation.playArea, ~((1 << 2) | (1 << 5)), NavMeshCollectGeometry.RenderMeshes, 0, new List<NavMeshBuildMarkup>(), sources);
            sources.RemoveAll(s => s.component && (!s.component.gameObject.activeInHierarchy || s.component.GetComponentInParent<ZooDayTwoNpc>() || s.component.GetComponentInParent<DesktopPlayerController>()));
            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i]; var renderer = source.component as Renderer;
                if (renderer && renderer.sharedMaterials.Any(m => m && (m.name.ToLowerInvariant().Contains("water") || m.name.ToLowerInvariant().Contains("ocean"))))
                { source.area = 1; sources[i] = source; }
            }
            var settings = NavMesh.GetSettingsByIndex(0);
            navigation.agentTypeID = settings.agentTypeID;
            settings.agentRadius = .48f; settings.agentHeight = 1.5f; settings.agentClimb = .22f; settings.agentSlope = 35;
            settings.overrideVoxelSize = true; settings.voxelSize = .09f;
            settings.minRegionArea = 1;
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, navigation.playArea, Vector3.zero, Quaternion.identity);
            if (!data) throw new System.InvalidOperationException("Day 02 walkable ground could not be baked.");
            string path = Root + "Generated/DayTwoWalkableGround.asset";
            var saved = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (saved) { EditorUtility.CopySerialized(data, saved); Object.DestroyImmediate(data); EditorUtility.SetDirty(saved); }
            else { data.name = "Day Two walkable ground"; AssetDatabase.CreateAsset(data, path); saved = data; }
            navigation.data = saved;
            Debug.Log("[Zoo Day 2] Baked broad roaming area from " + sources.Count + " environment meshes.");
        }
    }
}

