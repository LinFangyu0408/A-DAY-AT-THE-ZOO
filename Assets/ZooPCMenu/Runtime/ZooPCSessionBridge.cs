using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using ADayAtTheZoo.Monkeys.Care;

namespace ADayAtTheZoo.PC
{
    // This component is attached only to the dedicated PC tutorial scene.
    // The shared session and the team's VR scenes remain unchanged.
    [DefaultExecutionOrder(-400)]
    public sealed class ZooPCSessionBridge : MonoBehaviour
    {
        MonkeyCareSession session;
        DesktopPlayerController desktop;
        bool saved,wasPaused,wasLocked;
        IEnumerator Start()
        {
            if(!ZooPCFlow.Instance)
            {
                var flow=new GameObject("Zoo PC navigation").AddComponent<ZooPCFlow>();
                yield return null;
                // Directly playing the tutorial scene still gets pause / return navigation.
                flow.EnterGameplay();
            }
            session=FindFirstObjectByType<MonkeyCareSession>();desktop=FindFirstObjectByType<DesktopPlayerController>();
            while(session&&session.RoundView==null)yield return null;
            foreach(var result in FindObjectsByType<CareResultButton>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(result.restart){result.gameObject.SetActive(true);continue;}
                var button=result.GetComponent<Button>();
                if(button){button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>ZooPCFlow.Instance.ReturnToJournal());}
                foreach(var label in result.GetComponentsInChildren<Text>())if(label.name.EndsWith("caption"))label.text="WORK LOG";
                var collider=result.GetComponent<Collider>();if(collider)collider.enabled=false;
            }
        }
        void Update()
        {
            var flow=ZooPCFlow.Instance;if(!flow||!session)return;
            if(flow.Paused!=wasPaused)
            {
                if(desktop){if(flow.Paused){wasLocked=desktop.inputLocked;desktop.inputLocked=true;}else desktop.inputLocked=wasLocked;}
                wasPaused=flow.Paused;
            }
            if(flow.Paused||flow.IsLoading)return;
            if(session.Round!=null&&session.Round.Frozen)
            {
                if(!saved){ZooPCFlow.SaveDay(session.Round.Stars,session.Round.FeedingTotal);saved=true;}
                var keyboard=Keyboard.current;var pad=Gamepad.current;
                if((keyboard!=null&&(keyboard.spaceKey.wasPressedThisFrame||keyboard.aKey.wasPressedThisFrame))||(pad!=null&&pad.buttonSouth.wasPressedThisFrame))flow.ReturnToJournal();
            }
            else saved=false;
        }
    }
}
