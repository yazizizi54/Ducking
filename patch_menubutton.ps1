$ErrorActionPreference = 'Stop'
$scene = 'E:\Github-Duckkking\Ducking\Ducking\Assets\Scenes\SampleScene.unity'
$backup = 'E:\Github-Duckkking\Ducking\SceneBackups\SampleScene.unity.bak_menubtn'

Copy-Item -LiteralPath $scene -Destination $backup -Force
Write-Host "backup -> $backup"

$text = [System.IO.File]::ReadAllText($scene, [System.Text.Encoding]::UTF8)
$before = $text.Length

# ---------------------------------------------------------------- new fileIDs
$goMenu   = 1936003010
$rtMenu   = 1936003011
$txtGo    = 1936003012
$txtRt    = 1936003013
$txtComp  = 1936003014
$imgComp  = 1936003015
$btnComp  = 1936003016

foreach ($id in @($goMenu,$rtMenu,$txtGo,$txtRt,$txtComp,$imgComp,$btnComp)) {
    if ($text -match "&$id\b") { throw "fileID $id already exists in the scene." }
}

# ------------------------------------------------------- copy look from Reset
# The new button reuses ResetButton's own visual setup so it matches the panel:
# same font, same size, same colours.
$resetGoBlockStart = $text.IndexOf("--- !u!1 &1253701550")
if ($resetGoBlockStart -lt 0) { throw "ResetButton GameObject 1253701550 not found." }

# Pull the ResetButton Button colour block so the new button looks identical.
$resetBtnStart = $text.IndexOf("--- !u!114 &1253701552")
$resetBtnBlock = $text.Substring($resetBtnStart, 2200)
$colorsPart = [regex]::Match($resetBtnBlock, "(?s)  m_Colors:\r?\n.*?(?=\r?\n  m_SpriteState:)").Value
if ([string]::IsNullOrEmpty($colorsPart)) { throw "Could not extract m_Colors from ResetButton." }
Write-Host "copied Button colour block"

# ------------------------------------------------------------------ new button
$n = "`n"

$goBlock = @"
--- !u!1 &$goMenu
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: $rtMenu}
  - component: {fileID: $imgComp}
  - component: {fileID: $btnComp}
  m_Layer: 0
  m_Name: MenuButton
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &$rtMenu
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: $goMenu}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {fileID: $txtRt}
  m_Father: {fileID: 1193776953}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0.5, y: 0.5}
  m_AnchorMax: {x: 0.5, y: 0.5}
  m_AnchoredPosition: {x: 150, y: -190}
  m_SizeDelta: {x: 220, y: 64}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!222 &$imgComp
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: $goMenu}
  m_CullTransparentMesh: 1
--- !u!114 &$btnComp
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: $goMenu}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 4e29b1a8efbd4b44bb3f3716e73f07ff, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Navigation:
    m_Mode: 3
    m_WrapAround: 0
    m_SelectOnUp: {fileID: 0}
    m_SelectOnDown: {fileID: 0}
    m_SelectOnLeft: {fileID: 0}
    m_SelectOnRight: {fileID: 0}
  m_Transition: 1
$colorsPart
  m_SpriteState:
    m_HighlightedSprite: {fileID: 0}
    m_PressedSprite: {fileID: 0}
    m_SelectedSprite: {fileID: 0}
    m_DisabledSprite: {fileID: 0}
  m_AnimationTriggers:
    m_NormalTrigger: Normal
    m_HighlightedTrigger: Highlighted
    m_PressedTrigger: Pressed
    m_SelectedTrigger: Selected
    m_DisabledTrigger: Disabled
  m_Interactable: 1
  m_TargetGraphic: {fileID: 0}
  m_OnClick:
    m_PersistentCalls:
      m_Calls: []
--- !u!1 &$txtGo
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: $txtRt}
  - component: {fileID: $txtComp}
  m_Layer: 0
  m_Name: Text
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &$txtRt
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: $txtGo}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: $rtMenu}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 0}
  m_AnchorMax: {x: 1, y: 1}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 0, y: 0}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!114 &$txtComp
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: $txtGo}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 5f7201a12d95ffc409449d95f23cf332, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {fileID: 0}
  m_Color: {r: 0.15, g: 0.12, b: 0.06, a: 1}
  m_RaycastTarget: 1
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {fileID: 12800000, guid: 7431e8561722f7a44bfcd964c12ca569, type: 3}
    m_FontSize: 32
    m_FontStyle: 0
    m_BestFit: 0
    m_MinSize: 10
    m_MaxSize: 40
    m_Alignment: 4
    m_AlignByGeometry: 0
    m_RichText: 1
    m_HorizontalOverflow: 0
    m_VerticalOverflow: 0
    m_LineSpacing: 1
  m_Text: "\u4E3B\u83DC\u5355"
"@

$goBlock = $goBlock -replace "`r`n", "`n"

# The panel object: append the new child to SettingsPanel's RectTransform (1193776953).
$panelRt = "--- !u!224 &1193776953"
$pi = $text.IndexOf($panelRt)
if ($pi -lt 0) { throw "SettingsPanel RectTransform 1193776953 not found." }
$childLine = "  - {fileID: $txtComp}"
$anchor = "m_Children:`n"
$ci = $text.IndexOf($anchor, $pi)
if ($ci -lt 0) { throw "m_Children of SettingsPanel not found." }
$insertAt = $ci + $anchor.Length
$text = $text.Insert($insertAt, "  - {fileID: $rtMenu}`n")
Write-Host "added MenuButton to SettingsPanel children"

# Append the new object blocks at the end of the file.
$text = $text.TrimEnd("`n") + "`n" + $goBlock
Write-Host "appended MenuButton object blocks"

# Wire the SettingsPanel component's menuButton field.
$fieldAnchor = "  resetButton: {fileID: 1253701552}`n"
if (-not $text.Contains($fieldAnchor)) { throw "resetButton field line not found." }
$text = $text.Replace($fieldAnchor, $fieldAnchor + "  menuButton: {fileID: $btnComp}`n  menuSceneName: TitleScene`n  restoreTimeScaleOnMenu: 1`n")
Write-Host "wired menuButton field"

[System.IO.File]::WriteAllText($scene, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "written: $before -> $($text.Length) chars"

# ---------------------------------------------------------------- verification
$check = [System.IO.File]::ReadAllText($scene, [System.Text.Encoding]::UTF8)
$heads = ([regex]::Matches($check, '(?m)^--- !u!')).Count
Write-Host "blocks: $heads"
foreach ($id in @($goMenu,$rtMenu,$txtGo,$txtRt,$txtComp,$imgComp,$btnComp)) {
    $c = ([regex]::Matches($check, "&$id\b")).Count
    Write-Host "  &$id occurrences(body) = $c"
}
Write-Host ""
Write-Host "SettingsPanel fields:"
($check -split "`n" | Where-Object { $_ -match '^  (panelRoot|resetButton|backButton|menuButton|menuSceneName|restoreTimeScaleOnMenu)' }) | ForEach-Object { "  $($_.Trim())" }