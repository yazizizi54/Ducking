# Wires the imported button-click and jump sounds into SampleScene.
# Writes with UTF8NoBOM so the Chinese-free scene file stays byte-clean.
$ErrorActionPreference = 'Stop'

$scene = 'E:\Github-Duckkking\Ducking\Ducking\Assets\Scenes\SampleScene.unity'
$backup = "$scene.bak_audio"
if (-not (Test-Path $backup)) { Copy-Item $scene $backup }

$text = [System.IO.File]::ReadAllText($scene, [System.Text.UTF8Encoding]::new($false))

# --- ids -------------------------------------------------------------------
$duck1Go      = 453113160
$duck1Sfx     = 453113170
$duck2Go      = 1240820886
$duck2Sfx     = 1240820894
$directorGo   = 1936001000
$directorTr   = 1936001001
$directorComp = 1936001002
$clickerGo    = 1936001003
$clickerTr    = 1936001004
$clickerComp  = 1936001005

$guidDirector = '8337ecf2babb39644baacc364e87e3e4'
$guidDuckSfx  = 'd84c00bc9dcc3144d84d416294f97887'
$guidClicker  = 'b2e7c4a1d9f3402ea1c53b8d6f0e7a24'

$guidClickClip = '4051e68dee995a34f89d141a10db1cd2'
$guidJumpClip  = '6b9fbfc4cd893c64e9bc4a450cd6d168'

$nl = "`r`n"
if ($text -notmatch "`r`n") { $nl = "`n" }

function New-Block([string[]]$lines) { return (($lines -join $nl) + $nl) }

# --- components appended to the two ducks ----------------------------------
$duck1Component = "  - component: {fileID: $duck1Sfx}"
$duck2Component = "  - component: {fileID: $duck2Sfx}"

$old1 = "  - component: {fileID: 453113167}" + $nl + "  m_Layer: 0"
$new1 = "  - component: {fileID: 453113167}" + $nl + $duck1Component + $nl + "  m_Layer: 0"
if (-not $text.Contains($old1)) { throw 'Duck1 component anchor not found' }
$text = $text.Replace($old1, $new1)

$old2 = "  - component: {fileID: 1240820893}" + $nl + "  m_Layer: 0"
$new2 = "  - component: {fileID: 1240820893}" + $nl + $duck2Component + $nl + "  m_Layer: 0"
if (-not $text.Contains($old2)) { throw 'Duck2 component anchor not found' }
$text = $text.Replace($old2, $new2)

# --- new MonoBehaviour blocks ----------------------------------------------
$duckSfxTemplate = @(
'--- !u!114 &__ID__',
'MonoBehaviour:',
'  m_ObjectHideFlags: 0',
'  m_CorrespondingSourceObject: {fileID: 0}',
'  m_PrefabInstance: {fileID: 0}',
'  m_PrefabAsset: {fileID: 0}',
'  m_GameObject: {fileID: __GO__}',
'  m_Enabled: 1',
'  m_EditorHideFlags: 0',
'  m_Script: {fileID: 11500000, guid: __GUID__, type: 3}',
'  m_Name: ',
'  m_EditorClassIdentifier: ',
'  jumpClip: {fileID: 8300000, guid: ' + $guidJumpClip + ', type: 3}',
'  landClip: {fileID: 0}',
'  winClip: {fileID: 0}',
'  jumpVolume: 0.55',
'  landVolume: 0.8',
'  winVolume: 1',
'  pitchJitter: 0.06',
'  hardLandingSpeed: 12'
)

$duck1Block = (($duckSfxTemplate -join $nl) -replace '__ID__', $duck1Sfx -replace '__GO__', $duck1Go -replace '__GUID__', $guidDuckSfx) + $nl
$duck2Block = (($duckSfxTemplate -join $nl) -replace '__ID__', $duck2Sfx -replace '__GO__', $duck2Go -replace '__GUID__', $guidDuckSfx) + $nl

$directorBlock = New-Block @(
"--- !u!1 &$directorGo",
'GameObject:',
'  m_ObjectHideFlags: 0',
'  m_CorrespondingSourceObject: {fileID: 0}',
'  m_PrefabInstance: {fileID: 0}',
'  m_PrefabAsset: {fileID: 0}',
'  serializedVersion: 6',
'  m_Component:',
"  - component: {fileID: $directorTr}",
"  - component: {fileID: $directorComp}",
'  m_Layer: 0',
'  m_Name: Audio',
'  m_TagString: Untagged',
'  m_Icon: {fileID: 0}',
'  m_NavMeshLayer: 0',
'  m_StaticEditorFlags: 0',
'  m_IsActive: 1',
"--- !u!4 &$directorTr",
'Transform:',
'  m_ObjectHideFlags: 0',
'  m_CorrespondingSourceObject: {fileID: 0}',
'  m_PrefabInstance: {fileID: 0}',
'  m_PrefabAsset: {fileID: 0}',
"  m_GameObject: {fileID: $directorGo}",
'  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}',
'  m_LocalPosition: {x: 0, y: 0, z: 0}',
'  m_LocalScale: {x: 1, y: 1, z: 1}',
'  m_ConstrainProportionsScale: 0',
'  m_Children: []',
'  m_Father: {fileID: 0}',
'  m_RootOrder: 0',
'  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}',
"--- !u!114 &$directorComp",
'MonoBehaviour:',
'  m_ObjectHideFlags: 0',
'  m_CorrespondingSourceObject: {fileID: 0}',
'  m_PrefabInstance: {fileID: 0}',
'  m_PrefabAsset: {fileID: 0}',
"  m_GameObject: {fileID: $directorGo}",
'  m_Enabled: 1',
'  m_EditorHideFlags: 0',
"  m_Script: {fileID: 11500000, guid: $guidDirector, type: 3}",
'  m_Name: ',
'  m_EditorClassIdentifier: ',
'  startMusic: {fileID: 0}',
'  loopMusic: 1',
'  musicFadeIn: 0.6',
'  musicCrossfade: 0.8',
'  sfxVoices: 8'
)

$clickerBlock = New-Block @(
"--- !u!1 &$clickerGo",
'GameObject:',
'  m_ObjectHideFlags: 0',
'  m_CorrespondingSourceObject: {fileID: 0}',
'  m_PrefabInstance: {fileID: 0}',
'  m_PrefabAsset: {fileID: 0}',
'  serializedVersion: 6',
'  m_Component:',
"  - component: {fileID: $clickerTr}",
"  - component: {fileID: $clickerComp}",
'  m_Layer: 0',
'  m_Name: ButtonSfx',
'  m_TagString: Untagged',
'  m_Icon: {fileID: 0}',
'  m_NavMeshLayer: 0',
'  m_StaticEditorFlags: 0',
'  m_IsActive: 1',
"--- !u!4 &$clickerTr",
'Transform:',
'  m_ObjectHideFlags: 0',
'  m_CorrespondingSourceObject: {fileID: 0}',
'  m_PrefabInstance: {fileID: 0}',
'  m_PrefabAsset: {fileID: 0}',
"  m_GameObject: {fileID: $clickerGo}",
'  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}',
'  m_LocalPosition: {x: 0, y: 0, z: 0}',
'  m_LocalScale: {x: 1, y: 1, z: 1}',
'  m_ConstrainProportionsScale: 0',
'  m_Children: []',
'  m_Father: {fileID: 0}',
'  m_RootOrder: 0',
'  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}',
"--- !u!114 &$clickerComp",
'MonoBehaviour:',
'  m_ObjectHideFlags: 0',
'  m_CorrespondingSourceObject: {fileID: 0}',
'  m_PrefabInstance: {fileID: 0}',
'  m_PrefabAsset: {fileID: 0}',
"  m_GameObject: {fileID: $clickerGo}",
'  m_Enabled: 1',
'  m_EditorHideFlags: 0',
'  m_Script: {fileID: 11500000, guid: ' + $guidClicker + ', type: 3}',
'  m_Name: ',
'  m_EditorClassIdentifier: ',
'  clickClip: {fileID: 8300000, guid: ' + $guidClickClip + ', type: 3}',
'  volume: 0.7',
'  pitchJitter: 0.04',
'  includeInactive: 1'
)

# --- insert the new blocks before SceneRoots -------------------------------
$anchor = '--- !u!1660057539 &9223372036854775807'
if (-not $text.Contains($anchor)) { throw 'SceneRoots anchor not found' }
$text = $text.Replace($anchor, $duck1Block + $duck2Block + $directorBlock + $clickerBlock + $anchor)

# --- register the two new roots -------------------------------------------
$text = $text.Replace("  - {fileID: 1041019452}", "  - {fileID: 1041019452}" + $nl + "  - {fileID: $directorTr}" + $nl + "  - {fileID: $clickerTr}")

[System.IO.File]::WriteAllText($scene, $text, [System.Text.UTF8Encoding]::new($false))
'patched'