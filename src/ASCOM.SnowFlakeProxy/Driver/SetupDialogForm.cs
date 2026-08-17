using System;
using System.Drawing;
using System.Windows.Forms;

namespace ASCOM.SnowFlakeProxy
{
    public class SetupDialogForm : Form
    {
        private readonly ProxySettings settings;
        private readonly SnowflakeProxyController controller;
        private CheckBox chk_normalize;
        private CheckBox chk_trace;
        private TextBox txt_move_timeout;
        private TextBox txt_retry_delay;
        private Button cmd_ok;
        private Button cmd_cancel;
        private Button cmd_vendor_setup;
        private Label lbl_title;
        private Label lbl_prog_id;
        private Label lbl_move_timeout;
        private Label lbl_retry_delay;
        private Label lbl_vendor_status;

        internal SetupDialogForm(ProxySettings settings, SnowflakeProxyController controller)
        {
            this.settings = settings;
            this.controller = controller;
            InitializeComponent();
            LoadValues();
        }

        private void InitializeComponent()
        {
            chk_normalize = new CheckBox();
            chk_trace = new CheckBox();
            txt_move_timeout = new TextBox();
            txt_retry_delay = new TextBox();
            cmd_ok = new Button();
            cmd_cancel = new Button();
            cmd_vendor_setup = new Button();
            lbl_title = new Label();
            lbl_prog_id = new Label();
            lbl_move_timeout = new Label();
            lbl_retry_delay = new Label();
            lbl_vendor_status = new Label();
            SuspendLayout();

            lbl_title.AutoSize = true;
            lbl_title.Font = new Font(Font, FontStyle.Bold);
            lbl_title.Location = new Point(12, 12);
            lbl_title.Text = "SnowFlakeProxy";

            lbl_prog_id.AutoSize = true;
            lbl_prog_id.Location = new Point(12, 36);
            lbl_prog_id.Text = "Underlying driver: " + ProxyIdentity.VendorProgId;

            chk_normalize.AutoSize = true;
            chk_normalize.Location = new Point(12, 64);
            chk_normalize.Text = "Normalize Wanderer filter names";

            lbl_move_timeout.AutoSize = true;
            lbl_move_timeout.Location = new Point(12, 96);
            lbl_move_timeout.Text = "Move timeout (ms):";

            txt_move_timeout.Location = new Point(200, 93);
            txt_move_timeout.Width = 80;

            lbl_retry_delay.AutoSize = true;
            lbl_retry_delay.Location = new Point(12, 128);
            lbl_retry_delay.Text = "Stale-position retry delay (ms):";

            txt_retry_delay.Location = new Point(200, 125);
            txt_retry_delay.Width = 80;

            chk_trace.AutoSize = true;
            chk_trace.Location = new Point(12, 160);
            chk_trace.Text = "Trace logging";

            cmd_vendor_setup.Location = new Point(12, 192);
            cmd_vendor_setup.Size = new Size(180, 28);
            cmd_vendor_setup.Text = "Open Wanderer Setup...";
            cmd_vendor_setup.Click += CmdVendorSetup_Click;

            lbl_vendor_status.AutoSize = true;
            lbl_vendor_status.Location = new Point(200, 198);
            lbl_vendor_status.MaximumSize = new Size(220, 40);

            cmd_ok.DialogResult = DialogResult.OK;
            cmd_ok.Location = new Point(248, 240);
            cmd_ok.Size = new Size(75, 28);
            cmd_ok.Text = "OK";
            cmd_ok.Click += CmdOk_Click;

            cmd_cancel.DialogResult = DialogResult.Cancel;
            cmd_cancel.Location = new Point(329, 240);
            cmd_cancel.Size = new Size(75, 28);
            cmd_cancel.Text = "Cancel";

            ClientSize = new Size(420, 280);
            Controls.Add(lbl_title);
            Controls.Add(lbl_prog_id);
            Controls.Add(chk_normalize);
            Controls.Add(lbl_move_timeout);
            Controls.Add(txt_move_timeout);
            Controls.Add(lbl_retry_delay);
            Controls.Add(txt_retry_delay);
            Controls.Add(chk_trace);
            Controls.Add(cmd_vendor_setup);
            Controls.Add(lbl_vendor_status);
            Controls.Add(cmd_ok);
            Controls.Add(cmd_cancel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "SetupDialogForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Wanderer Snowflake Filter Wheel 1 (Proxy) Setup";
            AcceptButton = cmd_ok;
            CancelButton = cmd_cancel;
            ResumeLayout(false);
            PerformLayout();
        }

        private void LoadValues()
        {
            chk_normalize.Checked = settings.normalize_filter_names;
            chk_trace.Checked = settings.trace_enabled;
            txt_move_timeout.Text = settings.move_timeout_ms.ToString();
            txt_retry_delay.Text = settings.position_retry_delay_ms.ToString();
            if (controller.IsAnyClientConnected())
            {
                cmd_vendor_setup.Enabled = false;
                lbl_vendor_status.Text = "Disconnect all clients before opening the Wanderer setup dialog.";
            }
        }

        private void CmdOk_Click(object sender, EventArgs e)
        {
            int move_timeout_ms;
            int retry_delay_ms;
            if (!int.TryParse(txt_move_timeout.Text, out move_timeout_ms) || move_timeout_ms <= 0)
            {
                MessageBox.Show("Move timeout must be a positive integer.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            if (!int.TryParse(txt_retry_delay.Text, out retry_delay_ms) || retry_delay_ms < 0)
            {
                MessageBox.Show("Retry delay must be a non-negative integer.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            settings.normalize_filter_names = chk_normalize.Checked;
            settings.trace_enabled = chk_trace.Checked;
            settings.move_timeout_ms = move_timeout_ms;
            settings.position_retry_delay_ms = retry_delay_ms;
        }

        private void CmdVendorSetup_Click(object sender, EventArgs e)
        {
            try
            {
                controller.OpenVendorSetup();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
