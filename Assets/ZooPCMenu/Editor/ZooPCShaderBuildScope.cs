using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ADayAtTheZoo.PC.Editor
{
    /// <summary>
    /// This project uses the built-in renderer. Some supplied Shader Graphs also
    /// contain unused URP targets with billions of variants in Unity 6000.4.
    /// Build the existing built-in target, then restore every source byte.
    /// Backups live in Library so an interrupted build can also be recovered.
    /// </summary>
    public sealed class ZooPCShaderBuildScope : IDisposable
    {
        const string Backup="Library/ZooPCShaderBuildBackup";
        readonly Dictionary<string,byte[]> originals=new();
        RenderPipelineGlobalSettings urpSettings;
        bool builtinBuild;
#if UNITY_6000_4_OR_NEWER
        UnityEditor.Shaders.ShaderBuildSettings shaderBuildSettings;
#endif
        public ZooPCShaderBuildScope()
        {
            if(GraphicsSettings.defaultRenderPipeline!=null)return;
            RestoreInterrupted();
            builtinBuild=true;
            urpSettings=EditorGraphicsSettings.GetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>();
            var candidates=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)&&p.EndsWith(".shadergraph",StringComparison.Ordinal));
            var builtin=new Regex("\\\"m_Type\\\"\\s*:\\s*\\\"UnityEditor\\.Rendering\\.BuiltIn\\.ShaderGraph\\.BuiltInTarget\\\"\\s*,\\s*\\\"m_ObjectId\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
            var targets=new Regex("\\\"m_ActiveTargets\\\"\\s*:\\s*\\[[^\\]]*\\]");
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach(string path in candidates)
                {
                    byte[] bytes=File.ReadAllBytes(path);string source=Encoding.UTF8.GetString(bytes);var match=builtin.Match(source);var targetList=targets.Match(source);
                    if(!match.Success||!targetList.Success||!targetList.Value.Contains(match.Groups[1].Value))continue;
                    if(Regex.Matches(targetList.Value,"m_Id").Count<2)continue;
                    string replacement="\"m_ActiveTargets\": [ { \"m_Id\": \""+match.Groups[1].Value+"\" } ]";
                    originals.Add(path,bytes);string backup=Path.Combine(Backup,path);Directory.CreateDirectory(Path.GetDirectoryName(backup));File.WriteAllBytes(backup,bytes);
                    File.WriteAllText(path,source.Substring(0,targetList.Index)+replacement+source.Substring(targetList.Index+targetList.Length),new UTF8Encoding(false));
                    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                }
            }
            catch { RestoreBytes();throw; }
            finally {AssetDatabase.StopAssetEditing();}
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            // The inactive URP global-settings asset references its default shaders.
            // Exclude that unused resource tree only while building the built-in PC player.
            EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>(null);
#if UNITY_6000_4_OR_NEWER
            // Package Resources can still pull in an unused URP shader. Strip URP-only
            // declarations before variant enumeration, not in a late shader callback.
            shaderBuildSettings=EditorGraphicsSettings.GetShaderBuildSettings();
            var overrides=shaderBuildSettings.GetKeywordDeclarationOverridesCopy().ToList();
            foreach(string group in new[]{
                "_MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN",
                "_ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS", "EVALUATE_SH_MIXED EVALUATE_SH_VERTEX",
                "_ADDITIONAL_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING", "_REFLECTION_PROBE_BOX_PROJECTION",
                "_REFLECTION_PROBE_ATLAS", "_SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH",
                "_SCREEN_SPACE_OCCLUSION", "_SCREEN_SPACE_IRRADIANCE", "_DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3",
                "_LIGHT_COOKIES", "_LIGHT_LAYERS", "_CLUSTER_LIGHT_LOOP"})
            {
                overrides.Add(new UnityEditor.Shaders.ShaderBuildSettings.KeywordDeclarationOverride {
                    keywords=new[]{new UnityEditor.Shaders.ShaderBuildSettings.KeywordOverrideInfo("_",true)}.Concat(group.Split(' ').Select(k=>new UnityEditor.Shaders.ShaderBuildSettings.KeywordOverrideInfo(k,false))).ToArray(),
                    variantGenerationMode=UnityEditor.Shaders.ShaderBuildSettings.ShaderVariantGenerationMode.Default
                });
            }
            EditorGraphicsSettings.SetShaderBuildSettings(new UnityEditor.Shaders.ShaderBuildSettings {KeywordDeclarationOverrides=overrides.ToArray()});
#endif
            Debug.Log("[Zoo PC Build] Selected existing built-in targets in "+originals.Count+" graphs for this build only.");
        }
        public void Dispose()
        {
            if(builtinBuild)EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>(urpSettings);
#if UNITY_6000_4_OR_NEWER
            if(builtinBuild)EditorGraphicsSettings.SetShaderBuildSettings(shaderBuildSettings);
#endif
            AssetDatabase.StartAssetEditing();
            try{RestoreBytes();}finally{AssetDatabase.StopAssetEditing();}
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            DeleteBackup();
            Debug.Log("[Zoo PC Build] Original shader sources restored byte-for-byte.");
        }
        void RestoreBytes(){foreach(var item in originals){File.WriteAllBytes(item.Key,item.Value);AssetDatabase.ImportAsset(item.Key,ImportAssetOptions.ForceUpdate);}}
        [MenuItem("Zoo/PC Menu/Recover interrupted PC shader build")]
        public static void RestoreInterrupted()
        {
            if(!Directory.Exists(Backup))return;
            foreach(string path in Directory.GetFiles(Backup,"*.shadergraph",SearchOption.AllDirectories))
            {
                string relative=path.Substring(Backup.Length+1).Replace('\\','/');
                if(!relative.StartsWith("Assets/",StringComparison.Ordinal))throw new InvalidOperationException("Unexpected backup path");
                File.WriteAllBytes(relative,File.ReadAllBytes(path));AssetDatabase.ImportAsset(relative,ImportAssetOptions.ForceUpdate);
            }
            DeleteBackup();
        }
        static void DeleteBackup()
        {
            string folder=Path.GetFullPath(Backup),library=Path.GetFullPath("Library")+Path.DirectorySeparatorChar;
            if(!folder.StartsWith(library,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Backup is outside the project Library");
            if(Directory.Exists(folder))Directory.Delete(folder,true);
        }
    }
}
