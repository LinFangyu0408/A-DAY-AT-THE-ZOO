using UnityEngine;

namespace ADayAtTheZoo.PC
{
    [CreateAssetMenu(menuName = "Zoo/PC Menu Assets")]
    public sealed class ZooMenuAssets : ScriptableObject
    {
        public Texture2D cover, paper, journalTitle, journalCard, wood, levelPhoto;
        public Texture2D titleLogo, leafButton, linenButton, monkeyPortrait, cratePortrait;
        public GameObject bookPrefab;
        public Font headingFont, bodyFont, noteFont;
        public ZooCollectionEntry[] animals, facilities;
        public AudioClip pageTurn;
        public string tutorialScene = "Assets/ZooPCMenu/Scenes/PC_TutorialDay1.unity";
        public string menuScene = "Assets/ZooPCMenu/Scenes/PC_MainMenu.unity";
    }
}
