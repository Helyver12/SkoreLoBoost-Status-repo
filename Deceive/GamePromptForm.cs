using System;
using System.Drawing;
using System.Windows.Forms;

namespace Deceive;

internal partial class GamePromptForm : Form
{
    internal static LaunchGame SelectedGame = LaunchGame.Auto;

    internal GamePromptForm()
    {
        InitializeComponent();
        // Replace the icon baked into the designer resources with the app icon.
        Icon = Properties.Resources.DeceiveIcon;
        ApplyBranding();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.UseDarkTitleBar(this);
    }

    // SkoreLoBoost look: dark background, gold accents and the logo in a header.
    private void ApplyBranding()
    {
        const int headerHeight = 64;

        SuspendLayout();
        foreach (Control control in Controls)
            control.Top += headerHeight;
        ClientSize = new Size(ClientSize.Width, ClientSize.Height + headerHeight);

        BackColor = Theme.Bg;
        ForeColor = Theme.Text;

        var logo = Theme.LoadLogo();
        if (logo is not null)
            Controls.Add(new PictureBox
            {
                Image = logo,
                SizeMode = PictureBoxSizeMode.Zoom,
                Bounds = new Rectangle(0, 6, ClientSize.Width, headerHeight - 12)
            });

        label1.ForeColor = Theme.Gold;
        label1.Left = (ClientSize.Width - label1.PreferredSize.Width) / 2;
        checkboxRemember.ForeColor = Theme.Text;

        foreach (var button in new[] { buttonLaunchLoL, buttonLaunchLoR, buttonLaunchValorant, buttonLaunchLion, buttonLaunchTFT, buttonLaunchRiotClient })
            Theme.StyleButton(button);

        ResumeLayout(true);
    }

    private void OnFormLoad(object sender, EventArgs e) => Text = StartupHandler.DeceiveTitle;

    private void OnLoLLaunch(object sender, EventArgs e) => HandleLaunchChoiceAsync(LaunchGame.LoL);

    private void OnLoRLaunch(object sender, EventArgs e) => HandleLaunchChoiceAsync(LaunchGame.LoR);

    private void OnValorantLaunch(object sender, EventArgs e) => HandleLaunchChoiceAsync(LaunchGame.VALORANT);

    private void OnLionLaunch(object sender, EventArgs e) => HandleLaunchChoiceAsync(LaunchGame.Lion);

    private void OnTFTLaunch(object sender, EventArgs e) => HandleLaunchChoiceAsync(LaunchGame.TeamfightTactics);

    private void OnRiotClientLaunch(object sender, EventArgs e) => HandleLaunchChoiceAsync(LaunchGame.RiotClient);

    private void HandleLaunchChoiceAsync(LaunchGame game)
    {
        if (checkboxRemember.Checked)
            Persistence.SetDefaultLaunchGame(game);

        SelectedGame = game;
        DialogResult = DialogResult.OK;
    }
}
