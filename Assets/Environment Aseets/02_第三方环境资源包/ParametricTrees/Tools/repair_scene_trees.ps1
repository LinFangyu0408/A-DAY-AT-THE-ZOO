$ErrorActionPreference = 'Stop'

$scene = 'F:\My projects(1)\A DAY AT THE ZOO\Assets\Scenes\Monekey Mountain.unity'
$utf8 = [System.Text.UTF8Encoding]::new($false)
$text = [System.IO.File]::ReadAllText($scene, $utf8)

function Get-Document([string]$source, [string]$fileId) {
    $match = [regex]::Match($source, "(?ms)^--- !u!\d+ &$fileId\r?\n.*?(?=^--- !u!|\z)")
    if (-not $match.Success) { throw "Scene object $fileId was not found." }
    return $match.Value
}

function New-TreeDocuments {
    param(
        [string]$name,
        [string]$meshGuid,
        [string]$position,
        [string]$goId,
        [string]$transformId,
        [string]$rendererId,
        [string]$meshId
    )

    $go = Get-Document $text '2005267441'
    $tr = Get-Document $text '2005267442'
    $mr = Get-Document $text '2005267443'
    $mf = Get-Document $text '2005267444'
    $pairs = [ordered]@{
        '2005267441' = $goId
        '2005267442' = $transformId
        '2005267443' = $rendererId
        '2005267444' = $meshId
    }
    foreach ($pair in $pairs.GetEnumerator()) {
        $go = $go.Replace($pair.Key, $pair.Value)
        $tr = $tr.Replace($pair.Key, $pair.Value)
        $mr = $mr.Replace($pair.Key, $pair.Value)
        $mf = $mf.Replace($pair.Key, $pair.Value)
    }
    $go = $go -replace '(?m)^  m_Name: .*$', "  m_Name: $name"
    $tr = $tr -replace '(?m)^  m_LocalPosition: .*$', "  m_LocalPosition: $position"
    $mf = $mf -replace '(?m)^  m_Mesh: .*$', "  m_Mesh: {fileID: 4300000, guid: $meshGuid, type: 2}"
    return @($go, $tr, $mr, $mf, '')
}

$slender = New-TreeDocuments -name 'samor_Tree_Slender' -meshGuid 'cdf02677ef3e6c049bd6879a5c62cea2' -position '{x: 222.79004, y: 8.12, z: 23.5}' -goId '46514081' -transformId '46514082' -rendererId '46514083' -meshId '46514084'
$slenderPattern = '(?ms)^--- !u!1 &46514081\r?\n.*?(?=^--- !u!1001 &69222170)'
if (-not [regex]::IsMatch($text, $slenderPattern)) { throw 'Could not locate the broken slender tree block.' }
$text = [regex]::Replace($text, $slenderPattern, ($slender -join "`r`n"), 1)

$low = New-TreeDocuments -name 'samor_Tree_LowCanopy' -meshGuid '38ab1efe406e26b42b92eeb77786dd2e' -position '{x: 223.79004, y: 15.152, z: 51.5}' -goId '1564655115' -transformId '1564655116' -rendererId '1564655117' -meshId '1564655118'
$lowPattern = '(?ms)^--- !u!1 &1564655115\r?\n.*?(?=^--- !u!1001 &1641725100)'
if (-not [regex]::IsMatch($text, $lowPattern)) { throw 'Could not locate the broken low-canopy tree block.' }
$text = [regex]::Replace($text, $lowPattern, ($low -join "`r`n"), 1)

[System.IO.File]::WriteAllText($scene, $text, $utf8)
Write-Output 'REPAIRED'
