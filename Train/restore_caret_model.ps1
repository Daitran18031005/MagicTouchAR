param([switch]$Restore)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$reportPath = Join-Path $projectRoot 'Logs/caret-training-20261021/report.json'
$baselinePath = Join-Path $projectRoot 'Logs/caret-training-20261021/original.onnx'
$modelPath = Join-Path $projectRoot 'Assets/Models/magic_touch_model.onnx'
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
$baselineHash = (Get-FileHash -LiteralPath $baselinePath -Algorithm SHA256).Hash.ToLowerInvariant()
$currentHash = (Get-FileHash -LiteralPath $modelPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($baselineHash -ne $report.source_sha256) { throw 'Backup hash mismatch. No files changed.' }
if ($currentHash -eq $report.source_sha256) { Write-Output 'Original model is already active.'; exit 0 }
if ($currentHash -ne $report.candidate_sha256) { throw 'Model changed after this training run. Refusing to overwrite it.' }
if (-not $Restore) { Write-Output 'Backup verified. Run this script with -Restore to restore only the original ONNX model.'; exit 0 }
Copy-Item -LiteralPath $baselinePath -Destination $modelPath
Write-Output 'Original ONNX restored. Unity will reimport it; stop/start Play to reload workers. Sample saving and E/O geometry checks remain enabled.'
