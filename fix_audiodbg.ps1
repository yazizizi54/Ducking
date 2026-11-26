# Repairs the SampleScene damage done by patch_audiodbg.ps1.
# That script matched the AudioDirector's "m_Script ... m_Name ... m_EditorClassIdentifier"
# tail, which is shared by EVERY MonoBehaviour, so the new block landed inside the director
# and its own fields were left dangling at the bottom. This rebuilds the two blocks in the
# right order and moves the debug logger into a real block of its own.
$path = 'E:\Github-Duckkking\Ducking\Ducking\Assets\Scenes\SampleScene.unity'
Copy-Item $path "$path.bak_broken" -Force

$text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
$crlf = $text.Contains("`r`n")
$text = $text.Replace("`r`n", "`n")

$broken = @"
  m_Script: {fileID: 11500000, guid: 8337ecf2babb39644baacc364e87e3e4, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
--- !u!114 &1936004000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1936001000}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 78b397277f5e40999f96b96d8b658b25, type: 3}
  m_Name: 
  m_EditorClassIdentifier:   startMusic: {fileID: 0}
  loopMusic: 1
  musicFadeIn: 0.6
  musicCrossfade: 0.8
  sfxVoices: 8
"@
$broken = $broken.Replace("`r`n", "`n")

$fixed = @"
  m_Script: {fileID: 11500000, guid: 8337ecf2babb39644baacc364e87e3e4, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  startMusic: {fileID: 0}
  loopMusic: 1
  musicFadeIn: 0.6
  musicCrossfade: 0.8
  sfxVoices: 8
--- !u!114 &1936004000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1936001000}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 78b397277f5e40999f96b96d8b658b25, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
"@
$fixed = $fixed.Replace("`r`n", "`n")

if (-not $text.Contains($broken)) { throw 'broken block not found - inspect manually' }
$text = $text.Replace($broken, $fixed)

if ($crlf) { $text = $text.Replace("`n", "`r`n") }
[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Output 'repaired'