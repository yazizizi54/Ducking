# Adds the audio debug logger to the AudioDirector object in SampleScene, and to the
# runtime-spawned director for scenes that have none (the title screen). The log file it
# writes is the only reliable way to tell why a sound is inaudible.
$path = 'E:\Github-Duckkking\Ducking\Ducking\Assets\Scenes\SampleScene.unity'
Copy-Item $path "$path.bak_audiodbg" -Force

$text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
$crlf = $text.Contains("`r`n")
$text = $text.Replace("`r`n", "`n")

$goId = '1936001000'
$dbgId = '1936004000'
$dbgScript = '78b397277f5e40999f96b96d8b658b25'

$old = "  - component: {fileID: 1936001001}`n  - component: {fileID: 1936001002}`n  m_Layer: 0`n  m_Name: Audio"
if (-not $text.Contains($old)) { throw 'audio component list anchor not found' }
$text = $text.Replace($old, "  - component: {fileID: 1936001001}`n  - component: {fileID: 1936001002}`n  - component: {fileID: $dbgId}`n  m_Layer: 0`n  m_Name: Audio")

# The AudioDirector block ends right before the next document header. Insert after it.
$marker = "  m_Script: {fileID: 11500000, guid: 8337ecf2babb39644baacc364e87e3e4, type: 3}`n  m_Name: `n  m_EditorClassIdentifier: `n"
if (-not $text.Contains($marker)) { throw 'audio director block anchor not found' }

$block = $marker + @"
--- !u!114 &$dbgId
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: $goId}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $dbgScript, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
"@
$block = $block.Replace("`r`n", "`n")
$text = $text.Replace($marker, $block)

if ($crlf) { $text = $text.Replace("`n", "`r`n") }
[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Output 'patched sample'

# ---- Title scene: the director is spawned at runtime, so attach the logger there ----
$path2 = 'E:\Github-Duckkking\Ducking\Ducking\Assets\Scenes\TitleScene.unity'
Copy-Item $path2 "$path2.bak_audiodbg" -Force
$text2 = [System.IO.File]::ReadAllText($path2, [System.Text.Encoding]::UTF8)
$crlf2 = $text2.Contains("`r`n")
$text2 = $text2.Replace("`r`n", "`n")
$old2 = "  music: {fileID: 8300000, guid: cd29722bf9956c0498daeca0eb5ba97f, type: 3}`n  fadeTime: 1.2`n  volumeScale: 1`n  snapWhenSilent: 0"
if (-not $text2.Contains($old2)) { throw 'title music block anchor not found' }
# MusicSceneStarter gains a debug switch so the spawned director also logs.
$new2 = "  music: {fileID: 8300000, guid: cd29722bf9956c0498daeca0eb5ba97f, type: 3}`n  fadeTime: 1.2`n  volumeScale: 1`n  snapWhenSilent: 0`n  debugLog: 1"
$text2 = $text2.Replace($old2, $new2)
if ($crlf2) { $text2 = $text2.Replace("`n", "`r`n") }
[System.IO.File]::WriteAllText($path2, $text2, (New-Object System.Text.UTF8Encoding($false)))
Write-Output 'patched title'