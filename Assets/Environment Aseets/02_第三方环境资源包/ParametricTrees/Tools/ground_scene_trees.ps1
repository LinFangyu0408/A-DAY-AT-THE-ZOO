$ErrorActionPreference = 'Stop'

$scene = 'F:\My projects(1)\A DAY AT THE ZOO\Assets\Scenes\Monekey Mountain.unity'
$utf8 = [System.Text.UTF8Encoding]::new($false)
$text = [System.IO.File]::ReadAllText($scene, $utf8)

$heights = [ordered]@{
    'samor_Tree_Slender'  = '8.12'
    'samor_Tree_TwinFork' = '-7.706'
    'samor_Tree_LowCanopy' = '15.152'
}

foreach ($entry in $heights.GetEnumerator()) {
    $name = [regex]::Escape($entry.Key)
    $height = $entry.Value
    $pattern = "(?ms)(m_Name: $name.*?^--- !u!4 &\d+\r?\nTransform:.*?^  m_LocalPosition: \{x: [^,]+, y: )[-0-9.]+(, z: [-0-9.]+\})"
    $match = [regex]::Match($text, $pattern)
    if (-not $match.Success) {
        throw "Transform for $($entry.Key) was not found."
    }
    $match = [regex]::Match($text, $pattern)
    $before = $text.Substring(0, $match.Index)
    $after = $text.Substring($match.Index + $match.Length)
    $text = $before + $match.Groups[1].Value + $height + $match.Groups[2].Value + $after
}

[System.IO.File]::WriteAllText($scene, $text, $utf8)
Write-Output 'GROUNDED'
