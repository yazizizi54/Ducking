# TitleScene had a Camera but no AudioListener, so nothing in the scene had an ear:
# both the music and every button click were played into silence. This adds the
# listener to the existing Main Camera, the same setup SampleScene already has.
$path = 'E:\Github-Duckkking\Ducking\Ducking\Assets\Scenes\TitleScene.unity'
Copy-Item $path "$path.bak_listener" -Force

$text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
$crlf = $text.Contains("`r`n")
$text = $text.Replace("`r`n", "`n")

$goId = '1196246754'
$listenerId = '1936003000'

# 1) Register on the Main Camera GameObject.
$old = "  - component: {fileID: 1196246756}`n  - component: {fileID: 1196246755}`n  m_Layer: 0`n  m_Name: Main Camera"
if (-not $text.Contains($old)) { throw 'camera component list anchor not found' }
$text = $text.Replace($old, "  - component: {fileID: 1196246756}`n  - component: {fileID: 1196246755}`n  - component: {fileID: $listenerId}`n  m_Layer: 0`n  m_Name: Main Camera")

# 2) Append the AudioListener right after the Camera block, before the next header.
$marker = "  m_StereoConvergence: 10`n  m_StereoSeparation: 0.022`n--- !u!"
if (-not $text.Contains($marker)) { throw 'camera block end anchor not found' }

$block = @"
  m_StereoConvergence: 10
  m_StereoSeparation: 0.022
--- !u!81 &$listenerId
AudioListener:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: $goId}
  m_Enabled: 1
--- !u!
"@
$block = $block.Replace("`r`n", "`n")
$text = $text.Replace($marker, $block)

if ($crlf) { $text = $text.Replace("`n", "`r`n") }
[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Output 'patched'