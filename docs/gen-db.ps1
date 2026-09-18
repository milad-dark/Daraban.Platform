Set-StrictMode -Version Latest
# DB inventory generator for docs/13-Database-Guide.md.
# Emits: module | entity | base type | scalar props | navigations
# plus:  module | entity | table | schema-note | indexes | query filter
$entOut = @()
$cfgOut = @()
$modDirs = Get-ChildItem src/Modules -Directory | Select-Object -ExpandProperty FullName
foreach ($modDir in $modDirs) {
  $mod = Split-Path $modDir -Leaf
  $entFiles = Get-ChildItem $modDir -Recurse -File -Filter *.cs |
    Where-Object { $_.FullName -match '\\Entities\\' -and $_.FullName -notmatch '\\(obj|bin)\\' }
  foreach ($f in $entFiles) {
    $txt = Get-Content $f.FullName -Raw
    foreach ($m in ([regex]::Matches($txt, 'public (?:class|enum) (\w+)(?:\s*:\s*([\w<>,\s]+))?'))) {
      $name = $m.Groups[1].Value
      $base = $m.Groups[2].Value.Trim()
      if ($name -match '^(Create|Update|.*Request|.*Dto|.*Response|.*Result)$') { continue }
      if ($m.Value -match 'public enum') {
        $entOut += "$mod | ENUM $name | - | - | -"
        continue
      }
      $props = @()
      $navs = @()
      foreach ($pm in ([regex]::Matches($txt, 'public ([\w<>,\?\s\[\]]+?) (\w+) \{ get; set; \}'))) {
        $ptype = $pm.Groups[1].Value.Trim() -replace '\s+', ' '
        $pname = $pm.Groups[2].Value
        if ($ptype -match '^(ICollection|List)<(\w+)>') { $navs += "$pname -> $($Matches[1])" }
        elseif ($ptype -match '^(string|Guid|int|long|decimal|double|float|bool|DateTimeOffset|DateTime|DateOnly|TimeSpan|byte)\??$') { $props += "${pname}: ${ptype}" }
        elseif ($ptype -match '^(Guid\?|DateTimeOffset\?|DateOnly\?|int\?|long\?|decimal\?|bool\?)') { $props += "${pname}: ${ptype}" }
        else { $navs += "${pname}: ${ptype} (nav)" }
      }
      $entOut += "$mod | $name | base=$base | $(([string]::Join('; ', $props))) | $(([string]::Join('; ', $navs)))"
    }
  }
  $cfgFiles = Get-ChildItem $modDir -Recurse -File -Filter *Configuration.cs |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }
  foreach ($f in $cfgFiles) {
    $txt = Get-Content $f.FullName -Raw
    $entity = ''
    if ($txt -match 'IEntityTypeConfiguration<(\w+)>') { $entity = $Matches[1] }
    $table = ''
    if ($txt -match 'ToTable\("([^"]+)"(?:,\s*"([^"]+)")?\)') {
      $table = $Matches[1]
      if ($Matches[2] -ne '') { $table = "$($Matches[2]).$table" }
    }
    $idx = @()
    foreach ($im in ([regex]::Matches($txt, 'HasIndex\((.*?)\)\s*(?:\.IsUnique\(\))?\s*(?:\.HasDatabaseName\("([^"]+)"\))?'))) {
      $cols = $im.Groups[1].Value.Trim()
      $nm = $im.Groups[2].Value
      $uniq = if ($im.Value -match 'IsUnique') { ' UNIQUE' } else { '' }
      $idx += "$cols [$nm]$uniq"
    }
    $flt = if ($txt -match 'HasQueryFilter\((.*?)\)') { $Matches[1].Value.Trim() } else { '' }
    $gin = if ($txt -match 'HasMethod\("(\w+)"\)') { "method=$($Matches[1].Value)" } else { '' }
    $cfgOut += "$mod | $entity | $table | idx: $(([string]::Join(' || ', $idx))) | filter: $flt $gin"
  }
}
$entOut | Sort-Object | Get-Unique | Out-File "$env:TEMP/opencode-db-entities.txt"
$cfgOut | Sort-Object | Get-Unique | Out-File "$env:TEMP/opencode-db-config.txt"
"entities: $($entOut.Count) / configs: $($cfgOut.Count)"
