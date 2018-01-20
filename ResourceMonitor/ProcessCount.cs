using System;
using System.ComponentModel;
using System.Diagnostics;

namespace ResourceMonitorVT
{
    /// <summary>
    /// Класс счетчика процесса
    /// </summary>
    public class ProcessCount : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Свойства

        /// <summary>
        /// ID процесса
        /// </summary>
        public int ProcessId { get; }

        /// <summary>
        /// Имя процесса
        /// </summary>
        public string ProcessName { get; }

        /// <summary>
        /// Инстанс процесса
        /// </summary>
        public string Instance { get; }

        /// <summary>
        /// Счетчик "Page Faults/sec"
        /// </summary>
        public PerformanceCounter FaultsCounter { get; }

        /// <summary>
        /// Счетчик используемой памяти
        /// </summary>
        public PerformanceCounter RamCounter { get; }

        /// <summary>
        /// Счетчик процента используемого процессора
        /// </summary>
        public PerformanceCounter CpuCounter { get; }

        /// <summary>
        /// Значение "Page Faults/sec" процесса
        /// </summary>
        public double FaultsUsage
        {
            get => _faultsUsage;
            set
            {
                if (Math.Abs(_faultsUsage - value) > 0.01)
                {
                    _faultsUsage = value;
                    OnPropertyChanged(nameof(FaultsUsage));
                }
            }
        }

        /// <summary>
        /// Значение "Memory (MB)" процесса
        /// </summary>
        public double RamUsage
        {
            get => _ramUsage;
            set
            {
                if (Math.Abs(_ramUsage - value) > 0.01)
                {
                    _ramUsage = value;
                    OnPropertyChanged(nameof(RamUsage));
                }
            }
        }

        /// <summary>
        /// Значение "CPU Usage, %" процесса
        /// </summary>
        public double CpuUsage
        {
            get => _cpuUsage;
            set
            {
                if (Math.Abs(_cpuUsage - value) > 0.01)
                {
                    _cpuUsage = value;
                    OnPropertyChanged(nameof(CpuUsage));
                }
            }
        }

        public bool MoreValid
        {
            get => _moreValid;
            set
            {
                if (_moreValid != value)
                {
                    _moreValid = value;
                    OnPropertyChanged(nameof(MoreValid));
                }
            }
        }

        #endregion

        #region Поля
        
        /// <summary>
        /// Значение "Page Faults/sec" процесса
        /// </summary>
        private double _faultsUsage;

        /// <summary>
        /// Значение "Memory (MB)" процесса
        /// </summary>
        private double _ramUsage;

        /// <summary>
        /// Значение "CPU Usage, %" процесса
        /// </summary>
        private double _cpuUsage;

        private bool _moreValid;

        #endregion

        #region Конструкторы

        /// <summary>
        /// Базовый конструктор счетчика процесса
        /// </summary>
        /// <param name="id">ID процесса</param>
        /// <param name="processName">Имя процесса</param>
        /// <param name="instance">Инстанс процесса</param>
        /// <param name="ramCounter">Счетчик используемой памяти</param>
        /// <param name="faultsCounter">Счетчик "Page Faults/sec"</param>
        /// <param name="cpuCounter">Счетчик процента используемого процессора</param>
        public ProcessCount(int id, string processName, string instance, PerformanceCounter ramCounter, PerformanceCounter faultsCounter, PerformanceCounter cpuCounter)
        {
            ProcessId = id;
            ProcessName = processName;
            Instance = instance;
            RamCounter = ramCounter;
            FaultsCounter = faultsCounter;
            CpuCounter = cpuCounter;
            RamUsage = Math.Round(RamCounter.NextValue() / 1024 / 1024, 2);
            FaultsUsage = Math.Round(FaultsCounter.NextValue(), 2);
            CpuUsage = Math.Round(CpuCounter.NextValue() / Logic.CountProcessor, 2);
        }

        #endregion

        #region Методы

        /// <summary>
        /// Обновление значения со счетчиков
        /// </summary>
        public void UpdateValues()
        {
            try
            {
                FaultsUsage = Math.Round(FaultsCounter.NextValue() / Logic.CountProcessor, 2);
                RamUsage = Math.Round(RamCounter.NextValue() / 1024 / 1024, 2);
                CpuUsage = Math.Round(CpuCounter.NextValue() / Logic.CountProcessor, 2);
                MoreValid = CpuUsage > Logic.SafeLoadProcess;

            }
            catch
            {
                //ignored
            }
        } 

        #endregion
    }
}
