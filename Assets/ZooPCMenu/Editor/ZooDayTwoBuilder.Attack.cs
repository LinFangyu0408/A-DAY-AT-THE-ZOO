using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ADayAtTheZoo.PC.Editor
{
    public static partial class ZooDayTwoBuilder
    {
        public static void ValidateAttack() { PrepareAttack(); ZooDayTwoValidation.Attack(); }
        public static void ProbeAttack() { PrepareAttack(); ZooDayTwoValidation.AttackPose(); }
        [MenuItem("Zoo/Day Two/Prepare patience attack assets")]
        public static void PrepareAttack()
        {
            const string directory = "Assets/ZooPCMenu/Resources/ZooPCMenu";
            Directory.CreateDirectory(directory); AssetDatabase.Refresh();
            string path = directory + "/DayTwoAttack.asset";
            var assets = AssetDatabase.LoadAssetAtPath<ZooMonkeyAttackAssets>(path);
            if (!assets) { assets = ScriptableObject.CreateInstance<ZooMonkeyAttackAssets>(); AssetDatabase.CreateAsset(assets, path); }
            const string animations = "Assets/Suriyun/Animations/Pspsps Animations/Character Animations/Anim_Chibi@";
            assets.attack = AssetDatabase.LoadAllAssetsAtPath(animations + "ATK1.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
            assets.angry = AssetDatabase.LoadAllAssetsAtPath(animations + "Angry.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
            assets.angryFace = AssetDatabase.LoadAssetAtPath<Material>("Assets/Suriyun/Pspsps/Materials/Emotions/M_Chibi_Emo_16.mat");
            EditorUtility.SetDirty(assets); AssetDatabase.SaveAssets();
            Debug.Log("[Zoo Attack] Prepared clips: " + assets.attack.length + " / " + assets.angry.length);
        }
    }
}
