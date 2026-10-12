<#
  Acompanha o Deceive original (molenzwiebel/Deceive) e traz as correções dele para este fork.

  Por que isso importa: quando a Riot muda alguma coisa, o app para de funcionar até alguém corrigir.
  Quem costuma corrigir primeiro é o autor do Deceive. Este script mostra o que ele mudou e,
  se você quiser, aplica essas mudanças numa branch separada para você revisar e testar.

  Uso (na pasta do repositório):
    powershell -ExecutionPolicy Bypass -File tools/sync-upstream.ps1             # só mostra o que há de novo
    powershell -ExecutionPolicy Bypass -File tools/sync-upstream.ps1 -Aplicar    # aplica numa branch "sync-upstream-AAAA-MM-DD"

  Depois de -Aplicar:
    1. Resolva qualquer conflito indicado (arquivos com <<<<<<<).
    2. Compile e teste: dotnet build Deceive/Deceive.csproj -c Release
    3. Faça o commit e publique uma nova versão.

  O arquivo tools/upstream-base.txt guarda até qual commit do original este fork já está atualizado.
  README, LICENSE e a pasta .github do original são ignorados de propósito (o fork tem os seus próprios).
#>
param(
    [switch]$Aplicar
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$arquivoBase = Join-Path $PSScriptRoot "upstream-base.txt"
$base = (Get-Content $arquivoBase -Raw).Trim()
$ignorar = @(":(exclude)README.md", ":(exclude)LICENSE", ":(exclude).github")

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Host "O git não está instalado." -ForegroundColor Red
    exit 1
}

Push-Location $repo
try {
    if (-not ((git remote) -contains "upstream")) {
        git remote add upstream https://github.com/molenzwiebel/Deceive.git
    }
    git fetch upstream --quiet
    $atual = (git rev-parse upstream/master).Trim()

    if ($atual -eq $base) {
        Write-Host "Em dia: o Deceive original não tem nada novo desde $($base.Substring(0, 7))." -ForegroundColor Green
        return
    }

    Write-Host ""
    Write-Host "Novidades no Deceive original (desde $($base.Substring(0, 7))):" -ForegroundColor Yellow
    git log --format="  %h  %ad  %s" --date=short "$base..upstream/master"
    Write-Host ""
    Write-Host "Arquivos alterados:" -ForegroundColor Yellow
    git diff --stat $base upstream/master -- . @ignorar
    Write-Host ""

    if (-not $Aplicar) {
        Write-Host "Para aplicar essas mudanças numa branch separada, rode de novo com -Aplicar." -ForegroundColor Cyan
        return
    }

    if (git status --porcelain) {
        Write-Host "Há alterações não salvas neste repositório. Faça commit ou guarde-as antes de aplicar." -ForegroundColor Red
        exit 1
    }

    $branch = "sync-upstream-" + (Get-Date).ToString("yyyy-MM-dd")
    git checkout -b $branch
    $patch = Join-Path ([IO.Path]::GetTempPath()) "upstream-sync.patch"
    # --binary inclui imagens; --output deixa o git gravar o arquivo (sem mexer em acentos).
    git diff $base upstream/master --binary --output="$patch" -- . @ignorar
    # Mudanças em arquivos que o fork também alterou viram conflitos para você resolver.
    git apply --3way --whitespace=nowarn $patch
    $ok = ($LASTEXITCODE -eq 0)

    Set-Content -Path $arquivoBase -Value $atual -Encoding ascii
    Write-Host ""
    if ($ok) {
        Write-Host "Mudanças aplicadas sem conflito na branch '$branch'." -ForegroundColor Green
    } else {
        Write-Host "Algumas mudanças deram conflito na branch '$branch'. Procure por <<<<<<< nos arquivos marcados (git status)." -ForegroundColor Yellow
    }
    Write-Host "Agora compile, teste e faça o commit. A base foi atualizada para $($atual.Substring(0, 7))."
}
finally {
    Pop-Location
}
