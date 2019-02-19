using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
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
        private DateTime lastFinalReport = DateTime.Now;

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
            this.ActiveControl = this.buttonRun;
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
                
                #pragma warning disable CS4014
                Task.Run(async() =>
                {
                    while (true)
                    {
                        if (!this.isRunning) break;

                        AppConfig.TimeSchedule active = CurrentlyActiveSchedule();
                        if (active != null)
                        {
                            rpaChecker = new RpaChecker(this, appConfig.LogFileDirectory, appConfig.LogFileName, appConfig.WebhookUrl,
                                                        active.SuccessReport, active.ErrorReport,  active.ErrorJudgementInterval);
                            Task.Run(() => rpaChecker.Run());
                            await Task.Delay(active.RefreshInterval * 60 * 1000);
                        }
                        else
                            await Task.Delay(1000);
                    }
                });
                #pragma warning restore CS4014
            }
            else
            {
                this.isRunning = false;
                buttonRun.Text = "実行";
                buttonSaveConfig.Enabled = true;
                rpaChecker.ResetCount();
            }
        }

        private AppConfig.TimeSchedule CurrentlyActiveSchedule()
        {
            DateTime now = DateTime.Now;
            foreach (AppConfig.TimeSchedule ts in appConfig.Schedules)
            {
                if (ts.StartTime <= now && now < ts.EndTime)
                {
                    return ts;
                }
            }
            return null;
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
                else
                    listViewIntervalList.Items.Clear();
            }

            try
            {
                appConfig = AppConfig.Load();
            }
            catch (FileNotFoundException ex)
            {
                MessageBox.Show("設定ファイルが見つかりませんでした。\r\n\r\n" + ex.ToString(), "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            }
            catch (System.Configuration.ConfigurationErrorsException ex)
            {
                MessageBox.Show("設定ファイルの読み込みに失敗しました\r\n\r\n" + ex.ToString(), "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            }

            this.textBoxLogDir.Text = appConfig.LogFileDirectory;
            this.textBoxLogFile.Text = appConfig.LogFileName;
            this.textBoxWebhook.Text = appConfig.WebhookUrl;
            foreach(AppConfig.TimeSchedule ts in appConfig.Schedules)
            {
                listViewIntervalList.Items.Add(new ListViewItem(new string[] {
                    ts.StartTime.ToString("HH:mm"),
                    ts.EndTime.ToString("HH:mm"),
                    ts.RefreshInterval.ToString(),
                    ts.ErrorJudgementInterval.ToString(),
                    ts.SuccessReport ? "○" : "×",
                    ts.ErrorReport ? "○" : "×"}));
            }

            if (reopen)
                MessageBox.Show("設定を再読込しました。", "確認", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ApplyIntervalList()
        {
            appConfig.LogFileDirectory = textBoxLogDir.Text;
            appConfig.LogFileName = textBoxLogFile.Text;
            appConfig.WebhookUrl = textBoxWebhook.Text;
            List<AppConfig.TimeSchedule> tsList = new List<AppConfig.TimeSchedule>();
            foreach (ListViewItem item in listViewIntervalList.Items)
            {
                AppConfig.TimeSchedule ts = new AppConfig.TimeSchedule();
                ts.StartTime = DateTime.Parse(item.SubItems[0].Text);
                ts.EndTime = DateTime.Parse(item.SubItems[1].Text);
                ts.RefreshInterval = int.Parse(item.SubItems[2].Text);
                ts.ErrorJudgementInterval = int.Parse(item.SubItems[3].Text);
                ts.SuccessReport = item.SubItems[4].Text.Equals("○");
                ts.ErrorReport = item.SubItems[5].Text.Equals("○");
                tsList.Add(ts);
            }
            appConfig.Schedules = tsList.ToArray();
        }

        private void SaveConfiguration()
        {
            DialogResult result = MessageBox.Show("設定を保存してもよろしいですか？",
                                                    "確認",
                                                    MessageBoxButtons.YesNo,
                                                    MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
                return;

            ApplyIntervalList();
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

            string successReport = checkBoxNormalReport.Checked ? "○" : "×";
            string errorReport = checkBoxErrorReport.Checked ? "○": "×";

            listViewIntervalList.Items.Add(new ListViewItem(new string[] { startTimeStr, endTimeStr, refreshInterval, logUpdateInterval, successReport, errorReport }));

            listViewIntervalList.ListViewItemSorter = new ListViewItemComparer(0);
            ApplyIntervalList();
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
            checkBoxNormalReport.Checked = item.SubItems[4].Text.Equals("○");
            checkBoxErrorReport.Checked = item.SubItems[5].Text.Equals("○");
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
                selected.SubItems[4].Text = checkBoxNormalReport.Checked ? "○" : "×";
                selected.SubItems[5].Text = checkBoxErrorReport.Checked ? "○" : "×";
                ApplyIntervalList();
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
                ApplyIntervalList();
            }
        }

        private void listView1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                foreach (ListViewItem item in listView1.Items)
                {
                    item.Selected = true;
                }
            }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                if (listView1.SelectedItems.Count > 0)
                {
                    string str = "";
                    foreach (ListViewItem selected in listView1.SelectedItems)
                    {
                        var a = selected.SubItems[1];
                        foreach (ListViewItem.ListViewSubItem subitem in selected.SubItems)
                        {
                            str += subitem.Text + "\t";
                        }
                        str = str.Trim() + "\r\n";
                    }
                    str = System.Text.RegularExpressions.Regex.Replace(str, @"[\r\n]+$", "");
                    Clipboard.SetText(str);
                }
            }
        }

        private void listViewIntervalList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                foreach (ListViewItem item in listViewIntervalList.Items)
                {
                    item.Selected = true;
                }
            }
        }
    }
}
