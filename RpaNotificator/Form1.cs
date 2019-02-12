using Newtonsoft.Json;
using System;
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
            try
            {
                appConfig = new AppConfig();
            }
            catch(System.Configuration.SettingsPropertyNotFoundException ex)
            {
                MessageBox.Show("設定ファイルが見つかりませんでした。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            }
            InitializeComponent();

            // 設定ファイル読み込み
            this.textBoxLogDir.Text = appConfig.LogFileDirectory;
            this.textBoxLogFile.Text = appConfig.LogFileName;
            this.textBoxWebhook.Text = appConfig.WebhookUrl;
            this.checkBoxNormalReport.Checked = appConfig.ReportNormalLog;
            this.checkBoxErrorReport.Checked = appConfig.ReportErrorLog;
            this.numericUpDownRefreshInterval.Value = appConfig.RefreshInterval;
            this.numericUpDownLogUpdateInterval.Value = appConfig.LogUpdateInterval;

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

            DateTime dt1 = DateTime.ParseExact("2019/02/12 130000",
                            "yyyy/MM/dd HHmmss",
                            System.Globalization.DateTimeFormatInfo.InvariantInfo,
                            System.Globalization.DateTimeStyles.None);
            MessageBox.Show(dt1.ToString());
        }

        int test (int a)
        {
            int _min = a;
            int _min1Digit = (_min - (int)(_min / 10) * 10);
            int minBase = 0;

            if (_min1Digit >= 5)
            {
                minBase = _min - (_min1Digit - 5);
            }
            else
            {
                minBase = _min - _min1Digit;
            }

            return minBase;
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
            DialogResult result = MessageBox.Show("設定を保存してもよろしいですか？",
                                                    "確認",
                                                    MessageBoxButtons.YesNo,
                                                    MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
            {
                return;
            }

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

                RpaChecker.traialsCount = 100;
                RpaChecker.errorsCount = 1;
                
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
    }
}
