# Restores the finish trigger on the baoxiang (treasure chest) object in SampleScene.
# Earlier the FinishZone component and its collider were stripped by an overwrite, so the
# chest did nothing. This adds a BoxCollider2D trigger sized from the sprite bounds plus a
# FinishZone component, and re-enables the SceneGameManager that had been left disabled.
$path = 'E:\Github-Duckkking\Ducking\Ducking\Assets\Scenes\SampleScene.unity'
Copy-Item $path "$path.bak_finishzone" -Force

$text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
$crlf = $text.Contains("`r`n")
$text = $text.Replace("`r`n", "`n")
$lines = $text.Split("`n")

$boxId = '1041019454'
$zoneId = '1041019455'
$goId = '1041019450'
$spriteGuid = 'eef332105c67e9f49b73b929339b0048'
$zoneScriptGuid = '3ba15604104471f4eb97f6e0f4c57bee'
$spriteName = 'baoxiang_sprite.png'

# 1) Register the two new components on the GameObject.
$old = "  - component: {fileID: 1041019451}`n  - component: {fileID: 1041019453}"
$new = "  - component: {fileID: 1041019451}`n  - component: {fileID: 1041019453}`n  - component: {fileID: $boxId}`n  - component: {fileID: $zoneId}"
if (-not $text.Contains($old)) { throw 'component list anchor not found' }
$text = $text.Replace($old, $new)

# 2) Append the collider and the FinishZone blocks right after the existing EndingScroll block,
#    i.e. just before the next document header.
$marker = "  allowSkip: 1`n--- !u!1 &1095984598"
if (-not $text.Contains($marker)) { throw 'append anchor not found' }

$block = @"
  allowSkip: 1
--- !u!61 &$boxId
BoxCollider2D:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: $goId}
  m_Enabled: 1
  serializedVersion: 3
  m_Density: 1
  m_Material: {fileID: 0}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_ForceSendLayers:
    serializedVersion: 2
    m_Bits: 4294967295
  m_ForceReceiveLayers:
    serializedVersion: 2
    m_Bits: 4294967295
  m_ContactCaptureLayers:
    serializedVersion: 2
    m_Bits: 4294967295
  m_CallbackLayers:
    serializedVersion: 2
    m_Bits: 4294967295
  m_IsTrigger: 1
  m_UsedByEffector: 0
  m_UsedByComposite: 0
  m_Offset: {x: 0, y: 0}
  m_SpriteTilingProperty:
    border: {x: 0, y: 0, z: 0, w: 0}
    pivot: {x: 0.5, y: 0.5}
    oldSize: {x: 3.2, y: 2.32}
    newSize: {x: 3.2, y: 2.32}
    adaptiveTilingThreshold: 0.5
    drawMode: 0
    adaptiveTiling: 0
  m_AutoTiling: 0
  serializedVersion: 2
  m_Size: {x: 3.6, y: 2.9}
  m_EdgeRadius: 0
--- !u!114 &$zoneId
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: $goId}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $zoneScriptGuid, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
--- !u!1 &1095984598
"@
$block = $block.Replace("`r`n", "`n")
$text = $text.Replace($marker, $block)

# 3) The manager was left disabled, so Finish() was never reachable either.
$text = $text.Replace(
  "  m_GameObject: {fileID: 1712312841}`n  m_Enabled: 0",
  "  m_GameObject: {fileID: 1712312841}`n  m_Enabled: 1")
$text = $text.Replace('  finishHeight: 1e+11', '  finishHeight: 88')

if ($crlf) { $text = $text.Replace("`n", "`r`n") }
[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Output 'patched'