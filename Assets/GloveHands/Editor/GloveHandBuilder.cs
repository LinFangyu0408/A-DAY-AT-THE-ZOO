using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.XR;
using Object = UnityEngine.Object;

namespace ADayAtTheZoo.Hands.Editor
{
    public static class GloveHandBuilder
    {
        public const string Root = "Assets/GloveHands";
        public const float Scale = 0.23f;
        private sealed class Finger
        {
            public string name;
            public Vector3[] points;
            public float[] grip, point;
            public Finger(string n, Vector3[] p, float[] g, float[] q) { name=n; points=p; grip=g; point=q; }
        }
        // Joint locations in the supplied FBX's original modelling coordinates.
        private static readonly Finger[] Fingers = {
            new Finger("Index", new[]{new Vector3(.078f,-.105f,.065f),new Vector3(.085f,-.254f,.041f),new Vector3(.100f,-.326f,.023f),new Vector3(.11f,-.409f,.001f)},new[]{58f,56f,28f},new[]{-5f,-10f,-8f}),
            new Finger("Middle",new[]{new Vector3(-.032f,-.132f,.072f),new Vector3(-.032f,-.29f,.045f),new Vector3(-.03f,-.382f,.026f),new Vector3(-.027f,-.464f,-.006f)},new[]{56f,58f,26f},new[]{62f,85f,45f}),
            new Finger("Ring",new[]{new Vector3(-.13f,-.10f,.065f),new Vector3(-.143f,-.247f,.041f),new Vector3(-.145f,-.350f,.025f),new Vector3(-.147f,-.427f,.002f)},new[]{60f,58f,28f},new[]{64f,87f,43f}),
            new Finger("Little",new[]{new Vector3(-.222f,-.06f,.030f),new Vector3(-.235f,-.166f,.029f),new Vector3(-.242f,-.22f,.012f),new Vector3(-.25f,-.304f,-.005f)},new[]{64f,56f,28f},new[]{68f,85f,40f}),
            new Finger("Thumb",new[]{new Vector3(.12f,.17f,.025f),new Vector3(.194f,.059f,-.011f),new Vector3(.242f,-.011f,-.025f),new Vector3(.291f,-.089f,-.051f)},new[]{8f,10f,15f},new[]{3f,8f,12f})
        };

        [MenuItem("Tools/Glove Hands/Rebuild hand poses")]
        public static void Build()
        {
            foreach (string folder in new[]{"Generated", "Animations", "Materials", "Prefabs", "Demo"}) Directory.CreateDirectory(Root+"/"+folder);
            AssetDatabase.Refresh();
            var importer=(ModelImporter)AssetImporter.GetAtPath(Root+"/Source/Glove.fbx");
            if (!importer.isReadable) { importer.isReadable=true; importer.SaveAndReimport(); }
            BuildHand(false); BuildHand(true);
            AssetDatabase.SaveAssets();
            Debug.Log("[Glove] Built left/right rigs, idle, grip, point and controllers.");
        }

        private static Vector3 Canonical(Vector3 p, bool left)
        {
            return new Vector3((p.x+.04f)*(left?1f:-1f),p.z-.08f,.30f-p.y)*Scale;
        }
        private static Vector3 Source(Vector3 importedWorld) => new Vector3(-importedWorld.x,-importedWorld.z,importedWorld.y);

        private static void BuildHand(bool left)
        {
            string side=left?"Left":"Right";
            var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Source/Glove.fbx"));
            var mf=source.GetComponentInChildren<MeshFilter>();
            var mesh=Object.Instantiate(mf.sharedMesh);
            mesh.name="Glove_"+side;
            var original=mesh.vertices.Select(v=>Source(mf.transform.TransformPoint(v))).ToArray();
            mesh.vertices=original.Select(p=>Canonical(p,left)).ToArray();
            mesh.normals=mesh.normals.Select(n=> {
                var w=mf.transform.TransformDirection(n).normalized;
                return new Vector3(left?-w.x:w.x,w.y,w.z);
            }).ToArray();
            if(left) for(int s=0;s<mesh.subMeshCount;s++) {
                var tri=mesh.GetTriangles(s); for(int i=0;i<tri.Length;i+=3) (tri[i+1],tri[i+2])=(tri[i+2],tri[i+1]); mesh.SetTriangles(tri,s);
            }
            mesh.tangents=Array.Empty<Vector4>();
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/GloveBrown.mat");
            if(mat==null) {
                mat=new Material(Shader.Find("Standard")) {name="GloveBrown",color=new Color(.39f,.255f,.17f)};
                mat.SetFloat("_Glossiness",.18f);
                AssetDatabase.CreateAsset(mat,Root+"/Materials/GloveBrown.mat");
            }
            mat.SetFloat("_SpecularHighlights",0);mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_GlossyReflections",0);mat.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            EditorUtility.SetDirty(mat);
            Object.DestroyImmediate(source);

            var root=new GameObject("Glove_"+side);
            var wrist=NewBone("Wrist",root.transform,Vector3.zero);
            var palm=NewBone("Palm",wrist,Vector3.zero);
            var bones=new List<Transform>{palm};
            for(int f=0;f<Fingers.Length;f++) {
                Transform parent=palm;
                for(int j=0;j<3;j++) {
                    var bone=NewBone(Fingers[f].name+"_0"+(j+1),parent,Vector3.zero);
                    bone.position=Canonical(Fingers[f].points[j],left);
                    bones.Add(bone); parent=bone;
                }
                var tip=NewBone(Fingers[f].name+"Tip",parent,Vector3.zero);
                tip.position=Canonical(Fingers[f].points[3],left);
            }
            var socket=NewBone("GripSocket",palm,new Vector3(0f,-.036f,.093f));
            socket.localRotation=Quaternion.Euler(0,left?-20f:20f,90);
            var skin=root.AddComponent<SkinnedMeshRenderer>();
            mesh.boneWeights=original.Select(Weight).ToArray();
            mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*root.transform.localToWorldMatrix).ToArray();
            mesh.RecalculateBounds();
            var savedMesh=Save(mesh,Root+"/Generated/Glove_"+side+".asset");
            skin.sharedMesh=savedMesh; skin.bones=bones.ToArray(); skin.rootBone=wrist;
            skin.sharedMaterial=mat; skin.quality=SkinQuality.Bone4;
            skin.localBounds=new Bounds(new Vector3(0,-.025f,.075f),new Vector3(.25f,.23f,.30f));
            var animator=root.AddComponent<Animator>();
            animator.applyRootMotion=false; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var clips=new AnimationClip[3];
            for(int pose=0;pose<3;pose++) {
                string name=((GlovePose)pose).ToString().ToLowerInvariant();
                var clip=new AnimationClip {name=name,frameRate=30f};
                for(int b=0;b<bones.Count;b++) {
                    Quaternion q=Quaternion.identity;
                    if(b>0 && pose>0) {
                        int f=(b-1)/3,j=(b-1)%3;
                        var finger=Fingers[f];
                        Vector3 axis=Vector3.Cross((Canonical(finger.points[j+1],left)-Canonical(finger.points[j],left)).normalized,Vector3.down).normalized;
                        q=Quaternion.AngleAxis(pose==1?finger.grip[j]:finger.point[j],axis);
                        if(f==4 && j==0) q=Quaternion.Euler(0,(left?-1:1)*(pose==1?60f:65f),(left?-1:1)*(pose==1?12f:10f))*q;
                    }
                    SetRotation(clip,AnimationUtility.CalculateTransformPath(bones[b],root.transform),q);
                }
                clip.EnsureQuaternionContinuity();
                var settings=AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime=true; AnimationUtility.SetAnimationClipSettings(clip,settings);
                clips[pose]=Save(clip,Root+"/Animations/"+side+"_"+name+".anim");
            }
            string controllerPath=Root+"/Generated/Glove_"+side+".controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(controller==null) controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            else {
                foreach(var layer in controller.layers) foreach(var state in layer.stateMachine.states) layer.stateMachine.RemoveState(state.state);
                foreach(var tr in controller.layers[0].stateMachine.anyStateTransitions) controller.layers[0].stateMachine.RemoveAnyStateTransition(tr);
                controller.parameters=Array.Empty<AnimatorControllerParameter>();
            }
            controller.AddParameter("Pose",AnimatorControllerParameterType.Int);
            var sm=controller.layers[0].stateMachine;
            for(int i=0;i<3;i++) {
                var state=sm.AddState(((GlovePose)i).ToString().ToLowerInvariant(),new Vector3(260,80+i*80));
                state.motion=clips[i]; state.writeDefaultValues=false;
                if(i==0) sm.defaultState=state;
                var tr=sm.AddAnyStateTransition(state); tr.hasExitTime=false; tr.hasFixedDuration=true; tr.duration=.18f; tr.canTransitionToSelf=false;
                tr.interruptionSource=TransitionInterruptionSource.SourceThenDestination;
                tr.AddCondition(AnimatorConditionMode.Equals,i,"Pose");
            }
            EditorUtility.SetDirty(controller);
            animator.runtimeAnimatorController=controller;
            root.AddComponent<GlovePoseDriver>();
            var xr=root.AddComponent<GloveXRInput>(); xr.hand=left?XRNode.LeftHand:XRNode.RightHand; xr.enabled=false;
            PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/Glove_"+side+".prefab");
            Object.DestroyImmediate(root);
        }

        private static BoneWeight Weight(Vector3 p)
        {
            int f;
            if(p.x>.135f && p.y>-.125f && p.y<.225f) f=4;
            else if(p.y>-.015f) return new BoneWeight{boneIndex0=0,weight0=1};
            else f=p.x<-.1875f?3:p.x<-.084f?2:p.x<.022f?1:0;
            var pts=Fingers[f].points;
            float root;
            if(f==4) {
                var dir=(pts[1]-pts[0]).normalized;
                root=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.045f,.055f,Vector3.Dot(p-pts[0],dir)));
            } else root=Mathf.SmoothStep(0,1,Mathf.InverseLerp(pts[0].y+.065f,pts[0].y-.025f,p.y));
            int index=1+f*3;
            var weights=new Dictionary<int,float>{{0,1-root}};
            float b1=JointBlend(p,pts[1],pts[1]-pts[0],.035f);
            float b2=JointBlend(p,pts[2],pts[2]-pts[1],.027f);
            weights[index]=root*(1-b1);
            weights[index+1]=root*b1*(1-b2);
            weights[index+2]=root*b1*b2;
            var ranked=weights.OrderByDescending(w=>w.Value).ToArray();
            return new BoneWeight {boneIndex0=ranked[0].Key,weight0=ranked[0].Value,boneIndex1=ranked[1].Key,weight1=ranked[1].Value,boneIndex2=ranked[2].Key,weight2=ranked[2].Value,boneIndex3=ranked[3].Key,weight3=ranked[3].Value};
        }
        private static float JointBlend(Vector3 p,Vector3 joint,Vector3 dir,float width) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(-width,width,Vector3.Dot(p-joint,dir.normalized)));
        private static Transform NewBone(string name,Transform parent,Vector3 position) {
            var t=new GameObject(name).transform; t.SetParent(parent,false); t.localPosition=position; return t;
        }
        private static void SetRotation(AnimationClip clip,string path,Quaternion q) {
            var values=new[]{q.x,q.y,q.z,q.w}; var components=new[]{"x","y","z","w"};
            for(int i=0;i<4;i++) AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+components[i]),AnimationCurve.Constant(0,1,values[i]));
        }
        private static T Save<T>(T asset,string path) where T:Object {
            var existing=AssetDatabase.LoadAssetAtPath<T>(path);
            if(existing==null) {AssetDatabase.CreateAsset(asset,path);return asset;}
            EditorUtility.CopySerialized(asset,existing); EditorUtility.SetDirty(existing);Object.DestroyImmediate(asset);return existing;
        }
    }
}
