using UnityEngine;
using UnityEngine.AI;

namespace ADayAtTheZoo.PC
{
    [DefaultExecutionOrder(-500)]
    public sealed class ZooDayTwoNavigation : MonoBehaviour
    {
        public NavMeshData data;
        public int agentTypeID;
        public Bounds playArea = new Bounds(new Vector3(0, 0, -18), new Vector3(25, 5, 24));
        NavMeshDataInstance instance;
        public bool Ready => instance.valid;
        void OnEnable() { if (data) instance = NavMesh.AddNavMeshData(data); }
        void OnDisable() { if (instance.valid) instance.Remove(); }
        public bool Contains(Vector3 point, float margin = .6f) => point.x > playArea.min.x + margin && point.x < playArea.max.x - margin && point.z > playArea.min.z + margin && point.z < playArea.max.z - margin;
    }
}
