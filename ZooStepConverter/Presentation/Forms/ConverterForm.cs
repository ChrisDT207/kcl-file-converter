using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using ZooStepConverter.BusinessLogic.Common;
using ZooStepConverter.BusinessLogic.Services;
using ZooStepConverter.DataAccess;

namespace ZooStepConverter.Presentation.Forms
{
    /// <summary>
    /// Presentation Layer: Windows Forms UI for the Zoo KCL to STEP Converter.
    /// </summary>
    public class ConverterForm : Form
    {
        private TextBox txtFolder;
        private Button btnBrowse;
        private Button btnConvert;
        private Button btnCancel;
        private Button btnLogin;
        private Button btnCheck;
        private Button btnOpenFolder;
        private Button btnUpdateCli;
        private ComboBox cmbMode;
        private CheckBox chkAutoFallback;
        private ProgressBar progressBar;
        private TextBox txtLog;
        private Label lblStatus;

        private string apiToken;
        private string lastOutputDir;
        private volatile bool cancelRequested = false;

        // Service dependencies
        private readonly ZooProcessRunner processRunner;
        private readonly AuthService authService;
        private readonly CliUpdateService cliUpdateService;
        private readonly StepConversionService conversionService;

        public ConverterForm(string startFolder)
        {
            // Initialize underlying services
            processRunner = new ZooProcessRunner();
            authService = new AuthService(processRunner);
            cliUpdateService = new CliUpdateService(processRunner);
            conversionService = new StepConversionService(processRunner);

            Text = "Zoo KCL → STEP Converter (Zoo Studio Edition)";
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(740, 520);
            MinimumSize = new Size(640, 420);
            AllowDrop = true;
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;

            var lblFolder = new Label();
            lblFolder.Text = "Folder with .kcl files (or drag a folder onto this window):";
            lblFolder.SetBounds(12, 10, 600, 18);
            Controls.Add(lblFolder);

            txtFolder = new TextBox();
            txtFolder.SetBounds(12, 30, 620, 24);
            txtFolder.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            if (!string.IsNullOrEmpty(startFolder)) txtFolder.Text = startFolder;
            Controls.Add(txtFolder);

            btnBrowse = new Button();
            btnBrowse.Text = "Browse…";
            btnBrowse.SetBounds(640, 29, 88, 26);
            btnBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnBrowse.Click += OnBrowse;
            Controls.Add(btnBrowse);

            // Action buttons row
            btnConvert = new Button();
            btnConvert.Text = "Convert to STEP";
            btnConvert.SetBounds(12, 62, 120, 28);
            btnConvert.Click += OnConvert;
            Controls.Add(btnConvert);

            btnCancel = new Button();
            btnCancel.Text = "Cancel";
            btnCancel.SetBounds(138, 62, 70, 28);
            btnCancel.Enabled = false;
            btnCancel.Click += OnCancel;
            Controls.Add(btnCancel);

            btnLogin = new Button();
            btnLogin.Text = "Login with Zoo";
            btnLogin.SetBounds(214, 62, 105, 28);
            btnLogin.Click += OnLogin;
            Controls.Add(btnLogin);

            btnCheck = new Button();
            btnCheck.Text = "Check Account";
            btnCheck.SetBounds(325, 62, 105, 28);
            btnCheck.Click += OnCheckAccount;
            Controls.Add(btnCheck);

            btnUpdateCli = new Button();
            btnUpdateCli.Text = "Update CLI";
            btnUpdateCli.SetBounds(436, 62, 90, 28);
            btnUpdateCli.Click += OnUpdateCli;
            Controls.Add(btnUpdateCli);

            btnOpenFolder = new Button();
            btnOpenFolder.Text = "Open Output";
            btnOpenFolder.SetBounds(532, 62, 95, 28);
            btnOpenFolder.Enabled = false;
            btnOpenFolder.Click += OnOpenFolder;
            Controls.Add(btnOpenFolder);

            // Mode and Fallback options row
            var lblMode = new Label();
            lblMode.Text = "Mode:";
            lblMode.SetBounds(12, 100, 42, 20);
            Controls.Add(lblMode);

            cmbMode = new ComboBox();
            cmbMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbMode.Items.Add("All .kcl files");
            cmbMode.Items.Add("Assembly only (main.kcl)");
            cmbMode.Items.Add("Individual parts only (skip main.kcl)");
            cmbMode.SelectedIndex = 0;
            cmbMode.SetBounds(58, 97, 230, 24);
            Controls.Add(cmbMode);

            chkAutoFallback = new CheckBox();
            chkAutoFallback.Text = "Auto-fallback for KCL 3.0 on older CLIs";
            chkAutoFallback.Checked = true;
            chkAutoFallback.SetBounds(300, 98, 260, 22);
            Controls.Add(chkAutoFallback);

            lblStatus = new Label();
            lblStatus.Text = "";
            lblStatus.TextAlign = ContentAlignment.MiddleRight;
            lblStatus.SetBounds(560, 98, 168, 20);
            lblStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Controls.Add(lblStatus);

            // Progress bar
            progressBar = new ProgressBar();
            progressBar.SetBounds(12, 128, 716, 12);
            progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(progressBar);

            // Log window
            txtLog = new TextBox();
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.BackColor = Color.White;
            txtLog.Font = new Font("Consolas", 9F);
            txtLog.SetBounds(12, 146, 716, 362);
            txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            Controls.Add(txtLog);

            apiToken = TokenRepository.LoadToken();
            Shown += delegate { StartThread(Preflight); };
        }

        // ---------- UI events ----------

        private void OnBrowse(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Pick the folder that contains your Zoo Studio .kcl files";
                if (Directory.Exists(txtFolder.Text)) dlg.SelectedPath = txtFolder.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK) txtFolder.Text = dlg.SelectedPath;
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (paths == null || paths.Length == 0) return;
            string p = paths[0];
            if (File.Exists(p)) p = Path.GetDirectoryName(p);
            if (Directory.Exists(p)) txtFolder.Text = p;
        }

        private void OnLogin(object sender, EventArgs e)
        {
            StartThread(delegate
            {
                authService.OAuthLogin(
                    Log,
                    SetStatus,
                    delegate(string newToken) { apiToken = newToken; },
                    Preflight);
            });
        }

        private void OnCheckAccount(object sender, EventArgs e)
        {
            StartThread(Preflight);
        }

        private void OnUpdateCli(object sender, EventArgs e)
        {
            StartThread(delegate
            {
                cliUpdateService.UpdateCli(apiToken, Log, Preflight);
            });
        }

        private void OnOpenFolder(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(lastOutputDir) && Directory.Exists(lastOutputDir))
            {
                Process.Start("explorer.exe", lastOutputDir);
            }
        }

        private void OnCancel(object sender, EventArgs e)
        {
            cancelRequested = true;
            Log("[!] Cancelling conversion...");
            btnCancel.Enabled = false;
        }

        private void OnConvert(object sender, EventArgs e)
        {
            string folder = txtFolder.Text.Trim().Trim('"');
            if (!Directory.Exists(folder))
            {
                MessageBox.Show(this, "That folder does not exist. Pick the folder that contains your .kcl files.",
                    "Zoo Converter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            cancelRequested = false;
            var mode = (ConversionMode)cmbMode.SelectedIndex;
            bool fallback = chkAutoFallback.Checked;
            StartThread(delegate
            {
                conversionService.ConvertFolder(
                    folder,
                    mode,
                    fallback,
                    apiToken,
                    delegate { return cancelRequested; },
                    Log,
                    UpdateProgress,
                    delegate(string outDir) { lastOutputDir = outDir; },
                    delegate { EnableOpenFolder(true); });
            });
        }

        // ---------- Background Work & Helpers ----------

        private void Preflight()
        {
            authService.Preflight(apiToken, Log, SetStatus);
        }

        private void StartThread(ThreadStart work)
        {
            SetBusy(true);
            var t = new Thread(delegate()
            {
                try { work(); }
                catch (Exception ex) { Log("[ERROR] " + ex.Message); }
                finally { SetBusy(false); }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void Log(string msg)
        {
            if (txtLog.InvokeRequired) { txtLog.BeginInvoke((Action)delegate { Log(msg); }); return; }
            txtLog.AppendText(msg + Environment.NewLine);
        }

        private void SetStatus(string text, Color color)
        {
            if (lblStatus.InvokeRequired) { lblStatus.BeginInvoke((Action)delegate { SetStatus(text, color); }); return; }
            lblStatus.Text = text;
            lblStatus.ForeColor = color;
        }

        private void UpdateProgress(int current, int total)
        {
            if (progressBar.InvokeRequired)
            {
                progressBar.BeginInvoke((Action)delegate { UpdateProgress(current, total); });
                return;
            }
            progressBar.Maximum = total > 0 ? total : 1;
            progressBar.Value = Math.Min(current, progressBar.Maximum);
        }

        private void EnableOpenFolder(bool enable)
        {
            if (btnOpenFolder.InvokeRequired)
            {
                btnOpenFolder.BeginInvoke((Action)delegate { EnableOpenFolder(enable); });
                return;
            }
            btnOpenFolder.Enabled = enable;
        }

        private void SetBusy(bool busy)
        {
            if (InvokeRequired) { BeginInvoke((Action)delegate { SetBusy(busy); }); return; }
            btnConvert.Enabled = !busy;
            btnCancel.Enabled = busy;
            btnCheck.Enabled = !busy;
            btnBrowse.Enabled = !busy;
            btnLogin.Enabled = !busy;
            btnUpdateCli.Enabled = !busy;
            cmbMode.Enabled = !busy;
            chkAutoFallback.Enabled = !busy;
            UseWaitCursor = busy;
        }
    }
}

