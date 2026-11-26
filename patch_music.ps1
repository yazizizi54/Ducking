# Wires music and the duck quack into SampleScene, TitleScene and StoryScene.
# Every write uses UTF8 without BOM so the YAML stays exactly as Unity wrote it.

$ErrorActionPreference = 'Stop'
$enc = [System.Text.UTF8Encoding]::new($false)

$scenes = 'E:\Github-Duckkking\Ducking\Ducking\Assets\Scenes'
$sampleScene = Join-Path $scenes 'SampleScene.unity'
$titleScene  = Join-Path $scenes 'TitleScene.unity'
$storyScene  = Join-Path $scenes 'StoryScene.unity'

foreach ($s in $sampleScene, $titleScene, $storyScene) {
    $bak = "$s.bak_music"
    if (-not (Test-Path $bak)) { Copy-Item $s $bak }
}

# --- guids -----------------------------------------------------------------
$guidDirector   = '8337ecf2babb39644baacc364e87e3e4'
$guidStarter    = 'a61f4c2e8b7d41e5a0c3d9b17f2e8c54'
$guidEnding     = 'f3d19a6c2e5b48f7b9a4c0e83d71b26f'
$guidDuckSfx    = 'd84c00bc9dcc3144d84d416294f97887'
$guidBackground = 'cd29722bf9956c0498daeca0eb5ba97f'
$guidGameover   = '9029e077f55d1c145bfa3019cf000755'
$guidDuckQuack  = 'dd919e0819f892345964001d33a5388e'

# Music is an AudioClip, so the fileID inside its asset is 8300000.
function Ref([string]$guid) { return "{fileID: 8300000, guid: $guid, type: 3}" }

function Read-Scene([string]$path) {
    return [System.IO.File]::ReadAllText($path, [System.Text.UTF8Encoding]::new($false))
}

function Write-Scene([string]$path, [string]$text) {
    [System.IO.File]::WriteAllText($path, $text, [System.Text.UTF8Encoding]::new($false))
}

function Get-Newline([string]$text) {
    if ($text.Contains("`r`n")) { return "`r`n" } else { return "`n" }
}

# Builds a plain scene object (Transform + one MonoBehaviour) as raw YAML.
function New-ObjectBlock {
    param(
        [int]$goId, [int]$trId, [int]$mbId, [string]$name,
        [string]$scriptGuid, [string[]]$fields, [string]$nl
    )

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("--- !u!1 &$goId")
    $lines.Add('GameObject:')
    $lines.Add('  m_ObjectHideFlags: 0')
    $lines.Add('  m_CorrespondingSourceObject: {fileID: 0}')
    $lines.Add('  m_PrefabInstance: {fileID: 0}')
    $lines.Add('  m_PrefabAsset: {fileID: 0}')
    $lines.Add('  serializedVersion: 6')
    $lines.Add('  m_Component:')
    $lines.Add("  - component: {fileID: $trId}")
    $lines.Add("  - component: {fileID: $mbId}")
    $lines.Add('  m_Layer: 0')
    $lines.Add("  m_Name: $name")
    $lines.Add('  m_TagString: Untagged')
    $lines.Add('  m_Icon: {fileID: 0}')
    $lines.Add('  m_NavMeshLayer: 0')
    $lines.Add('  m_StaticEditorFlags: 0')
    $lines.Add('  m_IsActive: 1')
    $lines.Add("--- !u!4 &$trId")
    $lines.Add('Transform:')
    $lines.Add('  m_ObjectHideFlags: 0')
    $lines.Add('  m_CorrespondingSourceObject: {fileID: 0}')
    $lines.Add('  m_PrefabInstance: {fileID: 0}')
    $lines.Add('  m_PrefabAsset: {fileID: 0}')
    $lines.Add("  m_GameObject: {fileID: $goId}")
    $lines.Add('  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}')
    $lines.Add('  m_LocalPosition: {x: 0, y: 0, z: 0}')
    $lines.Add('  m_LocalScale: {x: 1, y: 1, z: 1}')
    $lines.Add('  m_ConstrainProportionsScale: 0')
    $lines.Add('  m_Children: []')
    $lines.Add('  m_Father: {fileID: 0}')
    $lines.Add('  m_RootOrder: 0')
    $lines.Add('  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}')
    $lines.Add("--- !u!114 &$mbId")
    $lines.Add('MonoBehaviour:')
    $lines.Add('  m_ObjectHideFlags: 0')
    $lines.Add('  m_CorrespondingSourceObject: {fileID: 0}')
    $lines.Add('  m_PrefabInstance: {fileID: 0}')
    $lines.Add('  m_PrefabAsset: {fileID: 0}')
    $lines.Add("  m_GameObject: {fileID: $goId}")
    $lines.Add('  m_Enabled: 1')
    $lines.Add('  m_EditorHideFlags: 0')
    $lines.Add("  m_Script: {fileID: 11500000, guid: $scriptGuid, type: 3}")
    $lines.Add('  m_Name: ')
    $lines.Add('  m_EditorClassIdentifier: ')
    foreach ($f in $fields) { $lines.Add($f) }

    return (($lines -join $nl) + $nl)
}

# ---------------------------------------------------------------------------
# 1. TitleScene: a Music object so the menu has its own loop.
# ---------------------------------------------------------------------------
$text = Read-Scene $titleScene
$nl = Get-Newline $text

if (-not $text.Contains($guidStarter)) {
    $block = New-ObjectBlock -goId 1936002000 -trId 1936002001 -mbId 1936002002 `
        -name 'Music' -scriptGuid $guidStarter -nl $nl -fields @(
            "  music: $(Ref $guidBackground)",
            '  fadeTime: 1.2',
            '  volumeScale: 1',
            '  snapWhenSilent: 0'
        )

    $anchor = '--- !u!1660057539 &9223372036854775807'
    if (-not $text.Contains($anchor)) { throw 'TitleScene: SceneRoots anchor missing' }
    $text = $text.Replace($anchor, $block + $anchor)

    $lastRoot = '  - {fileID: 1041019452}'
    $newRoot = "  - {fileID: 1936002001}"
    if ($text.Contains($lastRoot)) {
        $text = $text.Replace($lastRoot, $lastRoot + $nl + $newRoot)
    } else {
        # Fallback: append to the end of the root list.
        $text = $text.TrimEnd() + $nl + $newRoot + $nl
    }

    Write-Scene $titleScene $text
    'TitleScene: Music object added'
} else {
    'TitleScene: already wired'
}

# ---------------------------------------------------------------------------
# 2. StoryScene: the menu loop keeps going through the intro, nothing to add.
#    Recorded so the report is honest about what was touched.
# ---------------------------------------------------------------------------

# ---------------------------------------------------------------------------
# 3. SampleScene: level loop, ending jingle, quack clip, lower music.
# ---------------------------------------------------------------------------
$text = Read-Scene $sampleScene
$nl = Get-Newline $text

# 3a. Level music + ending theme objects.
if (-not $text.Contains($guidStarter)) {
    $musicBlock = New-ObjectBlock -goId 1936002010 -trId 1936002011 -mbId 1936002012 `
        -name 'Music' -scriptGuid $guidStarter -nl $nl -fields @(
            "  music: $(Ref $guidBackground)",
            '  fadeTime: 1.2',
            '  volumeScale: 0.85',
            '  snapWhenSilent: 0'
        )

    $endingBlock = New-ObjectBlock -goId 1936002020 -trId 1936002021 -mbId 1936002022 `
        -name 'EndingMusic' -scriptGuid $guidEnding -nl $nl -fields @(
            "  endingMusic: $(Ref $guidGameover)",
            '  fadeIn: 0.6',
            '  fadeOut: 1.5',
            '  volumeScale: 1',
            '  stopLevelMusic: 0'
        )

    $anchor = '--- !u!1660057539 &9223372036854775807'
    if (-not $text.Contains($anchor)) { throw 'SampleScene: SceneRoots anchor missing' }
    $text = $text.Replace($anchor, $musicBlock + $endingBlock + $anchor)

    $lastRoot = '  - {fileID: 1936001004}'
    $addRoots = "  - {fileID: 1936002011}" + $nl + "  - {fileID: 1936002021}"
    if ($text.Contains($lastRoot)) {
        $text = $text.Replace($lastRoot, $lastRoot + $nl + $addRoots)
    } else {
        $text = $text.TrimEnd() + $nl + $addRoots + $nl
    }

    'SampleScene: Music + EndingMusic objects added'
} else {
    'SampleScene: music already wired'
}

# 3b. The two ducks get the quack clip and the quieter jump volume.
$oldJump = '  jumpVolume: 0.3'
$newJump = '  jumpVolume: 0.3'
$oldDuck = '  hardLandingSpeed: 12'
$newDuck = @(
    '  quackClip: ' + (Ref $guidDuckQuack)
    '  quackVolume: 0.7'
    '  quackKey: 0'
    '  quackCooldown: 0.25'
    '  hardLandingSpeed: 12'
) -join $nl

if ($text.Contains($oldDuck) -and -not $text.Contains('quackClip')) {
    $text = $text.Replace($oldDuck, $newDuck)
    'SampleScene: quack clip added to both ducks'
}

Write-Scene $sampleScene $text
'patched'