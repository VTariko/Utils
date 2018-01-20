using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Windows.Media;
using Microsoft.Win32;

namespace ResourceMonitorVT
{
    public static class Logic
    {
        #region Константы

        /// <summary>
        /// Количество секунд между тиками таймера счетчиков
        /// </summary>
        public const double SECONDS_COUNT = 1;

        /// <summary>
        /// Количество секунд между тиками таймера мигания
        /// </summary>
        public const double SECONDS_BLINK = 0.5;

        /// <summary>
        /// Стартовая строка при записи в файл
        /// </summary>
        public const string START_STRING =
            "ServerName;Date;Total Memory;% CPU Usage;% Usage Physical Memory;KB/sec Disk I/O;ProcessName;RAM Usage by Process; CPU Usage by Process; Page Faults/sec;";


        #endregion

        #region Делегаты

        public delegate void ColorWork(Dimension source, Color value);

        #endregion

        #region Свойства

        /// <summary>
        /// Количество процессоров на исследуемом сервере
        /// </summary>
        public static int CountProcessor { get; private set; }

        /// <summary>
        /// Общее количество памяти на исследуемом сервере
        /// </summary>
        public static int TotalMemory { get; private set; }
        
        /// <summary>
        /// Имя исследуемого сервера
        /// </summary>
        public static string ServerName { get; private set; }

        /// <summary>
        /// Максимально допустимое значение нагрузки RAM и CPU
        /// </summary>
        public static double SafeLoad = double.Parse(ConfigurationManager.AppSettings.Get("MaxSafeLoad"));

        /// <summary>
        /// Максимально допустимое значение нагрузки на CPU отдельным процессом
        /// </summary>
        public static double SafeLoadProcess = double.Parse(ConfigurationManager.AppSettings.Get("MaxSafeLoadProcess"));

        #endregion

        #region Методы

        public static bool InitConnect(ResourceMonitor window, string server)
        {
            ServerName = server;
            string pathToSave = Logic.ChooseSaveFile(server);
            if (string.IsNullOrWhiteSpace(pathToSave))
                return false;
            window.Counters.Init(pathToSave);
            return true;
        }

        /// <summary>
        /// Изменение ширины колонок таблицы при изменении ширины окна
        /// </summary>
        /// <param name="window"></param>
        public static void ReSizeColumn(ResourceMonitor window)
        {
            double width = window.lvCommon.ActualWidth;

            window.gvCommon.Columns[0].Width = 0.4 * width;

            double newWidth = (width - window.gvCommon.Columns[0].Width) / (window.gvCommon.Columns.Count - 1);
            for (int i = 1; i < window.gvCommon.Columns.Count; i++)
            {
                window.gvCommon.Columns[i].Width = newWidth;
            }
        }

        /// <summary>
        /// Выбор пути к файлу сохранения
        /// </summary>
        /// <returns></returns>
        public static string ChooseSaveFile(string server)
        {
            ServerName = server;
            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "CSV Files | *.csv",
                CreatePrompt = true
            };
            if (sfd.ShowDialog() == true && !string.IsNullOrWhiteSpace(sfd.FileName))
            {
                return sfd.FileName;
            }
            return string.Empty;
        }

        /// <summary>
        /// Инициализация общего количества памяти на исследуемом сервере
        /// </summary>
        public static void GetTotalMemory()
        {
            ConnectionOptions co = new ConnectionOptions { Username = null };

            ManagementScope ms = new ManagementScope("\\\\" + Logic.ServerName + "\\root\\CIMV2", co);
            ObjectQuery q = new ObjectQuery("select TotalPhysicalMemory from Win32_ComputerSystem");
            ManagementObjectSearcher os = new ManagementObjectSearcher(ms, q);
            ManagementObjectCollection moc = os.Get();

            ulong phisicalMemorySize = moc.Cast<ManagementObject>().Aggregate<ManagementObject, ulong>(0,
                (current, o) =>
                    current + Convert.ToUInt64(o["TotalPhysicalMemory"], CultureInfo.InvariantCulture));


            TotalMemory = (int)(phisicalMemorySize / 1024 / 1014);
        }

        /// <summary>
        /// Инициализация количества процессоров на исследуемом сервере
        /// </summary>
        public static void GetProcessorCount()
        {
            ConnectionOptions co = new ConnectionOptions { Username = null };

            ManagementScope ms = new ManagementScope("\\\\" + Logic.ServerName + "\\root\\CIMV2", co);
            ObjectQuery q = new ObjectQuery("Select * from Win32_ComputerSystem");
            ManagementObjectSearcher os = new ManagementObjectSearcher(ms, q);
            ManagementObjectCollection moc = os.Get();
            int proc = 0;
            foreach (ManagementBaseObject o in moc)
            {
                proc = Convert.ToInt32(o["NumberOfLogicalProcessors"]);
            }

            CountProcessor = proc;
        }

        //public static string GetProcessPriority()
        //{
        //    string priopity = string.Empty;

        //    ConnectionOptions co = new ConnectionOptions { Username = null };
        //    ManagementScope ms = new ManagementScope("\\\\" + Logic.ServerName + "\\root\\CIMV2", co);
        //    ObjectQuery q = new ObjectQuery("Select * from Win32_Process");
        //    ManagementObjectSearcher os = new ManagementObjectSearcher(ms, q);
        //    ManagementObjectCollection moc = os.Get();
        //    foreach (ManagementBaseObject o in moc)
        //    {

        //    }
        //    return priopity;
        //}

        /// <summary>
        /// Получение имя инстанса исследуемого процесса
        /// </summary>
        /// <param name="process">Исследуемый процесс</param>
        /// <returns></returns>
        public static string InstanceName(this Process process)
        {
            int pid = process.Id;
            string name = process.ProcessName;
            string server = process.MachineName;

            PerformanceCounterCategory cat = new PerformanceCounterCategory("Process", server);

            List<string> instances = cat.GetInstanceNames().Where(inst => inst.StartsWith(name)).ToList();

            foreach (string instance in instances)
            {
                using (PerformanceCounter pc = new PerformanceCounter("Process", "ID Process", instance, server))
                {
                    try
                    {
                        int val = (int)pc.RawValue;
                        if (val == pid)
                        {
                            return instance;
                        }
                    }
                    catch
                    {
                        //ignored
                    }
                }
            }
            throw new Exception(
                $"Could not find performance counter instance name for process '{name}'. This is truly strange ...");
        }

        #endregion
    }
}
