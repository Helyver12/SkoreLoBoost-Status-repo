using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using Deceive.Properties;

namespace Deceive;

internal class MainController : ApplicationContext
{
    internal MainController()
    {
        TrayIcon = new NotifyIcon
        {
            Icon = Resources.DeceiveIcon,
            Visible = true,
            BalloonTipTitle = StartupHandler.DeceiveTitle,
            BalloonTipText = "O SkoreLoBoost Status está ocultando o seu status. Clique com o botão direito no ícone da bandeja para ver as opções."
        };
        TrayIcon.ShowBalloonTip(5000);
        TrayIcon.BalloonTipClicked += (_, _) =>
        {
            if (NewsBalloonShown)
            {
                NewsBalloonShown = false;
                NewsForm.ShowOrFocus();
            }
        };

        LoadStatus();
        UpdateTray();

        // Check the site for news a few seconds after startup, so it doesn't replace the first balloon.
        var newsTimer = new System.Windows.Forms.Timer { Interval = 12_000 };
        newsTimer.Tick += (_, _) =>
        {
            newsTimer.Stop();
            newsTimer.Dispose();
            var context = SynchronizationContext.Current;
            Task.Run(async () =>
            {
                var (items, _) = await NewsService.FetchAsync();
                context?.Post(_ => AnnounceNews(items), null);
            });
        };
        newsTimer.Start();

        // Checks every 30s whether we're crossing into or out of the scheduled window.
        var scheduleTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
        scheduleTimer.Tick += (_, _) => CheckSchedule();
        scheduleTimer.Start();
        CheckSchedule(); // also apply immediately if we're launched mid-window
    }

    private bool NewsBalloonShown { get; set; }
    private Schedule ScheduleConfig { get; set; } = Schedule.Load();
    private bool? ScheduleWasInsideWindow { get; set; }

    private async void CheckSchedule()
    {
        var isInside = ScheduleConfig.IsInsideWindow(DateTime.Now);
        if (isInside == ScheduleWasInsideWindow)
            return;

        ScheduleWasInsideWindow = isInside;
        if (!ScheduleConfig.Enabled)
            return;

        var target = isInside ? ScheduleConfig.WindowStatus : ScheduleConfig.OutsideStatus;
        await UpdateStatusAsync(Status = target);
        Enabled = true;
        UpdateTray();
    }

    private void AnnounceNews(List<NewsItem> items)
    {
        if (items.Count == 0 || items[0].Id == Persistence.GetLastSeenNewsId())
            return;

        Persistence.SetLastSeenNewsId(items[0].Id);
        NewsBalloonShown = true;
        TrayIcon.BalloonTipTitle = "Novidade: " + items[0].Title;
        TrayIcon.BalloonTipText = "Clique para ver as novidades da SkoreLoBoost.";
        TrayIcon.ShowBalloonTip(8000);
    }

    private NotifyIcon TrayIcon { get; }
    public bool Enabled { get; set; } = true;
    public string Status { get; set; } = null!;
    private string StatusFile { get; } = Path.Combine(Persistence.DataDir, "status");
    public bool ConnectToMuc { get; set; } = true;
    private bool SentIntroductionText { get; set; } = false;
    private CancellationTokenSource? ShutdownToken { get; set; } = null;

    private ToolStripMenuItem EnabledMenuItem { get; set; } = null!;
    private ToolStripMenuItem ChatStatus { get; set; } = null!;
    private ToolStripMenuItem OfflineStatus { get; set; } = null!;
    private ToolStripMenuItem MobileStatus { get; set; } = null!;

    private List<ProxiedConnection> Connections { get; } = new();

    public void StartServingClients(TcpListener server, X509Certificate2 serverCert, string chatHost, int chatPort)
    {
        Task.Run(() => ServeClientsAsync(server, serverCert, chatHost, chatPort));
    }

    private async Task ServeClientsAsync(TcpListener server, X509Certificate2 serverCert, string chatHost, int chatPort)
    {
        while (true)
        {
            try
            {
                // no need to shutdown, we received a new request
                ShutdownToken?.Cancel();
                ShutdownToken = null;

                var incoming = await server.AcceptTcpClientAsync();
                var sslIncoming = new SslStream(incoming.GetStream());
                await sslIncoming.AuthenticateAsServerAsync(serverCert);

                TcpClient outgoing;
                while (true)
                {
                    try
                    {
                        outgoing = new TcpClient(chatHost, chatPort);
                        break;
                    }
                    catch (SocketException e)
                    {
                        Trace.WriteLine(e);
                        var result = MessageBox.Show(
                            "Não foi possível conectar ao servidor de chat. Verifique a sua conexão com a internet. " +
                            "Se o problema continuar e você conseguir usar o chat normalmente sem o SkoreLoBoost Status, " +
                            "avise a gente no Discord (https://discord.gg/vFp2YRZFCy).",
                            StartupHandler.DeceiveTitle,
                            MessageBoxButtons.RetryCancel,
                            MessageBoxIcon.Error,
                            MessageBoxDefaultButton.Button1
                        );
                        if (result == DialogResult.Cancel)
                            Environment.Exit(0);
                    }
                }

                var sslOutgoing = new SslStream(outgoing.GetStream());
                await sslOutgoing.AuthenticateAsClientAsync(chatHost);

                var proxiedConnection = new ProxiedConnection(this, sslIncoming, sslOutgoing);
                proxiedConnection.Start();
                proxiedConnection.ConnectionErrored += (_, _) =>
                {
                    Trace.WriteLine("Disconnected incoming connection.");
                    Connections.Remove(proxiedConnection);

                    if (Connections.Count == 0)
                    {
                        Task.Run(ShutdownIfNoReconnect);
                    }
                };
                Connections.Add(proxiedConnection);

                if (!SentIntroductionText && !Persistence.GetHasShownIntroduction())
                {
                    SentIntroductionText = true;
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(10_000);
                        await SendIntroductionTextAsync();
                    });
                }
            } catch (Exception e)
            {
                Trace.WriteLine("Failed to handle incoming connection.");
                Trace.WriteLine(e);
            }
        }
    }

    private void UpdateTray()
    {
        var aboutMenuItem = new ToolStripMenuItem(StartupHandler.DeceiveTitle, null, (_, _) => Process.Start(Branding.SiteUrl))
        {
            Tag = "brand",
            Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold)
        };

        EnabledMenuItem = new ToolStripMenuItem("Ativado", null, async (_, _) =>
        {
            Enabled = !Enabled;
            await UpdateStatusAsync(Enabled ? Status : "chat");
            await SendMessageFromFakePlayerAsync(Enabled ? "O SkoreLoBoost Status foi ativado." : "O SkoreLoBoost Status foi desativado.");
            UpdateTray();
        })
        { Checked = Enabled };

        var mucMenuItem = new ToolStripMenuItem("Ativar chat do lobby", null, (_, _) =>
        {
            ConnectToMuc = !ConnectToMuc;
            UpdateTray();
        })
        { Checked = ConnectToMuc };

        ChatStatus = new ToolStripMenuItem("Online", null, async (_, _) =>
        {
            await UpdateStatusAsync(Status = "chat");
            Enabled = true;
            UpdateTray();
        })
        { Checked = Status.Equals("chat") };

        OfflineStatus = new ToolStripMenuItem("Offline", null, async (_, _) =>
        {
            await UpdateStatusAsync(Status = "offline");
            Enabled = true;
            UpdateTray();
        })
        { Checked = Status.Equals("offline") };

        MobileStatus = new ToolStripMenuItem("Mobile", null, async (_, _) =>
        {
            await UpdateStatusAsync(Status = "mobile");
            Enabled = true;
            UpdateTray();
        })
        { Checked = Status.Equals("mobile") };

        var typeMenuItem = new ToolStripMenuItem("Tipo de status", null, ChatStatus, OfflineStatus, MobileStatus);
        
        var currentStartup = Persistence.GetStartupStatus();
        var startupOnline = new ToolStripMenuItem("Online", null, (_, _) =>
        {
            Persistence.SetStartupStatus("chat");
            UpdateTray();
        })
        { Checked = currentStartup == "chat" };

        var startupOffline = new ToolStripMenuItem("Offline", null, (_, _) =>
        {
            Persistence.SetStartupStatus("offline");
            UpdateTray();
        })
        { Checked = currentStartup == "offline" };

        var startupMobile = new ToolStripMenuItem("Mobile", null, (_, _) =>
        {
            Persistence.SetStartupStatus("mobile");
            UpdateTray();
        })
        { Checked = currentStartup == "mobile" };

        var startupLast = new ToolStripMenuItem("Lembrar o último", null, (_, _) =>
        {
            Persistence.SetStartupStatus("last");
            UpdateTray();
        })
        { Checked = currentStartup == "last" };

        var startupStatusMenuItem = new ToolStripMenuItem("Status padrão ao iniciar", null, startupOnline, startupOffline, startupMobile, startupLast);

        var windowsStartupMenuItem = new ToolStripMenuItem("Iniciar com o Windows", null, (_, _) =>
        {
            var enable = !WindowsStartup.IsEnabled();
            WindowsStartup.SetEnabled(enable);
            if (enable)
                MessageBox.Show(
                    "O SkoreLoBoost Status vai abrir junto com o Windows e iniciar o Riot Client. " +
                    "Para não precisar escolher o jogo toda vez, marque \"Lembrar minha escolha\" na tela de seleção de jogo " +
                    "(use \"Reiniciar e abrir outro jogo\" no menu se quiser escolher de novo).",
                    StartupHandler.DeceiveTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            UpdateTray();
        })
        { Checked = WindowsStartup.IsEnabled() };

        var scheduleMenuItem = new ToolStripMenuItem("Agendar horários...", null, (_, _) =>
        {
            using var form = new ScheduleForm(ScheduleConfig);
            if (form.ShowDialog() == DialogResult.OK)
            {
                ScheduleWasInsideWindow = null; // força reavaliar e aplicar agora mesmo
                CheckSchedule();
            }
        })
        { Checked = ScheduleConfig.Enabled };

        var restartWithDifferentGameItem = new ToolStripMenuItem("Reiniciar e abrir outro jogo", null, (_, _) =>
        {
            var result = MessageBox.Show(
                "Reiniciar o SkoreLoBoost Status para abrir outro jogo? Os jogos da Riot que estiverem abertos também serão fechados.",
                StartupHandler.DeceiveTitle,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1
            );

            if (result is not DialogResult.Yes)
                return;

            Utils.KillProcesses();
            Thread.Sleep(2000);

            Persistence.SetDefaultLaunchGame(LaunchGame.Prompt);
            Process.Start(Application.ExecutablePath);
            Environment.Exit(0);
        });

        var quitMenuItem = new ToolStripMenuItem("Sair", null, (_, _) =>
        {
            var result = MessageBox.Show(
                "Tem certeza que deseja fechar o SkoreLoBoost Status? Os jogos da Riot que estiverem abertos também serão fechados.",
                StartupHandler.DeceiveTitle,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1
            );

            if (result is not DialogResult.Yes)
                return;

            Utils.KillProcesses();
            SaveStatus();
            Application.Exit();
        });

        var newsMenuItem = new ToolStripMenuItem("Novidades", null, (_, _) => NewsForm.ShowOrFocus());
        var siteMenuItem = new ToolStripMenuItem("SkoreLoBoost - Site", null, (_, _) => Process.Start(Branding.SiteUrl));
        var discordMenuItem = new ToolStripMenuItem("SkoreLoBoost - Discord", null, (_, _) => Process.Start(Branding.DiscordUrl));

        TrayIcon.ContextMenuStrip = new ContextMenuStrip { Renderer = new DarkRenderer(), BackColor = Theme.Panel, ForeColor = Theme.Text };

#if DEBUG
        var sendTestMsg = new ToolStripMenuItem("Send message", null, async (_, _) => { await SendMessageFromFakePlayerAsync("Test"); });

        TrayIcon.ContextMenuStrip.Items.AddRange(new ToolStripItem[]
        {
            aboutMenuItem, EnabledMenuItem, typeMenuItem, startupStatusMenuItem, windowsStartupMenuItem, scheduleMenuItem, mucMenuItem, sendTestMsg, restartWithDifferentGameItem, quitMenuItem
        });
#else
        TrayIcon.ContextMenuStrip.Items.AddRange(new ToolStripItem[] { aboutMenuItem, EnabledMenuItem, typeMenuItem, startupStatusMenuItem, windowsStartupMenuItem, scheduleMenuItem, mucMenuItem, restartWithDifferentGameItem, newsMenuItem, siteMenuItem, discordMenuItem, quitMenuItem });
#endif
    }

    public async Task HandleChatMessage(string content)
    {
        // Os comandos aceitam português e inglês. "desativar" precisa ser checado antes de "ativar".
        var text = content.ToLower();

        if (text.Contains("offline"))
        {
            if (!Enabled)
                await SendMessageFromFakePlayerAsync("O SkoreLoBoost Status foi ativado.");
            OfflineStatus.PerformClick();
        }
        else if (text.Contains("mobile"))
        {
            if (!Enabled)
                await SendMessageFromFakePlayerAsync("O SkoreLoBoost Status foi ativado.");
            MobileStatus.PerformClick();
        }
        else if (text.Contains("online"))
        {
            if (!Enabled)
                await SendMessageFromFakePlayerAsync("O SkoreLoBoost Status foi ativado.");
            ChatStatus.PerformClick();
        }
        else if (text.Contains("desativar") || text.Contains("disable"))
        {
            if (!Enabled)
                await SendMessageFromFakePlayerAsync("O SkoreLoBoost Status já está desativado.");
            else
                EnabledMenuItem.PerformClick();
        }
        else if (text.Contains("ativar") || text.Contains("enable"))
        {
            if (Enabled)
                await SendMessageFromFakePlayerAsync("O SkoreLoBoost Status já está ativado.");
            else
                EnabledMenuItem.PerformClick();
        }
        else if (text.Contains("status"))
        {
            await SendMessageFromFakePlayerAsync("Você está aparecendo " + (Status == "chat" ? "online" : Status) + ".");
        }
        else if (text.Contains("ajuda") || text.Contains("help"))
        {
            await SendMessageFromFakePlayerAsync("Você pode enviar estas mensagens para mudar as configurações rapidamente: online / offline / mobile / ativar / desativar / status");
        }
    }

    private async Task SendIntroductionTextAsync()
    {
        Persistence.SetHasShownIntroduction();
        await SendMessageFromFakePlayerAsync("Bem-vindo! O SkoreLoBoost Status está ativo e você está aparecendo " + (Status == "chat" ? "online" : Status) +
                                             ". Mesmo que o cliente do jogo indique outra coisa, seus amigos veem você assim, a não ser que você desative o SkoreLoBoost Status manualmente.");
        await Task.Delay(200);
        await SendMessageFromFakePlayerAsync(
            "Se quiser convidar outras pessoas enquanto está offline, talvez seja preciso desativar o SkoreLoBoost Status para elas aceitarem. Você pode ativar de novo assim que elas entrarem no seu lobby.");
        await Task.Delay(200);
        await SendMessageFromFakePlayerAsync("Para ativar, desativar ou mudar outras configurações, procure o SkoreLoBoost Status nos ícones da bandeja do Windows.");
        await Task.Delay(200);
        await SendMessageFromFakePlayerAsync("Bom jogo!");
    }

    private async Task SendMessageFromFakePlayerAsync(string message)
    {
        foreach (var connection in Connections)
            await connection.SendMessageFromFakePlayerAsync(message);
    }

    private async Task UpdateStatusAsync(string newStatus)
    {
        foreach (var connection in Connections)
            await connection.UpdateStatusAsync(newStatus);

        await SendMessageFromFakePlayerAsync("Agora você está aparecendo " + (newStatus == "chat" ? "online" : newStatus) + ".");
    }

    private void LoadStatus()
    {
        var startupStatus = Persistence.GetStartupStatus();

        if (startupStatus is "chat" or "offline" or "mobile")
        {
            Status = startupStatus;
            return;
        }

        if (!File.Exists(StatusFile))
        {
            Status = "offline";
            return;
        }

        // "last" or unrecognized: use the saved session status.
        var saved = File.ReadAllText(StatusFile);
        Status = saved switch
        {
            "chat" => "chat",
            "mobile" => "mobile",
            _ => "offline"
        };
    }

    private async Task ShutdownIfNoReconnect()
    {
        if (ShutdownToken == null)
            ShutdownToken = new CancellationTokenSource();
        await Task.Delay(60_000, ShutdownToken.Token);

        Trace.WriteLine("Received no new connections after 60s, shutting down.");
        Environment.Exit(0);
    }

    private void SaveStatus() => File.WriteAllText(StatusFile, Status);
}
