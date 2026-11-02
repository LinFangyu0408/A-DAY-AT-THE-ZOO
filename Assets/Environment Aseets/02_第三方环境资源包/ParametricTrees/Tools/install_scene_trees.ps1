$ErrorActionPreference = 'Stop'

$scene = 'F:\My projects(1)\A DAY AT THE ZOO\Assets\Scenes\Monekey Mountain.unity'
$backup = 'F:\My projects(1)\A DAY AT THE ZOO\Assets\Scenes\Monekey Mountain.backup-2026-11-02-pre-parametric-trees.unity'
$utf8 = [System.Text.UTF8Encoding]::new($false)

if (-not (Test-Path -LiteralPath $backup)) {
    throw "Required scene backup is missing: $backup"
}

$text = [System.IO.File]::ReadAllText($scene, $utf8)
if ($text.Contains('samor_Tree_Spreading')) {
    Write-Output 'ALREADY_INSTALLED'
    exit 0
}

function Get-Document([string]$source, [string]$fileId) {
    $match = [regex]::Match(
        $source,
        "(?ms)^--- !u!\d+ &$fileId\r?\n.*?(?=^--- !u!|\z)"
    )
    if (-not $match.Success) {
        throw "Scene object $fileId was not found."
    }
    return $match.Value
}

function Replace-Once([string]$source, [string]$old, [string]$new, [string]$label) {
    if (-not $source.Contains($old)) {
        throw "Could not find $label."
    }
    return $source.Replace($old, $new, 1)
}

function Clone-TreeDocument {
    param(
        [string]$gameObjectId,
        [string]$transformId,
        [string]$meshId,
        [string]$rendererId,
        [string]$name,
        [string]$meshGuid,
        [string]$position,
        [bool]$rootLevel,
        [int]$sequence
    )

    $go = Get-Document $text $gameObjectId
    $tr = Get-Document $text $transformId
    $mf = Get-Document $text $meshId
    $mr = Get-Document $text $rendererId

    $newGoId = (1881000001 + $sequence * 10).ToString()
    $newTrId = (1881000002 + $sequence * 10).ToString()
    $newMfId = (1881000003 + $sequence * 10).ToString()
    $newMrId = (1881000004 + $sequence * 10).ToString()

    $go = $go.Replace($gameObjectId, $newGoId)
    $tr = $tr.Replace($transformId, $newTrId).Replace($gameObjectId, $newGoId)
    $mf = $mf.Replace($meshId, $newMfId).Replace($gameObjectId, $newGoId)
    $mr = $mr.Replace($rendererId, $newMrId).Replace($gameObjectId, $newGoId)

    $go = $go -replace '(?m)^  m_Name: .*$', "  m_Name: $name"
    $tr = $tr -replace '(?m)^  m_LocalPosition: .*$', "  m_LocalPosition: $position"
    if ($rootLevel) {
        $tr = $tr -replace '(?m)^  m_Father: .*$', '  m_Father: {fileID: 0}'
    }
    $mf = $mf -replace '(?m)^  m_Mesh: .*$', "  m_Mesh: {fileID: 4300000, guid: $meshGuid, type: 2}"
    return @($go, $tr, $mf, $mr, '')
}

$guids = @{
    Spreading = '36260261babee1547a0669780fcb9bf5'
    Slender   = 'cdf02677ef3e6c049bd6879a5c62cea2'
    WindSwept = '0c06cf79c5819954982eb31efd8c02c8'
    TwinFork  = 'd469f4586a0b4d049a1249a101daae4e'
    LowCanopy = '38ab1efe406e26b42b92eeb77786dd2e'
}

# Replace the two existing user-placed trees in their current transforms.
$text = Replace-Once $text 'm_Name: samor_4077_31 (1)' 'm_Name: samor_Tree_Spreading' 'existing tree name'
$text = Replace-Once $text 'm_Mesh: {fileID: -476688809330563646, guid: dd3784d5c268c0c4c96477fbad0e9614, type: 3}' "m_Mesh: {fileID: 4300000, guid: $($guids.Spreading), type: 2}" 'spreading mesh reference'
$text = Replace-Once $text 'm_Name: samor_4077_569 (1)' 'm_Name: samor_Tree_WindSwept' 'existing tree name'
$text = Replace-Once $text 'm_Mesh: {fileID: -1108902464119403747, guid: dd3784d5c268c0c4c96477fbad0e9614, type: 3}' "m_Mesh: {fileID: 4300000, guid: $($guids.WindSwept), type: 2}" 'windswept mesh reference'

# Add three independent, grounded trees around the existing cluster.
$newDocuments = @()
$newDocuments += Clone-TreeDocument -gameObjectId '2005267441' -transformId '2005267442' -meshId '2005267444' -rendererId '2005267443' -name 'samor_Tree_Slender' -meshGuid $guids.Slender -position '{x: -4357.355, y: 8.12, z: 47545.88}' -rootLevel $true -sequence 1
$newDocuments += Clone-TreeDocument -gameObjectId '1561309089' -transformId '1561309090' -meshId '1561309092' -rendererId '1561309091' -name 'samor_Tree_TwinFork' -meshGuid $guids.TwinFork -position '{x: -4401.075, y: -7.706, z: 47572.24}' -rootLevel $true -sequence 2
$newDocuments += Clone-TreeDocument -gameObjectId '2005267441' -transformId '2005267442' -meshId '2005267444' -rendererId '2005267443' -name 'samor_Tree_LowCanopy' -meshGuid $guids.LowCanopy -position '{x: -4356.355, y: 15.152, z: 47573.88}' -rootLevel $true -sequence 3

$text = $text.TrimEnd() + "`r`n`r`n" + ($newDocuments -join "`r`n")
[System.IO.File]::WriteAllText($scene, $text, $utf8)
Write-Output 'INSTALLED'
