using UnityEngine;
namespace ADayAtTheZoo.Monkeys.Care
{
    public sealed class CareTeleportPoint : MonoBehaviour
    {
        public MonkeyCareNpc owner;
        public Vector3 Landing => transform.position - Vector3.up * .025f;
        public void Highlight(bool value)
        {
            var r = GetComponent<Renderer>(); if (!r) return;
            var block = new MaterialPropertyBlock(); r.GetPropertyBlock(block);
            block.SetColor("_Color", value ? Color.yellow : new Color(.2f, .9f, .9f, .75f)); r.SetPropertyBlock(block);
        }
    }
}
