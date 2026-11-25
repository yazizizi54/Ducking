$ErrorActionPreference='Stop'
$root='E:\Github-Duckkking\Ducking\Ducking\Assets\Scripts'
$enc=New-Object System.Text.UTF8Encoding($false,$true)
$files=Get-ChildItem $root -Recurse -Filter *.cs
$fail=0
foreach($f in $files){
  $issues=@()
  try{ $t=$enc.GetString([System.IO.File]::ReadAllBytes($f.FullName)) }
  catch{ "ENCODING FAIL: $($f.FullName)"; $fail++; continue }

  if($t -match "\uFFFD"){ $issues+='U+FFFD replacement char' }

  $o=([regex]::Matches($t,'\{')).Count
  $c=([regex]::Matches($t,'\}')).Count
  if($o -ne $c){ $issues+="braces $o/$c" }

  $p=([regex]::Matches($t,'\(')).Count
  $q=([regex]::Matches($t,'\)')).Count
  if($p -ne $q){ $issues+="parens $p/$q" }

  if($issues.Count -gt 0){ "FAIL $($f.Name): $($issues -join ', ')"; $fail++ }
}
""
"checked $($files.Count) files, $fail with issues"