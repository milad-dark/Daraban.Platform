Set-StrictMode -Version Latest
$out = @()
$files = Get-ChildItem src -Recurse -File -Filter *Controller.cs |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }
foreach ($f in $files) {
  if ($f.FullName -match '\\Modules\\([^\\]+)\\') { $mod = $Matches[1] }
  elseif ($f.FullName -match '\\Host\\([^\\]+)\\') { $mod = 'Host/' + $Matches[1] }
  else { $mod = '?' }
  $lines = Get-Content $f.FullName
  $classRoute = ''
  $controller = ''
  $classAuth = ''
  foreach ($l in $lines) {
    if ($l.Trim() -match '^\[Route\("([^"]+)"\)\]') { $classRoute = $Matches[1]; break }
  }
  foreach ($l in $lines) {
    # The [controller] token (used by all five Financial controllers) expands to the
    # class basename per ASP.NET convention. Without this, five distinct controllers
    # emit the identical string "/api/[controller]" and Get-Unique silently eats four
    # of them -- 21 real endpoints collapsed the first time this ran unexpanded.
    if ($l.Trim() -match '^public (?:sealed )?class (\w+)') { $controller = $Matches[1] -replace 'Controller$', '' }
  }
  $classRoute = $classRoute -replace '\[controller\]', $controller
  # Class-level authorization is the default for every action in the file.
  # Method-level attributes refine it -- including [AllowAnonymous], which is how
  # AgentInventoryController.prolog is correctly reported Anonymous despite the
  # class-level agent:scope policy (pre-auth fleet handshake, by explicit design).
  foreach ($l in $lines) {
    $t = $l.Trim()
    if ($t -match '^\[Authorize\]$') { $classAuth = 'Auth'; break }
    if ($t -match '^\[Authorize\(Policy = "([^"]+)"\)\]') { $classAuth = "Auth [$($Matches[1])]"; break }
    if ($t -match '^public (?:sealed )?class ') { break }
  }
  # Walk methods: an action = run of [attr] lines immediately above a member line.
  # Both async (Task<IActionResult>) and sync (IActionResult) actions count --
  # metadata endpoints (fields/operators/action-types) are sync and were missed
  # entirely by an async-only pattern once already.
  for ($i = 0; $i -lt $lines.Count; $i++) {
    $t = $lines[$i].Trim()
    if ($t -match '^public (?:async Task<IActionResult>|Task<IActionResult>|IActionResult|async Task<ActionResult<[^>]+>|Task<ActionResult<[^>]+>|ActionResult<[^>]+>) (\w+)\(') {
      $action = $Matches[1]
      $http = ''; $sub = ''; $perm = ''; $anon = $false; $methodAuth = ''; $rate = ''
      for ($j = $i - 1; $j -ge 0; $j--) {
        $a = $lines[$j].Trim()
        if ($a -eq '' -or $a.StartsWith('//')) { continue }
        if (-not $a.StartsWith('[')) { break }
        if ($a -match '^\[Http(Get|Post|Put|Delete|Patch)(?:\("([^"]*)"\))?\]') {
          $http = $Matches[1].ToUpper(); $sub = $Matches[2]
        }
        if ($a -match '^\[RequirePermission\("([^"]+)"\)\]') { $perm = $Matches[1]; $methodAuth = 'Auth' }
        if ($a -match '^\[Authorize\]$') { $methodAuth = 'Auth' }
        if ($a -match '^\[Authorize\(Policy = "([^"]+)"\)\]') { $methodAuth = "Auth [$($Matches[1])]" }
        if ($a -match '^\[AllowAnonymous\]') { $anon = $true }
        if ($a -match 'EnableRateLimiting\("([^"]+)"\)') { $rate = $Matches[1] }
      }
      if ($http -ne '') {
        $full = ($classRoute + '/' + $sub).Trim('/') -replace '//+', '/'
        # Method-level auth wins; otherwise the class default applies; otherwise the
        # endpoint is genuinely anonymous (login/register/token only -- anything else
        # showing Anonymous here is a missing [Authorize] until proven otherwise).
        $auth = if ($anon) { 'Anonymous' } elseif ($perm -ne '' -or $methodAuth -ne '') { 'Auth' } else { $classAuth }
        if ($auth -eq '') { $auth = 'Anonymous' }
        if ($perm -ne '') { $auth += " + $perm" }
        if ($rate -ne '') { $auth += " [rate:$rate]" }
        $out += "$mod | $http | /$full | $auth | $action"
      }
    }
  }
}
$out | Sort-Object | Get-Unique | Out-File "$env:TEMP/opencode-endpoints.txt"
$out.Count
