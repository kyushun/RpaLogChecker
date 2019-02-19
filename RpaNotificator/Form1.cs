using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RpaNotificator
{
    public partial class Form1 : Form
    {
        private AppConfig appConfig;
        private RpaChecker rpaChecker;
        private bool isRunning = false;
        private DateTime lastFinalReport = DateTime.Now.AddDays(-1);

        public Form1()
        {
            InitializeComponent();

            // 設定ファイル読み込み
            OpenConfiguration();

            //フォームの最大化ボタンの表示、非表示を切り替える
            this.MaximizeBox = !this.MaximizeBox;
            //ユーザーがサイズを変更できないようにする
            this.FormBorderStyle = FormBorderStyle.FixedSingle;

            Task.Run(async() =>
            {
                while(true)
                {
                    if (isRunning && DateTime.Now.Hour == 18 && lastFinalReport.Date.CompareTo(DateTime.Now.Date) == -1)
                    {
                        lastFinalReport = DateTime.Now;
                        rpaChecker.SendFinalReport();
                    }
                    await Task.Delay(30 * 1000);
                }
            });

            listView1.Columns.Add("時間", -2, HorizontalAlignment.Left);
            listView1.Columns.Add("ログ", -2, HorizontalAlignment.Left);
            AddLog("プログラム起動");

            listViewIntervalList.Columns.Add("開始", -2, HorizontalAlignment.Left);
            listViewIntervalList.Columns.Add("終了", -2, HorizontalAlignment.Left);
            listViewIntervalList.Columns.Add("ログ調査（分）", -2, HorizontalAlignment.Left);
            listViewIntervalList.Columns.Add("エラー判定（分）", -2, HorizontalAlignment.Left);
            listViewIntervalList.Columns.Add("正常報告", -2, HorizontalAlignment.Left);
            listViewIntervalList.Columns.Add("エラー報告", -2, HorizontalAlignment.Left);
        }
        
        private void Form1_Load(object sender, EventArgs e)
        {
            if (Program.args.HasSwitch("-a") || Program.args.HasSwitch("--autostart"))
            {
                if (!isRunning)
                    this.buttonRun.PerformClick();
            }
            if (Program.args.HasSwitch("-r") || Program.args.HasSwitch("--restarted"))
            {
                Notificator.HangoutsChat chat = new Notificator.HangoutsChat(this.textBoxWebhook.Text);
                MessageBuilder mb = new MessageBuilder();
                chat.Send(mb.GetMessage(MessageBuilder.ReportLevel.APPLICATION_RESTARTED));
            }
        }

        public void AddLog(string msg)
        {
            listView1.Items.Add(new ListViewItem(new string[] { DateTime.Now.ToString(), msg }));
            listView1.EnsureVisible(listView1.Items.Count - 1);
        }

        // 別スレッド用
        delegate void delegete1(string msg);
        public void AddLogFromAnotherThread(string msg)
        {
            Invoke(new delegete1(AddLog), msg);
        }

        private void buttonSaveConfig_Click(object sender, EventArgs e)
        {
            SaveConfiguration();
        }

        private void buttonRun_Click(object sender, EventArgs e)
        {
            if (buttonRun.Text == "実行")
            {
                this.isRunning = true;
                buttonRun.Text = "キャンセル";
                buttonSaveConfig.Enabled = false;
                rpaChecker = new RpaChecker(this, textBoxLogDir.Text, textBoxLogFile.Text, textBoxWebhook.Text,
                                            checkBoxNormalReport.Checked, checkBoxErrorReport.Checked,
                                            decimal.ToInt32(numericUpDownRefreshInterval.Value), decimal.ToInt32(numericUpDownLogUpdateInterval.Value));
                
#pragma warning disable CS4014 // この呼び出しを待たないため、現在のメソッドの実行は、呼び出しが完了する前に続行します
                Task.Run(async() =>
                {
                    while (true)
                    {
                        if (!this.isRunning) break;

                        Task.Run(() => rpaChecker.Run());
                        
                        await Task.Delay((decimal.ToInt32(numericUpDownRefreshInterval.Value) * 60 * 1000));
                    }
                });
#pragma warning restore CS4014 // この呼び出しを待たないため、現在のメソッドの実行は、呼び出しが完了する前に続行します
            }
            else
            {
                this.isRunning = false;
                buttonRun.Text = "実行";
                buttonSaveConfig.Enabled = true;
            }
        }

        private void OpenConfiguration(bool reopen = false)
        {
            if (reopen)
            {
                DialogResult result = MessageBox.Show("設定を再読込みします。\r\n保存していない設定は破棄されますがよろしいですか？",
                                                    "確認",
                                                    MessageBoxButtons.YesNo,
                                                    MessageBoxIcon.Question);
                if (result != DialogResult.Yes)
                    return;
            }

            try
            {
                appConfig = new AppConfig();
            }
            catch (System.Configuration.SettingsPropertyNotFoundException ex)
            {
                MessageBox.Show("設定ファイルが見つかりませんでした。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            }

            this.textBoxLogDir.Text = appConfig.LogFileDirectory;
            this.textBoxLogFile.Text = appConfig.LogFileName;
            this.textBoxWebhook.Text = appConfig.WebhookUrl;
            this.checkBoxNormalReport.Checked = appConfig.ReportNormalLog;
            this.checkBoxErrorReport.Checked = appConfig.ReportErrorLog;
            this.numericUpDownRefreshInterval.Value = appConfig.RefreshInterval;
            this.numericUpDownLogUpdateInterval.Value = appConfig.LogUpdateInterval;

            if (reopen)
                MessageBox.Show("設定を再読込しました。", "確認", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SaveConfiguration()
        {
            DialogResult result = MessageBox.Show("設定を保存してもよろしいですか？",
                                                    "確認",
                                                    MessageBoxButtons.YesNo,
                                                    MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
                return;

            appConfig.LogFileDirectory = textBoxLogDir.Text;
            appConfig.LogFileName = textBoxLogFile.Text;
            appConfig.WebhookUrl = textBoxWebhook.Text;
            appConfig.ReportNormalLog = checkBoxNormalReport.Checked;
            appConfig.ReportErrorLog = checkBoxErrorReport.Checked;
            appConfig.RefreshInterval = decimal.ToInt32(numericUpDownRefreshInterval.Value);
            appConfig.LogUpdateInterval = decimal.ToInt32(numericUpDownLogUpdateInterval.Value);

            appConfig.Save();

            MessageBox.Show("設定を保存しました。", "確認", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenConfiguration(true);
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveConfiguration();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
            
            MessageBox.Show(asm.GetName().Name + "\r\nv" + asm.GetName().Version + "", "About");
        }

        private void buttonAddToIntervalList_Click(object sender, EventArgs e)
        {
            DateTime startTime = dateTimePickerStartTime.Value;
            DateTime endTime = dateTimePickerEndTime.Value;
            int _refreshInterval = decimal.ToInt32(numericUpDownRefreshInterval.Value);
            int _logUpdateInterval = decimal.ToInt32(numericUpDownLogUpdateInterval.Value);

            if (startTime >= endTime)
            {
                MessageBox.Show("開始時間は終了時間より前である必要があります。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            } else if (_refreshInterval <= 0 || _logUpdateInterval <= 0)
            {
                MessageBox.Show("ログ取得間隔とエラー判定間隔は1分以上である必要があります。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            string startTimeStr = dateTimePickerStartTime.Text;
            string endTimeStr = dateTimePickerEndTime.Text;

            string refreshInterval = numericUpDownRefreshInterval.Value.ToString();
            string logUpdateInterval = numericUpDownLogUpdateInterval.Value.ToString();

            listViewIntervalList.Items.Add(new ListViewItem(new string[] { startTimeStr, endTimeStr, refreshInterval, logUpdateInterval }));

            listViewIntervalList.ListViewItemSorter = new ListViewItemComparer(0);
        }


        /// <summary>
        /// ListViewの項目の並び替えに使用するクラス
        /// </summary>
        public class ListViewItemComparer : IComparer
        {
            private int _column;

            /// <summary>
            /// ListViewItemComparerクラスのコンストラクタ
            /// </summary>
            /// <param name="col">並び替える列番号</param>
            public ListViewItemComparer(int col)
            {
                _column = col;
            }

            public int Compare(object x, object y)
            {
                ListViewItem itemx = (ListViewItem)x;
                ListViewItem itemy = (ListViewItem)y;

                DateTime.TryParse(itemx.SubItems[_column].Text, out DateTime _x);
                DateTime.TryParse(itemy.SubItems[_column].Text, out DateTime _y);


                return DateTime.Compare(_x, _y);
            }
        }

        private void listViewIntervalList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listViewIntervalList.SelectedItems.Count > 0)
            {
                SetIntervalConfigValue(listViewIntervalList.SelectedItems[0].Index);
            }
        }

        private void SetIntervalConfigValue(int index)
        {
            if (index >= listViewIntervalList.Items.Count)
            {
                index = 0;
            }

            ListViewItem item = listViewIntervalList.SelectedItems[0];
            DateTime.TryParse(item.SubItems[0].Text, out DateTime startTime);
            DateTime.TryParse(item.SubItems[1].Text, out DateTime endTime);
            int.TryParse(item.SubItems[2].Text, out int refreshInterval);
            int.TryParse(item.SubItems[3].Text, out int logupdateInterval);
            dateTimePickerStartTime.Value = startTime;
            dateTimePickerEndTime.Value = endTime;
            numericUpDownRefreshInterval.Value = refreshInterval;
            numericUpDownLogUpdateInterval.Value = logupdateInterval;
        }

        private void buttonUpdateToIntervalList_Click(object sender, EventArgs e)
        {
            if (listViewIntervalList.SelectedItems.Count > 0)
            {
                ListViewItem selected = listViewIntervalList.SelectedItems[0];
                selected.SubItems[0].Text = dateTimePickerStartTime.Text;
                selected.SubItems[1].Text = dateTimePickerEndTime.Text;
                selected.SubItems[2].Text = numericUpDownRefreshInterval.Value.ToString();
                selected.SubItems[3].Text = numericUpDownLogUpdateInterval.Value.ToString();
            }
        }

        private void buttonDeleteFromIntervalList_Click(object sender, EventArgs e)
        {
            if (listViewIntervalList.SelectedItems.Count > 0)
            {
                foreach (ListViewItem selected in listViewIntervalList.SelectedItems)
                {
                    listViewIntervalList.Items.Remove(selected);
                }
            }
        }
    }
}
