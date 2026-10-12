# SkoreLoBoost Status

Ferramenta gratuita para aparecer **offline** (ou mobile) no chat da Riot Games enquanto você joga League of Legends, Valorant, TFT e Legends of Runeterra.

- Site: https://skoreloboost.com
- Discord: https://discord.gg/vFp2YRZFCy

## Como usar
1. Baixe o `SkoreLoBoostStatus.exe` na aba **Releases** deste repositório.
2. Feche o Riot Client e abra o `SkoreLoBoostStatus.exe`.
3. Escolha o jogo. O app inicia o Riot Client por você.
4. Use o ícone da bandeja para alternar entre online, offline e mobile.

Apenas a sua presença global é filtrada. Lista de amigos, convites que você envia e chat de lobby continuam funcionando. Amigos não conseguem te convidar enquanto você aparece offline.

## Como funciona
O app inicia o Riot Client apontando o chat para um proxy local, que reescreve apenas os pacotes de presença. Nenhuma senha ou dado de conta passa por servidores nossos.

## Novidades e privacidade
O app consulta `https://skoreloboost.com/status-news.json` para mostrar a tela **Novidades** e avisar quando há algo novo. É só um arquivo de texto: o app não envia nenhum dado seu, apenas o nome e a versão do app no pedido, como qualquer navegador. Os links da tela só abrem páginas da SkoreLoBoost, do nosso Discord e deste repositório.

Para publicar uma novidade, edite esse arquivo no site seguindo o modelo em `docs/status-news.example.json`.

## Estatísticas (para o dono do projeto)
O script `tools/stats.ps1` mostra os downloads de cada Release e os membros do Discord. Ele não faz parte do app e só lê dados públicos:

```powershell
powershell -ExecutionPolicy Bypass -File tools/stats.ps1
```

Use `-Salvar` para guardar um registro em `tools/stats-historico.csv` e acompanhar o crescimento.

## Compilar
Requer Windows e o .NET SDK.

```
dotnet build Deceive/Deceive.csproj -c Release
```

A saída fica em `Deceive/bin/Release/net472/SkoreLoBoostStatus.exe`.
Se o build falhar no passo do Costura por causa do tamanho do caminho, clone o repositório em uma pasta com caminho curto (por exemplo, direto na raiz do disco).

## Créditos e licença
**SkoreLoBoost Status é um fork do [Deceive](https://github.com/molenzwiebel/Deceive)**, criado por **molenzwiebel**. Todo o crédito pela técnica e pelo código original é dele.

Este projeto é distribuído sob a **GPL-3.0** (veja `LICENSE`). O código-fonte completo está neste repositório.

### Alterações em relação ao original
- Nome, ícone, textos e links com a marca SkoreLoBoost
- Itens de menu "Site" e "Discord" na bandeja
- Interface e mensagens em português
- Aviso de nova versão apontando para as Releases deste repositório
- Opção "Iniciar com o Windows" no menu da bandeja
- Ícone da marca também na tela de escolha do jogo
- Visual próprio: tema escuro com dourado, logo na tela de jogos e menu da bandeja estilizado
- Tela **Novidades**, que mostra avisos publicados no site (veja abaixo)
- Verificação de atualização do projeto original removida

O app ainda usa o certificado e o domínio de proxy hospedados pelo autor original (`mln.cx` e `deceive-localhost.molenzwiebel.xyz`). Se eles saírem do ar, o app deixa de funcionar.

## Aviso
Este projeto não é afiliado nem endossado pela Riot Games. Ferramentas que modificam o comportamento do cliente ficam em área cinzenta dos termos da Riot. Use por sua conta e risco. O app apenas oculta o seu status e não protege contas contra nenhuma regra da Riot.