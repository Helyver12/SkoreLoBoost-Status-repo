<#
  Estatísticas do SkoreLoBoost Status (não faz parte do app).

  Mostra:
    - downloads de cada arquivo de cada Release no GitHub
    - membros e online do servidor do Discord (pelo convite público)

  Uso:
    powershell -ExecutionPolicy Bypass -File stats.ps1
    powershell -ExecutionPolicy Bypass -File stats.ps1 -Salvar    # também guarda um registro em stats-historico.csv

  Só lê dados públicos. Não precisa de login nem de token.
#>
param(
    [string]$Repo = "Helyver12/SkoreLoBoost-Status-repo",
    [string]$DiscordInvite = "vFp2YRZFCy",
    [switch]$Salvar
)

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$headers = @{ "User-Agent" = "SkoreLoBoost-Stats"; "Accept" = "application/vnd.github+json" }

Write-Host ""
Write-Host "=== SkoreLoBoost Status - estatísticas ===" -ForegroundColor Yellow
Write-Host ("Gerado em {0:dd/MM/yyyy HH:mm}" -f (Get-Date))
Write-Host ""

# ---- Downloads (GitHub Releases) ----
$totalDownloads = $null
try {
    $releases = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases?per_page=100" -Headers $headers
    $rows = @()
    foreach ($release in $releases) {
        foreach ($asset in $release.assets) {
            $rows += [pscustomobject]@{
                Versao    = $release.tag_name
                Publicado = ([datetime]$release.published_at).ToString("dd/MM/yyyy")
                Arquivo   = $asset.name
                Downloads = [int]$asset.download_count
            }
        }
    }

    Write-Host "Downloads por versão" -ForegroundColor Cyan
    if ($rows.Count -eq 0) {
        Write-Host "  Nenhum arquivo anexado em Releases ainda."
        $totalDownloads = 0
    }
    else {
        $rows | Format-Table -AutoSize | Out-String | Write-Host
        $totalDownloads = ($rows | Measure-Object -Property Downloads -Sum).Sum
        Write-Host ("  Total de downloads: {0}" -f $totalDownloads) -ForegroundColor Green
        Write-Host "  (inclui downloads repetidos e alguns robôs; o ZIP do código-fonte não é contado)"
    }
}
catch {
    Write-Host "Não foi possível ler as Releases: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# ---- Discord ----
$members = $null
$online = $null
try {
    $invite = Invoke-RestMethod -Uri "https://discord.com/api/v9/invites/$DiscordInvite`?with_counts=true" -Headers @{ "User-Agent" = "SkoreLoBoost-Stats" }
    $members = [int]$invite.approximate_member_count
    $online = [int]$invite.approximate_presence_count
    Write-Host "Discord" -ForegroundColor Cyan
    Write-Host ("  Servidor: {0}" -f $invite.guild.name)
    Write-Host ("  Membros:  {0}" -f $members) -ForegroundColor Green
    Write-Host ("  Online:   {0}" -f $online)
}
catch {
    Write-Host "Não foi possível ler os dados do Discord: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# ---- Histórico opcional ----
if ($Salvar) {
    $file = Join-Path $PSScriptRoot "stats-historico.csv"
    $line = [pscustomobject]@{
        Data            = (Get-Date).ToString("yyyy-MM-dd HH:mm")
        TotalDownloads  = $totalDownloads
        MembrosDiscord  = $members
        OnlineDiscord   = $online
    }
    $line | Export-Csv -Path $file -Append -NoTypeInformation -Encoding UTF8
    Write-Host "Registro salvo em $file" -ForegroundColor DarkGray
}
