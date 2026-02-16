using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.IO;
using Path = System.IO.Path;
using System.Windows.Threading;

namespace RTSP_Translation_Gremm
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Process _proc;
        public DispatcherTimer StartTimer3s;
        public MainWindow()
        {
            InitializeComponent();
            StartTimer_for_StartAppication(null, null);
            this.Closing += MainWindow_Closing;

            var saved = Properties.Settings.Default.LastRtspUrl;
            if (!string.IsNullOrWhiteSpace(saved))
                RtspUrlBox.Text = saved;

        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopProcess();
        }

        private string GetBaseDir()
        {
            // Стабильно для .NET Framework/.NET Core
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private string GetServerExePath()
        {
            return Path.Combine(GetBaseDir(), "happytime-rtsp-server", "RtspServer.exe");
            //return Path.Combine(GetBaseDir(), "rtsp_payload", "RTSP_Translation_Gremm.exe");

        }

        private string GetWorkingDir()
        {
            return Path.GetDirectoryName(GetServerExePath());
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_proc != null && !_proc.HasExited)
                {
                    AddLog("Уже запущено.");
                    await UpdateStatusAsync();
                    return;
                }

                string exePath = GetServerExePath();
                if (!File.Exists(exePath))
                {
                    AddLog("Не найден exe: " + exePath);
                    StatusText.Text = "Статус: ошибка (exe не найден)";
                    return;
                }

                var psi = new ProcessStartInfo();
                psi.FileName = exePath;
                psi.WorkingDirectory = GetWorkingDir();
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;

                _proc = new Process();
                _proc.StartInfo = psi;
                _proc.EnableRaisingEvents = true;
                _proc.OutputDataReceived += Proc_OutputDataReceived;
                _proc.ErrorDataReceived += Proc_ErrorDataReceived;
                _proc.Exited += Proc_Exited;

                bool started = _proc.Start();
                if (!started)
                {
                    AddLog("Process.Start вернул false.");
                    StatusText.Text = "Статус: ошибка запуска";
                    return;
                }

                _proc.BeginOutputReadLine();
                _proc.BeginErrorReadLine();

                AddLog("Запущено: PID=" + _proc.Id);
                StatusText.Text = "Статус: запуск…";

                string url = RtspUrlBox.Text.Trim();
                bool ok = await WaitRtspReadyAsync(url, 5000);

                StatusText.Text = ok ? "Статус: поток доступен ✅" : "Статус: нет ответа RTSP ❌";
                AddLog(ok ? "RTSP отвечает на OPTIONS." : "RTSP не отвечает (проверь URL/порт/фаервол).");
            }
            catch (Exception ex)
            {
                AddLog("Ошибка запуска: " + ex.Message);
                StatusText.Text = "Статус: ошибка запуска";
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            StopProcess();
            StatusText.Text = "Статус: остановлено";
        }

        private void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;
            Dispatcher.Invoke(() => AddLog(e.Data));
        }

        private void Proc_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;
            Dispatcher.Invoke(() => AddLog("[ERR] " + e.Data));
        }

        private void Proc_Exited(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                int code = 0;
                try { code = _proc != null ? _proc.ExitCode : 0; } catch { }
                AddLog("Процесс завершился. Код: " + code);
                StatusText.Text = "Статус: остановлено";
            });
        }

        private void StopProcess()
        {
            try
            {
                if (_proc == null) return;
                if (_proc.HasExited) return;

                _proc.Kill();
                _proc.WaitForExit(2000);
                AddLog("Остановлено.");
            }
            catch (Exception ex)
            {
                AddLog("Ошибка остановки: " + ex.Message);
            }
            finally
            {
                if (_proc != null)
                {
                    _proc.Dispose();
                    _proc = null;
                }
            }
        }

        private async Task<bool> WaitRtspReadyAsync(string url, int waitMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < waitMs)
            {
                if (_proc != null && _proc.HasExited) return false;

                bool ok = await RtspProbe.IsRtspAliveAsync(url, 800);
                if (ok) return true;

                await Task.Delay(250);
            }
            return false;
        }

        private async Task UpdateStatusAsync()
        {
            string url = RtspUrlBox.Text.Trim();
            bool ok = await RtspProbe.IsRtspAliveAsync(url, 1200);
            StatusText.Text = ok ? "Статус: поток доступен ✅" : "Статус: нет ответа RTSP ❌";
        }

        private void AddLog(string text) // Логирование в блок
        {
            LogList.Items.Insert(0, DateTime.Now.ToString("HH:mm:ss") + "  " + text);
            if (LogList.Items.Count > 300)
                LogList.Items.RemoveAt(LogList.Items.Count - 1);
        }

        private void StartTimer_for_StartAppication (object sender, EventArgs e) // Таймер на 3 секунды
        {
            StartTimer3s = new DispatcherTimer();
            StartTimer3s.Interval = TimeSpan.FromSeconds(2);
            StartTimer3s.Tick += Tick_timer_start_app;
            StartTimer3s.Start();
        }

        private void Tick_timer_start_app (object sender, EventArgs e) // Тик таймера Старта
        {
            StartTimer3s.Stop();
            StartTimer3s.Tick -= Tick_timer_start_app;
            //StartButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); // Нажимаем на кнопку
            Start_Click(null, null);
        }

        //////Косметика
        ///
        private void RtspUrlBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) // Логика сохранения приложения
        {
            Properties.Settings.Default.LastRtspUrl = RtspUrlBox.Text;
            Properties.Settings.Default.Save();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) // Закрытие приложения с сохранением
        {
            Properties.Settings.Default.LastRtspUrl = RtspUrlBox.Text;
            Properties.Settings.Default.Save();
            StopProcess();
        }

        private void DefaultStringTextRtsp (object sender, RoutedEventArgs e) // Базовый адрес RTSP
        {
            RtspUrlBox.Text = "rtsp://127.0.0.1:5544/screenlive";
        }
    }

}
