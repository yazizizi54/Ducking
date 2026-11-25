$ErrorActionPreference = 'Stop'
$scene = 'E:\Github-Duckkking\Ducking\Ducking\Assets\Scenes\SampleScene.unity'

# Strict UTF-8 read
$enc = New-Object System.Text.UTF8Encoding($false, $true)
$text = $enc.GetString([System.IO.File]::ReadAllBytes($scene))
$nl = if ($text -match "`r`n") { "`r`n" } else { "`n" }
$lines = $text -split "`r?`n"

# ---- 1. Replace the BalanceBeam MonoBehaviour serialized fields ----------------
$mbStart = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^--- !u!114 &1992698760\b') { $mbStart = $i; break }
}
if ($mbStart -lt 0) { throw 'BalanceBeam MonoBehaviour block not found' }

# find end of block (next '--- !u!' or EOF)
$mbEnd = $lines.Count
for ($i = $mbStart + 1; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -like '--- !u!*') { $mbEnd = $i; break }
}

$newMb = @(
    '--- !u!114 &1992698760'
    'MonoBehaviour:'
    '  m_ObjectHideFlags: 0'
    '  m_CorrespondingSourceObject: {fileID: 0}'
    '  m_PrefabInstance: {fileID: 0}'
    '  m_PrefabAsset: {fileID: 0}'
    '  m_GameObject: {fileID: 1992698759}'
    '  m_Enabled: 1'
    '  m_EditorHideFlags: 0'
    '  m_Script: {fileID: 11500000, guid: c9d5e26278acfc34f81e25aa276095cb, type: 3}'
    '  m_Name: '
    '  m_EditorClassIdentifier: '
    '  halfLength: 1.9'
    '  plankThickness: 0.4'
    '  surfaceOffset: 0.11'
    '  maxAngle: 18'
    '  plankMass: 6'
    '  boardTolerance: 0.45'
    '  edgeTolerance: 0.25'
    '  debugLog: 1'
)

$out = New-Object System.Collections.Generic.List[string]
for ($i = 0; $i -lt $mbStart; $i++) { $out.Add($lines[$i]) }
foreach ($l in $newMb) { $out.Add($l) }
for ($i = $mbEnd; $i -lt $lines.Count; $i++) { $out.Add($lines[$i]) }

# ---- 2. Insert Rigidbody2D + HingeJoint2D blocks after the BoxCollider2D ------
# BoxCollider2D block is !u!61 &1992698766; insert right after it ends.
$rb2dId = 1992698767
$hingeId = 1992698768
$insertAt = -1
for ($i = 0; $i -lt $out.Count; $i++) {
    if ($out[$i] -match '^--- !u!61 &1992698766\b') {
        for ($j = $i + 1; $j -lt $out.Count; $j++) {
            if ($out[$j] -like '--- !u!*') { $insertAt = $j; break }
        }
        break
    }
}
if ($insertAt -lt 0) { throw 'BoxCollider2D block end not found' }

$newComponents = @(
    "--- !u!50 &$rb2dId"
    'Rigidbody2D:'
    '  serializedVersion: 4'
    '  m_ObjectHideFlags: 0'
    '  m_CorrespondingSourceObject: {fileID: 0}'
    '  m_PrefabInstance: {fileID: 0}'
    '  m_PrefabAsset: {fileID: 0}'
    '  m_GameObject: {fileID: 1992698759}'
    '  m_BodyType: 0'
    '  m_Simulated: 1'
    '  m_UseFullKinematicContacts: 0'
    '  m_UseAutoMass: 0'
    '  m_Mass: 6'
    '  m_LinearDrag: 0.5'
    '  m_AngularDrag: 2'
    '  m_GravityScale: 3'
    '  m_Material: {fileID: 0}'
    '  m_IncludeLayers:'
    '    serializedVersion: 2'
    '    m_Bits: 0'
    '  m_ExcludeLayers:'
    '    serializedVersion: 2'
    '    m_Bits: 0'
    '  m_Interpolate: 1'
    '  m_Constraints: 0'
    '  m_CollisionDetection: 2'
    "--- !u!58 &$hingeId"
    'HingeJoint2D:'
    '  m_ObjectHideFlags: 0'
    '  m_CorrespondingSourceObject: {fileID: 0}'
    '  m_PrefabInstance: {fileID: 0}'
    '  m_PrefabAsset: {fileID: 0}'
    '  m_GameObject: {fileID: 1992698759}'
    '  m_Enabled: 1'
    '  m_ConnectedBody: {fileID: 0}'
    '  m_ConnectedRigidBody: {fileID: 0}'
    '  m_BreakForce: Infinity'
    '  m_BreakTorque: Infinity'
    '  m_AutoConfigureConnectedAnchor: 0'
    '  m_Anchor: {x: 0, y: 0}'
    '  m_ConnectedAnchor: {x: 0.06, y: 8.2}'
    '  m_UseLimits: 1'
    '  m_AngleLimits:'
    '    m_LowerAngle: -18'
    '    m_UpperAngle: 18'
    '  m_UseMotor: 0'
    '  m_Motor:'
    '    m_MotorSpeed: 0'
    '    m_MaximumMotorForce: 10000'
)

$out2 = New-Object System.Collections.Generic.List[string]
for ($i = 0; $i -lt $insertAt; $i++) { $out2.Add($out[$i]) }
foreach ($l in $newComponents) { $out2.Add($l) }
for ($i = $insertAt; $i -lt $out.Count; $i++) { $out2.Add($out[$i]) }

# ---- 3. Register the two components on the GameObject -------------------------
# GameObject 1992698759 component list is at fixed offsets; find it by search.
$added = $false
for ($i = 0; $i -lt $out2.Count; $i++) {
    if ($out2[$i] -match '^--- !u!1 &1992698759\b') {
        for ($j = $i; $j -lt $i + 20; $j++) {
            if ($out2[$j] -match '^  - component: \{fileID: 1992698766\}') {
                # insert after this line
                $out3 = New-Object System.Collections.Generic.List[string]
                for ($k = 0; $k -le $j; $k++) { $out3.Add($out2[$k]) }
                $out3.Add("  - component: {fileID: $rb2dId}")
                $out3.Add("  - component: {fileID: $hingeId}")
                for ($k = $j + 1; $k -lt $out2.Count; $k++) { $out3.Add($out2[$k]) }
                $out2 = $out3
                $added = $true
                break
            }
        }
        break
    }
}
if (-not $added) { throw 'GameObject component list not patched' }

$result = ($out2 -join $nl)
[System.IO.File]::WriteAllText($scene, $result, (New-Object System.Text.UTF8Encoding($false)))
"patched: $scene"
"lines: $($out2.Count)"