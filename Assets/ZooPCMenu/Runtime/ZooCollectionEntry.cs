using System;
using UnityEngine;

namespace ADayAtTheZoo.PC
{
    [Serializable]
    public sealed class ZooCollectionEntry
    {
        public string id;
        public string displayName;
        public string subtitle;
        public Texture2D portrait;
        public bool unlocked;
        public int discoveredDay=1;
        public bool IsUnlocked => unlocked || ZooPCFlow.CollectionUnlocked(id);
    }
}
