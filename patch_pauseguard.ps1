# Adds PauseStateGuard to the game's GameManager object. The settings panel in this
# scene is toggled by three different things (the HUD button, its own Back button, and
# Escape), and only the HUD used to give timeScale back, which is why the level stayed
# frozen after Back. The guard owns that decision instead.
$path = 'E:\Github-Duckkking\Ducking\Ducking\Assets\Scenes\SampleScene.unity'
Copy-Item $path "$path.bak_pauseguard" -Force

$text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
$crlf = $text.Contains("`r`n")
$text = $text.Replace("`r`n", "`n")

$goId = '1712312841'
$mbId = '1712312842'
$guardId = '1712312850'
$guardScript = '707157cd087048978b8d75bfe1ad3dff'
$panelId = '1193776952'

# Match the GameObject header plus its component list, which is unique to this object
# because of the id. Matching the shared MonoBehaviour tail would corrupt the file.
$old = "--- !u!1 &$goId`nGameObject:`n"
if (-not $text.Contains($old)) { throw 'game manager header not found' }

$headerStart = $text.IndexOf($old)
$headerEnd = $text.IndexOf("  m_Name: GameManager", $headerStart)
if ($headerEnd -lt 0) { throw 'game manager name not found' }

$before = $text.Substring(0, $headerEnd)
$after = $text.Substring($headerEnd)

# Insert the component reference at the end of the m_Component list.
$compAnchor = "  m_Component:`n"
$lastRef = $before.LastIndexOf("  - component: {fileID: $mbId}`n")
if ($lastRef -lt 0) { throw 'game manager component list does not reference the manager' }
$insertAt = $lastRef + ("  - component: {fileID: $mbId}`n").Length
$before = $before.Substring(0, $insertAt) + "  - component: {fileID: $guardId}`n" + $before.Substring($insertAt)

# Append the guard block right after the SceneGameManager block, which ends at the
# next document header.
$tail = "  startFlashTime: 0.6`n--- !u!"
if ($text.IndexOf($tail) -lt 0) { throw 'manager block end anchor not found' }

$guardBlock = "  startFlashTime: 0.6`n" + @"
--- !u!114 &$guardId
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: $goId}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $guardScript, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  pausePanels:
  - {fileID: $panelId}
  ignorePanels: []
  scanByName: 1
  pauseNames:
  - settingspanel
  - pausemenu
  debugLog: 1
--- !u!
"@
$guardBlock = $guardBlock.Replace("`r`n", "`n")
$text = $before + $after
$text = $text.Replace($tail, $guardBlock)

if ($crlf) { $text = $text.Replace("`n", "`r`n") }
[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Output 'patched'