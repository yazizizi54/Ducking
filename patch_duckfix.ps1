$ErrorActionPreference = 'Stop'
$scene = 'E:\Github-Duckkking\Ducking\Ducking\Assets\Scenes\SampleScene.unity'
$backup = 'E:\Github-Duckkking\Ducking\SceneBackups\SampleScene.unity.bak_duckfix'

Copy-Item -LiteralPath $scene -Destination $backup -Force
Write-Host "backup -> $backup"

$text = [System.IO.File]::ReadAllText($scene, [System.Text.Encoding]::UTF8)
$before = $text.Length

# Duck1 and Duck2 both have groundCheckWidth: 1 / 0.85 and debugGround: 1 / 0.
# Duck1 was left on groundCheckWidth: 1 and debugGround: 1 during the ice-surface
# hunt. A full-width ground probe is wider than the duck, so it catches the ledge
# edge while the duck is still falling.
$duck1 = @('groundCheckWidth: 1
', 'debugGround: 1
')
$duck1new = @('groundCheckWidth: 0.85
', 'debugGround: 0
')

# Only rewrite the first occurrence of each: Duck1 appears before Duck2 in the file.
$i = $text.IndexOf("groundCheckWidth: 1`n")
if ($i -ge 0) {
    $text = $text.Remove($i, "groundCheckWidth: 1`n".Length).Insert($i, "groundCheckWidth: 0.85`n")
    Write-Host "Duck1 groundCheckWidth 1 -> 0.85"
} else {
    Write-Host "groundCheckWidth: 1 not found (already fixed?)"
}

$j = $text.IndexOf("debugGround: 1`n")
if ($j -ge 0) {
    $text = $text.Remove($j, "debugGround: 1`n".Length).Insert($j, "debugGround: 0`n")
    Write-Host "Duck1 debugGround 1 -> 0"
} else {
    Write-Host "debugGround: 1 not found (already fixed?)"
}

# The new input probe field does not exist in the serialized data yet. Unity would
# load it as false, which is what we want by default, but adding it explicitly keeps
# the Inspector from showing a stale zero for a field the player may want to use.
$anchor = "nudgeStep: 1`n"
if ($text.Contains($anchor) -and $text.IndexOf("debugInput:") -lt 0) {
    $count = ([regex]::Matches($text, [regex]::Escape($anchor))).Count
    $text = $text.Replace($anchor, $anchor + "  debugInput: 0`n")
    Write-Host "added debugInput: 0 to $count duck(s)"
}

[System.IO.File]::WriteAllText($scene, $text, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "written: $before -> $($text.Length) chars"

# Verify
$check = [System.IO.File]::ReadAllText($scene, [System.Text.Encoding]::UTF8)
$lines = $check -split "`n"
foreach ($n in @('groundCheckWidth:', 'debugGround:', 'debugInput:')) {
    $found = $lines | Where-Object { $_.Trim().StartsWith($n) }
    Write-Host "$n -> $($found -join ' | ')"
}