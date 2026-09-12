param(
    [string]$Python = 'python',
    [string]$RunId = '',
    [string]$EnvironmentExe = '',
    [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
$hiveProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$hiveConfig = Join-Path $hiveProject 'Assets/_Project/ML/Configs/hive_ppo.yaml'
& $Python -c "import sys, torch, importlib.metadata as m; assert sys.version_info[:2] == (3,10), 'Use Python 3.10'; assert m.version('mlagents') == '1.1.0'; assert torch.cuda.is_available(), 'CUDA PyTorch is required'; print('Ready:', torch.__version__, torch.cuda.get_device_name(0))"
if ($LASTEXITCODE -ne 0) { throw 'Hive GPU prerequisites failed.' }
if ($CheckOnly) { return }
if (-not $RunId) { throw 'Provide a unique -RunId to start training. Existing runs are never overwritten.' }
$hiveResults = Join-Path $hiveProject 'TrainingResults/Hive'
if (Test-Path -LiteralPath (Join-Path $hiveResults $RunId)) { throw 'RunId already exists. Choose a new RunId.' }
$hiveArgs = @('-m','mlagents.trainers.learn',$hiveConfig,'--run-id',$RunId,'--results-dir',$hiveResults,'--torch-device','cuda')
if ($EnvironmentExe) { $hiveArgs += @('--env',$EnvironmentExe,'--no-graphics') }
else { Write-Host 'Open Assets/_Project/Scenes/HiveTraining.unity and press Play when the trainer is listening.' }
& $Python @hiveArgs
if ($LASTEXITCODE -ne 0) { throw 'Training exited with an error. Existing checkpoints are preserved.' }
