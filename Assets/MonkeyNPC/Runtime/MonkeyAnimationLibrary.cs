using System;
using System.Collections.Generic;
using UnityEngine;

namespace ADayAtTheZoo.Monkeys
{
    [CreateAssetMenu(menuName = "A Day at the Zoo/Monkey Animation Library")]
    public sealed class MonkeyAnimationLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public MonkeyAction action;
            public AnimationClip clip;
            [Tooltip("Original asset path or Mixamo animation URL.")]
            public string source;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        public IReadOnlyList<Entry> Entries => entries;

        public AnimationClip GetClip(MonkeyAction action)
        {
            foreach (Entry entry in entries)
                if (entry.action == action)
                    return entry.clip;
            return null;
        }

        public bool HasAction(MonkeyAction action) => GetClip(action) != null;

        public IEnumerable<MonkeyAction> MissingActions()
        {
            foreach (MonkeyAction action in Enum.GetValues(typeof(MonkeyAction)))
                if (!HasAction(action))
                    yield return action;
        }

#if UNITY_EDITOR
        public void SetEntries(Entry[] value)
        {
            entries = value ?? Array.Empty<Entry>();
        }
#endif
    }
}
