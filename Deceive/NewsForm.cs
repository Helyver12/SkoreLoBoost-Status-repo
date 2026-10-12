using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Deceive;

/// The "Novidades" window: announcements published on the SkoreLoBoost site.
internal sealed class NewsForm : Form
{
    private static NewsForm? _instance;

    private readonly FlowLayoutPanel _list;
    private readonly Label _status;

    /// Opens the window, or brings the existing one to the front.
    internal static void ShowOrFocus()
    {
        if (_instance is null || _instance.IsDisposed)
            _instance = new NewsForm();

        _instance.Show();
        _instance.WindowState = FormWindowState.Normal;
        _instance.Activate();
    }

    private NewsForm()
    {
        Text = "Novidades - " + StartupHandler.DeceiveTitle;
        Icon = Properties.Resources.DeceiveIcon;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9.75F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(480, 560);

        var header = new PictureBox
        {
            Image = Theme.LoadLogo(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Bounds = new Rectangle(0, 6, ClientSize.Width, 52)
        };

        var title = new Label
        {
            Text = "Novidades",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Theme.Gold,
            AutoSize = true,
            Location = new Point(16, 66)
        };

        _status = new Label
        {
            Text = "Carregando...",
            ForeColor = Theme.Muted,
            AutoSize = true,
            Location = new Point(18, 98)
        };

        _list = new FlowLayoutPanel
        {
            Bounds = new Rectangle(12, 124, ClientSize.Width - 24, ClientSize.Height - 124 - 60),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Theme.Bg
        };

        var siteButton = new Button { Text = "Site", Bounds = new Rectangle(12, ClientSize.Height - 48, 140, 36) };
        siteButton.Click += (_, _) => Process.Start(Branding.SiteUrl);
        Theme.StyleButton(siteButton);

        var discordButton = new Button { Text = "Discord", Bounds = new Rectangle(160, ClientSize.Height - 48, 140, 36) };
        discordButton.Click += (_, _) => Process.Start(Branding.DiscordUrl);
        Theme.StyleButton(discordButton, primary: true);

        var closeButton = new Button { Text = "Fechar", Bounds = new Rectangle(ClientSize.Width - 152, ClientSize.Height - 48, 140, 36) };
        closeButton.Click += (_, _) => Close();
        Theme.StyleButton(closeButton);

        Controls.AddRange(new Control[] { header, title, _status, _list, siteButton, discordButton, closeButton });
        Shown += async (_, _) => await RefreshAsync();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.UseDarkTitleBar(this);
    }

    private async System.Threading.Tasks.Task RefreshAsync()
    {
        var (items, online) = await NewsService.FetchAsync();
        if (IsDisposed)
            return;

        _status.Text = online
            ? "Atualizado agora."
            : items.Count > 0
                ? "Sem conexão com o site. Mostrando as últimas novidades salvas."
                : "Sem conexão com o site e nenhuma novidade salva ainda.";

        Render(items);

        if (items.Count > 0)
            Persistence.SetLastSeenNewsId(items[0].Id);
    }

    private void Render(List<NewsItem> items)
    {
        _list.SuspendLayout();
        _list.Controls.Clear();

        var cardWidth = _list.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4;
        if (items.Count == 0)
        {
            _list.Controls.Add(new Label
            {
                Text = "Nada por aqui por enquanto. Volte mais tarde ou entre no nosso Discord.",
                ForeColor = Theme.Muted,
                AutoSize = true,
                MaximumSize = new Size(cardWidth, 0),
                Margin = new Padding(6)
            });
        }

        foreach (var item in items)
            _list.Controls.Add(BuildCard(item, cardWidth));

        _list.ResumeLayout(true);
    }

    private static Control BuildCard(NewsItem item, int width)
    {
        var card = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Width = width,
            BackColor = Theme.Panel,
            Padding = new Padding(16, 10, 10, 12),
            Margin = new Padding(0, 0, 0, 10)
        };
        card.Paint += (_, e) =>
        {
            using var brush = new SolidBrush(Theme.Gold);
            e.Graphics.FillRectangle(brush, 0, 0, 3, card.Height);
        };

        var textWidth = width - 40;
        card.Controls.Add(new Label
        {
            Text = item.Title,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Theme.Text,
            AutoSize = true,
            MaximumSize = new Size(textWidth, 0)
        });

        if (item.Date.Length > 0)
            card.Controls.Add(new Label { Text = item.Date, ForeColor = Theme.Muted, AutoSize = true, Margin = new Padding(3, 0, 3, 4) });

        if (item.Text.Length > 0)
            card.Controls.Add(new Label
            {
                Text = item.Text,
                ForeColor = Color.FromArgb(200, 200, 210),
                AutoSize = true,
                MaximumSize = new Size(textWidth, 0)
            });

        if (item.Url is not null)
        {
            var url = item.Url;
            var button = new Button { Text = item.ButtonText, Size = new Size(150, 32), Margin = new Padding(3, 8, 3, 0) };
            button.Click += (_, _) =>
            {
                if (NewsService.IsAllowedUrl(url))
                    Process.Start(url);
            };
            Theme.StyleButton(button, primary: true);
            card.Controls.Add(button);
        }

        return card;
    }
}
