$ErrorActionPreference = 'Stop'
$scene = 'E:\Github-Duckkking\Ducking\Ducking\Assets\Scenes\SampleScene.unity'
$backup = 'E:\Github-Duckkking\Ducking\SceneBackups\SampleScene.unity.bak_standpartner'

Copy-Item -LiteralPath $scene -Destination $backup -Force
Write-Host "backup -> $backup"

$text = [System.IO.File]::ReadAllText($scene, [System.Text.Encoding]::UTF8)
$before = $text.Length
$nl = "`n"

# DuckController serde block has groundCheckWidth right after groundCheckOffset.
# Insert the new field after groundCheckWidth so the order matches the script.
$anchor = "  groundCheckWidth: 0.85"
$cnt = ([regex]::Matches($text, [regex]::Escape($anchor))).Count
Write-Host "groundCheckWidth anchor occurrences: $cnt"
if ($cnt -ne 2) { throw "expected 2 duck blocks, found $cnt" }

if ($text.Contains("  canStandOnPartner:")) { throw "field already present" }

$text = $text.Replace($anchor, $anchor + $nl + "  canStandOnPartner: 1")
Write-Host "inserted canStandOnPartner: 1 into 2 blocks"

[System.IO.File]::WriteAllText($scene, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "written: $before -> $($text.Length) chars"

# verify
$lines = [System.IO.File]::ReadAllLines($scene, [System.Text.Encoding]::UTF8)
Write-Host ""
Write-Host "lines: $($lines.Count)"
Write-Host "blocks: $((([regex]::Matches([System.IO.File]::ReadAllText($scene, [System.Text.Encoding]::UTF8), '(?m)^--- !u!')).Count))"
Write-Host ""
Write-Host "duck fields:"
Select-String -Path $scene -Pattern '^  (groundCheckWidth|canStandOnPartner|groundLayers|coyoteTime|maxFallSpeed)' -Encoding UTF8 | ForEach-Object { "  line $($_.LineNumber): $($_.Line.Trim())" }