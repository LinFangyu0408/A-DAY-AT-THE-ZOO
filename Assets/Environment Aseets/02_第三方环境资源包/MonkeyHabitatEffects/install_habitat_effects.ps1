$ErrorActionPreference = 'Stop'

$scene = 'F:\My projects(1)\A DAY AT THE ZOO\Assets\Scenes\Monekey Mountain.unity'
$backup = 'F:\My projects(1)\A DAY AT THE ZOO\Assets\Scenes\Monekey Mountain.backup-2026-11-02-pre-habitat-effects.unity'
$utf8 = [System.Text.UTF8Encoding]::new($false)
$text = [System.IO.File]::ReadAllText($scene, $utf8)

if ($text.Contains('m_Name: Monkey Habitat Effects')) {
    Write-Output 'ALREADY_INSTALLED'
    exit 0
}

if (-not (Test-Path -LiteralPath $backup)) {
    Copy-Item -LiteralPath $scene -Destination $backup
}

$component = @'
--- !u!1 &1900000001
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 1900000002}
  - component: {fileID: 1900000003}
  m_Layer: 0
  m_Name: Monkey Habitat Effects
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1900000002
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1900000001}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!114 &1900000003
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1900000001}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 7f8e2b9d61ad4f2a91e8c0a7b3d5e401, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  gardenCenter: {x: -4380, y: 0, z: 47544}
  windDirection: {x: 0.83, y: 0, z: 0.56}
  gardenColumns: 12
  gardenRows: 8
  swayGeneratedTrees: 1
'@

$text = $text.TrimEnd() + "`r`n`r`n" + $component + "`r`n"
[System.IO.File]::WriteAllText($scene, $text, $utf8)
Write-Output ('INSTALLED backup=' + $backup)
