$ErrorActionPreference = 'Stop'

$scene = 'F:\My projects(1)\A DAY AT THE ZOO\Assets\Scenes\Monekey Mountain.unity'
$backup = 'F:\My projects(1)\A DAY AT THE ZOO\Assets\Scenes\Monekey Mountain.backup-2026-11-03-pre-fractal-giants.unity'
$utf8 = [System.Text.UTF8Encoding]::new($false)
$text = [System.IO.File]::ReadAllText($scene, $utf8)

if ($text.Contains('m_Name: samor_Tree_FractalSpire')) {
    Write-Output 'ALREADY_INSTALLED'
    exit 0
}

if (-not (Test-Path -LiteralPath $backup)) {
    Copy-Item -LiteralPath $scene -Destination $backup
}

$trees = @(
    @{
        Name = 'samor_Tree_FractalSpire'
        Go = 1911000001
        Tr = 1911000002
        Mr = 1911000003
        Mf = 1911000004
        Col = 1911000005
        Mesh = '3222316618521584fa9e342e4d440eff'
        Pos = '{x: -4422, y: -17.367, z: 47537}'
        Rot = '{x: -0.6457519, y: 0.28810507, z: 0.28810507, w: 0.6457519}'
        Hint = '{x: -90, y: 0, z: 48.088}'
    },
    @{
        Name = 'samor_Tree_FractalTowering'
        Go = 1911000011
        Tr = 1911000012
        Mr = 1911000013
        Mf = 1911000014
        Col = 1911000015
        Mesh = '7772c97121ccc43499131df4b8de795e'
        Pos = '{x: -4337, y: 13.776, z: 47535}'
        Rot = '{x: -0.19054812, y: 0.6809489, z: 0.6809489, w: 0.19054812}'
        Hint = '{x: -90, y: 0, z: 148.734}'
    },
    @{
        Name = 'samor_Tree_FractalBroad'
        Go = 1911000021
        Tr = 1911000022
        Mr = 1911000023
        Mf = 1911000024
        Col = 1911000025
        Mesh = '605b4d01195075c459975d321f244621'
        Pos = '{x: -4435, y: -15.321, z: 47598}'
        Rot = '{x: -0.6457519, y: 0.28810507, z: 0.28810507, w: 0.6457519}'
        Hint = '{x: -90, y: 0, z: 48.088}'
    },
    @{
        Name = 'samor_Tree_FractalAncient'
        Go = 1911000031
        Tr = 1911000032
        Mr = 1911000033
        Mf = 1911000034
        Col = 1911000035
        Mesh = '4cca057b76fdd2c46ad111c702b6e5d9'
        Pos = '{x: -4334, y: 32.127, z: 47604}'
        Rot = '{x: -0.19054812, y: 0.6809489, z: 0.6809489, w: 0.19054812}'
        Hint = '{x: -90, y: 0, z: 148.734}'
    }
)

$documents = @()
foreach ($tree in $trees) {
    $documents += @"
--- !u!1 &$($tree.Go)
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: $($tree.Tr)}
  - component: {fileID: $($tree.Mf)}
  - component: {fileID: $($tree.Mr)}
  - component: {fileID: $($tree.Col)}
  m_Layer: 0
  m_Name: $($tree.Name)
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &$($tree.Tr)
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: $($tree.Go)}
  serializedVersion: 2
  m_LocalRotation: $($tree.Rot)
  m_LocalPosition: $($tree.Pos)
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 1
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: $($tree.Hint)
--- !u!23 &$($tree.Mr)
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: $($tree.Go)}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 2
  m_RayTraceProcedural: 0
  m_RayTracingAccelStructBuildFlagsOverride: 0
  m_RayTracingAccelStructBuildFlags: 1
  m_SmallMeshCulling: 1
  m_ForceMeshLod: -1
  m_MeshLodSelectionBias: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {fileID: 2100000, guid: b7f3997abd33cd645a4b13ada15b1fa8, type: 2}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 1
  m_StaticBatchRoot: {fileID: 0}
  m_ProbeAnchor: {fileID: 0}
  m_LightProbeVolumeOverride: {fileID: 0}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_GlobalIlluminationMeshLod: 0
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_MaskInteraction: 0
  m_AdditionalVertexStreams: {fileID: 0}
--- !u!33 &$($tree.Mf)
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: $($tree.Go)}
  m_Mesh: {fileID: 4300000, guid: $($tree.Mesh), type: 2}
--- !u!64 &$($tree.Col)
MeshCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: $($tree.Go)}
  m_Material: {fileID: 0}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 0
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 5
  m_Convex: 0
  m_CookingOptions: 30
  m_Mesh: {fileID: 4300000, guid: $($tree.Mesh), type: 2}
"@
}

$text = $text.TrimEnd() + "`r`n`r`n" + ($documents -join "`r`n") + "`r`n"
foreach ($tree in $trees) {
    $rootLine = "  - {fileID: $($tree.Tr)}"
    $rootMatch = [regex]::Match(
        $text,
        '(?m)^SceneRoots:\r?\n  m_ObjectHideFlags: 0\r?\n  m_Roots:\r?\n(?:  - \{fileID: \d+\}\r?\n)+'
    )
    if (-not $rootMatch.Success) {
        throw 'SceneRoots list was not found.'
    }
    $text = $text.Insert($rootMatch.Index + $rootMatch.Length, $rootLine + "`r`n")
}

[System.IO.File]::WriteAllText($scene, $text, $utf8)
Write-Output ('INSTALLED backup=' + $backup)
