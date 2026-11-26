$ErrorActionPreference = 'Stop'
$scene = 'E:\Github-Duckkking\Ducking\Ducking\Assets\Scenes\SampleScene.unity'
$backup = 'E:\Github-Duckkking\Ducking\SceneBackups\SampleScene.unity.bak_menubtn2'

Copy-Item -LiteralPath $scene -Destination $backup -Force
Write-Host "backup -> $backup"

$text = [System.IO.File]::ReadAllText($scene, [System.Text.Encoding]::UTF8)
$before = $text.Length
$nl = "`n"

# ---------------------------------------------- 1. delete the extra MenuButton
$menuIds = @(1936003010, 1936003011, 1936003012, 1936003013, 1936003014, 1936003015, 1936003016)
foreach ($id in $menuIds) {
    $pattern = "(?s)--- !u!\d+ &$id$nl.*?(?=$nl--- !u!|\z)"
    $m = [regex]::Match($text, $pattern)
    if (-not $m.Success) { throw "block $id not found" }
    $text = $text.Remove($m.Index, $m.Length)
}
Write-Host "removed extra MenuButton blocks"

$text = $text.Replace("  m_Children:$nl  - {fileID: 1936003011}$nl", "  m_Children:$nl")
Write-Host "removed MenuButton from SettingsPanel children"

# ---------------------------------------------- 2. turn ResetButton into MenuButton
$nameAnchor = "  m_Name: ResetButton$nl"
if (-not $text.Contains($nameAnchor)) { throw "ResetButton name line not found" }
$text = $text.Replace($nameAnchor, "  m_Name: MenuButton$nl")
Write-Host "renamed ResetButton -> MenuButton"

$oldLabel = 'm_Text: "\u91CD\u7F6E"'
$newLabel = 'm_Text: "\u4E3B\u83DC\u5355"'
if (-not $text.Contains($oldLabel)) { throw "reset label text not found" }
$text = $text.Replace($oldLabel, $newLabel)
Write-Host "label 91CD-7F6E -> 4E3B-83DC-5355 (zhongwen -> zhucaidan)"

# ---------------------------------------------- 3. rewire the script fields
$oldFields = "  resetButton: {fileID: 1253701552}$nl  menuButton: {fileID: 1936003016}$nl"
$newFields = "  resetButton: {fileID: 0}$nl  menuButton: {fileID: 1253701552}$nl"
if (-not $text.Contains($oldFields)) { throw "panel field block not found" }
$text = $text.Replace($oldFields, $newFields)
Write-Host "rewired resetButton -> empty, menuButton -> old reset button"

[System.IO.File]::WriteAllText($scene, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "written: $before -> $($text.Length) chars"

# ---------------------------------------------- verify
$check = [System.IO.File]::ReadAllText($scene, [System.Text.Encoding]::UTF8)
$lines = [System.IO.File]::ReadAllLines($scene, [System.Text.Encoding]::UTF8)
Write-Host ""
Write-Host "lines: $($lines.Count)"
Write-Host "blocks: $((([regex]::Matches($check, '(?m)^--- !u!')).Count))"
foreach ($id in $menuIds) {
    if ($check -match "&$id\b") { Write-Host "  !! leftover $id" } else { Write-Host "  $id cleared" }
}
Write-Host ""
Write-Host "SettingsPanel fields:"
$lines | Where-Object { $_ -match '^  (panelRoot|resetButton|backButton|menuButton|menuSceneName|restoreTimeScaleOnMenu)' } | ForEach-Object { "  $($_.Trim())" }
Write-Host ""
Write-Host "MenuButton objects:"
Select-String -Path $scene -Pattern 'm_Name: MenuButton' -Encoding UTF8 | ForEach-Object { "  line $($_.LineNumber)" }
Write-Host ""
Write-Host "button labels:"
Select-String -Path $scene -Pattern 'm_Text: "' -Encoding UTF8 | Where-Object { $_.Line -match '\\u4E3B|\\u91CD' } | ForEach-Object { "  line $($_.LineNumber): $($_.Line.Trim())" }