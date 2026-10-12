using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Deceive;

/// The "Agendar horários" window: lets the user pick days of the week and a time window
/// during which the app automatically switches to offline/mobile, and what to go back to
/// outside that window.
internal sealed class ScheduleForm : Form
{
    private static readonly string[] DayLabels = { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb" };

    private readonly CheckBox _enabledBox;
    private readonly CheckBox[] _dayBoxes = new CheckBox[7];
    private readonly DateTimePicker _startPicker;
    private readonly DateTimePicker _endPicker;
    private readonly ComboBox _windowStatusBox;
    private readonly ComboBox _outsideStatusBox;
    private readonly Schedule _schedule;

    internal ScheduleForm(Schedule current)
    {
        _schedule = current;

        Text = "Agendar horários - " + StartupHandler.DeceiveTitle;
        Icon = Properties.Resources.DeceiveIcon;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9.5F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 410);

        var title = new Label
        {
            Text = "Agendar horários",
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = Theme.Gold,
            AutoSize = true,
            Location = new Point(20, 16)
        };

        _enabledBox = new CheckBox
        {
            Text = "Ativar agendamento",
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(22, 54),
            Checked = current.Enabled
        };

        var daysLabel = new Label { Text = "Dias da semana", ForeColor = Theme.Muted, AutoSize = true, Location = new Point(22, 88) };
        for (var i = 0; i < 7; i++)
        {
            _dayBoxes[i] = new CheckBox
            {
                Text = DayLabels[i],
                ForeColor = Theme.Text,
                AutoSize = true,
                Location = new Point(22 + i * 52, 110),
                Checked = current.Days[i]
            };
        }

        var windowLabel = new Label { Text = "Durante esse horário, aparecer como:", ForeColor = Theme.Muted, AutoSize = true, Location = new Point(22, 150) };
        _windowStatusBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(22, 172), Width = 150, BackColor = Theme.Panel, ForeColor = Theme.Text };
        _windowStatusBox.Items.AddRange(new object[] { "Offline", "Mobile" });
        _windowStatusBox.SelectedIndex = current.WindowStatus == "mobile" ? 1 : 0;

        var fromLabel = new Label { Text = "De", ForeColor = Theme.Muted, AutoSize = true, Location = new Point(22, 212) };
        _startPicker = new DateTimePicker { Format = DateTimePickerFormat.Time, ShowUpDown = true, Location = new Point(50, 208), Width = 100 };
        _startPicker.Value = DateTime.Today.Add(current.StartTime);

        var toLabel = new Label { Text = "até", ForeColor = Theme.Muted, AutoSize = true, Location = new Point(165, 212) };
        _endPicker = new DateTimePicker { Format = DateTimePickerFormat.Time, ShowUpDown = true, Location = new Point(195, 208), Width = 100 };
        _endPicker.Value = DateTime.Today.Add(current.EndTime);

        var outsideLabel = new Label { Text = "Fora desse horário, voltar a aparecer como:", ForeColor = Theme.Muted, AutoSize = true, Location = new Point(22, 252) };
        _outsideStatusBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(22, 274), Width = 150, BackColor = Theme.Panel, ForeColor = Theme.Text };
        _outsideStatusBox.Items.AddRange(new object[] { "Online", "Offline" });
        _outsideStatusBox.SelectedIndex = current.OutsideStatus == "offline" ? 1 : 0;

        var saveButton = new Button { Text = "Salvar", Bounds = new Rectangle(ClientSize.Width - 240, ClientSize.Height - 46, 110, 34) };
        saveButton.Click += OnSave;
        Theme.StyleButton(saveButton, primary: true);

        var cancelButton = new Button { Text = "Cancelar", Bounds = new Rectangle(ClientSize.Width - 120, ClientSize.Height - 46, 100, 34) };
        cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        Theme.StyleButton(cancelButton);

        Controls.AddRange(new Control[]
        {
            title, _enabledBox, daysLabel, windowLabel, _windowStatusBox,
            fromLabel, _startPicker, toLabel, _endPicker, outsideLabel, _outsideStatusBox,
            saveButton, cancelButton
        });
        Controls.AddRange(_dayBoxes);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.UseDarkTitleBar(this);
    }

    private void OnSave(object? sender, EventArgs e)
    {
        _schedule.Enabled = _enabledBox.Checked;
        for (var i = 0; i < 7; i++)
            _schedule.Days[i] = _dayBoxes[i].Checked;
        _schedule.StartTime = _startPicker.Value.TimeOfDay;
        _schedule.EndTime = _endPicker.Value.TimeOfDay;
        _schedule.WindowStatus = _windowStatusBox.SelectedIndex == 1 ? "mobile" : "offline";
        _schedule.OutsideStatus = _outsideStatusBox.SelectedIndex == 1 ? "offline" : "chat";

        if (_schedule.Enabled && !Array.Exists(_schedule.Days, d => d))
        {
            MessageBox.Show(
                "Escolha pelo menos um dia da semana para o agendamento funcionar.",
                StartupHandler.DeceiveTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            return;
        }

        _schedule.Save();
        DialogResult = DialogResult.OK;
        Close();
    }
}
