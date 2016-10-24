using System;                       
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ProjectSafe
{
    public partial class Main : Form
    {
        private const string ImDone = "不干了";
        private const string Ready = "";
        private const string NeedPassword = "需要密码";
        private const string NeedPath = "需要指定工程路径";
        private const string MoreInvalid = "还是";
        private const string InvalidPath = "非法目录";
        private const string MultiInvalid = "只支持一个目录";
        private const string Encrypting = "正在加密";
        private const string Encrypted = "加密完成";
        private const string Decrypting = "正在解密";
        private const string Decrypted = "解密完成";
        private const string Title = "秘盒 ";
        private const string Warning = "正在工作, 强制退出可能导致数据损坏且永不可恢复, 确认退出?";
        private bool _inited;

        public Main()
        {
            InitializeComponent();
            // ReSharper disable once VirtualMemberCallInConstructor
            Text = Title;
            progressBar.Maximum = 100;
        }

        private void ReportStatus(string msg, bool resume = true)
        {
            Text = Title + msg;
            if (resume)
            {
                Task.Run(async delegate
                {
                    await Task.Delay(1000);
                    SetProperty(() =>
                    {
                        Text = Title + Ready;
                        progressBar.Value = 0;
                    });
                });
            }
        }

        private bool Check()
        {
            if (!_inited)
            {
                ReportStatus(NeedPath);
                return false;
            }

            // ReSharper disable once InvertIf
            if (string.IsNullOrEmpty(textBoxPassword.Text))
            {
                ReportStatus(NeedPassword);
                return false;
            }

            return true;
        }

        private static long _total;
        private static long _current;
        private void SearchDeeper(string path, Action<string> action, bool top = true)
        {
            try
            {
                if (top)
                {
                    _current = 0;
                    _total = Directory.GetFiles(path, "*", SearchOption.AllDirectories).Sum(t => new FileInfo(t).Length);
                }

                foreach (var d in Directory.EnumerateDirectories(path))
                {
                    SearchDeeper(d, action, false);
                }

                foreach (var p in Directory.EnumerateFiles(path))
                {
                    _current += new FileInfo(p).Length;
                    SetProperty(() => { progressBar.Value = (int) (_current*100/_total); });
                    action(p);
                }

                if (!top)
                    return;
                _total = 0;
                _current = 0;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }            
        }

        private void SetProperty(Action action)
        {
            if (InvokeRequired)
            {
                Invoke(action);
            }
            else
            {
                action();
            }
        }
               
        private bool _busying;
        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_busying)
                return;
            if (MessageBox.Show(this, Warning, "", MessageBoxButtons.OKCancel) == DialogResult.Cancel)
                e.Cancel = true;
        }

        private void Busying(bool busy)
        {
            SetProperty(() =>
            {
                _busying = busy;
                buttonEncrypt.Enabled = !busy;
                buttonDecrypt.Enabled = !busy;
            });
        }

        private void BeginWork(string msg)
        {
            ReportStatus(msg, false);
            Busying(true);
            SetProperty(() => { });
        }

        private void EndWork(string msg)
        {
            ReportStatus(msg);
            Busying(false);
        }

        private async void buttonEncrypt_Click(object sender, EventArgs e)
        {
            if (!Check())
                return;
            BeginWork(Encrypting);
            await Task.Run(() => SearchDeeper(labelProjectPath.Text, p =>
            {
                new Crypto(textBoxPassword.Text).Encrypt(p, checkBoxBackup.Checked);
            }));
            EndWork(Encrypted);
        }

        private async void buttonDecrypt_Click(object sender, EventArgs e)
        {
            if (!Check())
                return;
            BeginWork(Decrypting);
            await Task.Run(() => SearchDeeper(labelProjectPath.Text, p =>
            {
                new Crypto(textBoxPassword.Text).Decrypt(p, checkBoxBackup.Checked);
            }));
            EndWork(Decrypted);
        }  

        private void Main_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void Main_DragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;
            try
            {
                var path = ((Array)e.Data.GetData(DataFormats.FileDrop)).Cast<object>().Single().ToString();
                if (!Directory.Exists(path))
                {
                    if (labelProjectPath.Text == MoreInvalid + InvalidPath)
                    {
                        MessageBox.Show(this, ImDone, "", MessageBoxButtons.OK);
                        Close();
                    }

                    if (labelProjectPath.Text == InvalidPath)
                    {
                        labelProjectPath.Text = MoreInvalid + InvalidPath;
                        return;
                    }

                    labelProjectPath.Text = InvalidPath;
                    return;
                }

                _tooltip.SetToolTip(labelProjectPath, path);
                labelProjectPath.Text = path;
                _inited = true;
            }
            catch (InvalidOperationException)
            {
                labelProjectPath.Text = MultiInvalid;
            }
        }

        private readonly ToolTip _tooltip = new ToolTip();
    }


}