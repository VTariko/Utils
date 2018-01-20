using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace ResourceMonitorVT
{
    /// <summary>
    /// Класс Счётчик
    /// </summary>
    public class CountersLogic : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region События

        public event Logic.ColorWork ChangeColor;

        #endregion

        #region Делегаты

        private delegate void ProcessWork(Process process, string instance);

        #endregion

        #region Свойства

        /// <summary>
        /// Значение "% CPU Usage"
        /// </summary>
        public string ValueCpu
        {
            get => _valueCpu;
            set
            {
                if (_valueCpu != value)
                {
                    _valueCpu = value;
                    OnPropertyChanged(nameof(ValueCpu));
                }
            }
        }

        /// <summary>
        /// Значение "% Usage Physical Memory"
        /// </summary>
        public string ValueMemory
        {
            get => _valueMemory;
            set
            {
                if (_valueMemory != value)
                {
                    _valueMemory = value;
                    OnPropertyChanged(nameof(ValueMemory));
                }
            }
        }

        /// <summary>
        /// Значение "KB/sec Disk I/O"
        /// </summary>
        public string ValueDisk
        {
            get => _valueDisk;
            set
            {
                if (_valueDisk != value)
                {
                    _valueDisk = value;
                    OnPropertyChanged(nameof(ValueDisk));
                }
            }
        }

        /// <summary>
        /// Коллекция всех запущенных на исследуемой машине процессов
        /// </summary>
        public ObservableCollection<Process> AllProcesses
        {
            get => _allProcesses;
            set
            {
                if (_allProcesses != value)
                {
                    _allProcesses = value;
                    OnPropertyChanged(nameof(AllProcesses));
                }
            }
        }

        /// <summary>
        /// Коллекция счетчиков процессов
        /// </summary>
        public ObservableCollection<ProcessCount> ProcessesCounts
        {
            get => _processesCounts;
            set
            {
                if (_processesCounts != value)
                {
                    _processesCounts = value;
                    OnPropertyChanged(nameof(ProcessesCounts));
                }
            }
        }

        /// <summary>
        /// Шаблон для поиска процессов из списка
        /// </summary>
        public string Template
        {
            get => _template;
            set
            {
                if (_template != value)
                {
                    _template = value;
                    OnPropertyChanged(nameof(Template));
                }
            }
        }

        #endregion

        #region Поля

        #region Процессы

        private ObservableCollection<Process> _allProcesses;
        private ObservableCollection<ProcessCount> _processesCounts;
        private List<ProcessCount> _removedProcesses;

        #endregion
        
        #region Счетчики


        private PerformanceCounter _counterCpu;
        private PerformanceCounter _counterMemory;
        private PerformanceCounter _counterDisk;

        private string _valueCpu;
        private string _valueMemory;
        private string _valueDisk;

        private double _countCpu;
        private double _countMemory;

        #endregion

        #region Локеры

        private readonly object _lock = new object();
        private readonly object _lockTick = new object();
        private readonly object _lockTickColor = new object();

        #endregion

        private ProcessWork _addProcess;

        /// <summary>
        /// Запись лога в файл
        /// </summary>
        private StreamWriter _writer;

        /// <summary>
        /// Строки считанных значений
        /// </summary>
        private List<string> _calcs;

        /// <summary>
        /// Шаблон для поиска процессов из списка
        /// </summary>
        private string _template;

        /// <summary>
        /// Таймер запуска считывания новых значений
        /// </summary>
        private readonly Timer _tCount;

        /// <summary>
        /// Таймер мигания полей с превышающим норму значением
        /// </summary>
        private readonly Timer _tBlink;

        /// <summary>
        /// Флаг изменения цвета поля с превышением допустимой нагрузки
        /// </summary>
        private bool _tickColor;

        /// <summary>
        /// Флаг, что идет считывание новых значений
        /// </summary>
        private static bool _isCalc;

        /// <summary>
        /// Флаг, что идет раскраска полей
        /// </summary>
        private static bool _isColor;

        /// <summary>
        /// Флаг, что можно писать в файл
        /// </summary>
        private bool _isWrite;

        #endregion

        #region Конструкторы

        /// <summary>
        /// Конструктор
        /// </summary>
        public CountersLogic()
        {
            _removedProcesses = new List<ProcessCount>();
            ProcessesCounts = new ObservableCollection<ProcessCount>();
            _tCount = new Timer(TimerTickCount, null, Timeout.Infinite, Timeout.Infinite);
            _tBlink = new Timer(TimerTickBlink, null, Timeout.Infinite, Timeout.Infinite);
            _tickColor = false;
            _addProcess += AddProcessCount;
            _template = "chrome|firefox";
            _calcs = new List<string>();
            _isWrite = false;
        }

        #endregion

        #region Методы

        /// <summary>
        /// Обработчик события выбора процесса в выпадающем списке всех процессов
        /// </summary>
        /// <param name="process">Выбранный процесс</param>
        public void SelectedProcess(Process process)
        {
            lock (_lock)
            {
                string instance = process.InstanceName();

                if (ProcessesCounts.Any(p => p.ProcessName == process.ProcessName && p.ProcessId == process.Id && p.Instance == instance))
                    return;

                // Удаляем из коллекции помеченных как удаленные процессов все с именем, как у добавленного
                _removedProcesses.RemoveAll(rp => rp.ProcessName == process.ProcessName);

                //AddProcessCount(process, instance);
                _addProcess?.Invoke(process, instance);
            }
        }

        /// <summary>
        /// Инициализация счетчиков
        /// </summary>
        /// <param name="pathAutoSave"></param>
        public void Init(string pathAutoSave)
        {
            Ping ping = new Ping();
            try
            {
                // Проверяем сервер на доступность
                PingReply pingReply = ping.Send(Logic.ServerName);
                if (pingReply?.Status == IPStatus.Success)
                {
                    // Останавливаем таймер
                    TimerOff();
                    ProcessesCounts = new ObservableCollection<ProcessCount>();
                    _removedProcesses = new List<ProcessCount>();
                    // Инициализация общих счетчиков
                    InitCounters();
                    //Инициализация счетчиков процессов
                    InitProcesses();
                    _isCalc = false;
                    // Инициализация объекта записи считываемых значений в файл
                    InitWriter(pathAutoSave);
                    //Запускаем таймер
                    TimerOn();
                }
                else
                {
                    MessageBox.Show($"Сервер {Logic.ServerName} недоступен!");
                }
            }
            catch (PingException ex)
            {
                MessageBox.Show(
                    $"Сервер {Logic.ServerName} не существует или к нему нет доступа!{Environment.NewLine}{Environment.NewLine}{ex}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Сервер {Logic.ServerName}: ошибка!{Environment.NewLine}{Environment.NewLine}{ex}");
            }
        }

        /// <summary>
        /// Инициализация счетчиков процессов
        /// </summary>
        private void InitProcesses()
        {
            // Получаем полный список процессов на исследуемой машине
            List<Process> allProcesses = Process.GetProcesses(Logic.ServerName)
                .OrderBy(p => p.ProcessName).ToList();

            // Список введенных шаблонов с разделителем "|"
            List<string> patterns = Template.Split('|').ToList();
            // Выбираем из всех процессов те, которые соответствуют шаблону и отсутствуют в списке удаленных процессов
            List<Process> processes = allProcesses.AsParallel()
                .Where(p => patterns.Any(c => p.ProcessName.ToLower().Contains(c.ToLower())))
                .Where(sp =>
                    !_removedProcesses.AsParallel()
                        .Any(rp => rp.ProcessName == sp.ProcessName && rp.ProcessId == sp.Id))
                .ToList();

            lock (_lock)
            {
                try
                {
                    // Если процесса нет в уже готовом списке счетчиков - передаем его на создание счетчика по процессу
                    foreach (Process process in processes)
                    {
                        string instance = process.InstanceName();

                        if (ProcessesCounts.AsParallel()
                            .Any(p => p.ProcessName == process.ProcessName && p.ProcessId == process.Id &&
                                      p.Instance == instance))
                            continue;

                        _addProcess?.Invoke(process, instance);
                    }

                    // Удаляем из списка счетчиков процессов те, которых уже нет (их нет в списке всех полученных процессов)
                    // и сортируем счетчики процессов по имени
                    ProcessesCounts = new ObservableCollection<ProcessCount>(ProcessesCounts
                        .Where(pc => allProcesses.Any(p => p.Id == pc.ProcessId && p.ProcessName == pc.ProcessName && pc.Instance == p.InstanceName()))
                        .OrderBy(pc => pc.ProcessName));
                }
                catch
                {
                    //ignored
                }
            }
            //Удаляем из списка процессов (отображаемом в выпадающем списке) те, что добавлены в счетчики
            AllProcesses = new ObservableCollection<Process>(allProcesses.Where(p =>
                !ProcessesCounts.Any(prc => prc.ProcessId == p.Id && prc.ProcessName == p.ProcessName)));
        }

        /// <summary>
        /// Создание счетчика процесса и добавление его в общую коллекцию
        /// </summary>
        /// <param name="process">Добавляемый процесс</param>
        /// <param name="instance">Инстанс процесса</param>
        private void AddProcessCount(Process process, string instance)
        {
            PerformanceCounter pcRam =
                new PerformanceCounter("Process", "Working Set", instance, Logic.ServerName);

            PerformanceCounter pcFaults =
                new PerformanceCounter("Process", "Page Faults/sec", instance, Logic.ServerName);

            PerformanceCounter pcCpu =
                new PerformanceCounter("Process", "% Processor Time", instance, Logic.ServerName);

            ProcessesCounts.Add(new ProcessCount(process.Id, process.ProcessName, instance, pcRam, pcFaults, pcCpu));
        }

        /// <summary>
        /// Инициализация общих счетчиков
        /// </summary>
        private void InitCounters()
        {
            try
            {
                _counterCpu = new PerformanceCounter("Processor", "% Processor Time", "_Total", Logic.ServerName);
                _counterMemory = new PerformanceCounter("Memory", "Available MBytes", null, Logic.ServerName);
                _counterDisk = new PerformanceCounter("PhysicalDisk", "Disk Bytes/sec", "_Total", Logic.ServerName);
                Logic.GetTotalMemory();
                Logic.GetProcessorCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                throw;
            }
        }

        /// <summary>
        /// Событие тика таймера
        /// </summary>
        /// <param name="obj"></param>
        private void TimerTickCount(object obj)
        {
            // Если какой-то поток уже обновляет значения - пройти мимо
            lock (_lockTick)
            {
                if (_isCalc)
                {
                    return;
                }
                _isCalc = true;
            }
            try
            {
                _countCpu = Math.Round(_counterCpu.NextValue(), 2);
                _countMemory = Math.Round((Logic.TotalMemory - _counterMemory.NextValue()) / Logic.TotalMemory * 100, 2);
                double countDisk = Math.Round(_counterDisk.NextValue() / 1024, 2);
                ValueMemory = $"{_countMemory}";
                ValueCpu = $"{_countCpu}";
                ValueDisk = $"{countDisk}";
                string common = $"{Logic.TotalMemory};{ValueCpu};{ValueMemory};{ValueDisk}";

                InitProcesses();

                Parallel.ForEach(ProcessesCounts, (pc) =>
                {
                    pc.UpdateValues();
                });

                foreach (ProcessCount pc in ProcessesCounts)
                {
                    string line =
                        $"{Logic.ServerName};{DateTime.Now:yyyy-MM-dd HH:mm:ss};{common};{pc.ProcessName};{pc.RamUsage};{pc.CpuUsage};{pc.FaultsUsage};";
                    _calcs.Add(line);
                }

                if (_isWrite && _calcs.Count >= 30)
                {
                    foreach (string calc in _calcs)
                    {
                        _writer.WriteLine(calc);
                    }
                    _writer.Flush();
                    _calcs = new List<string>();
                }
            }
            catch (Exception)
            {
                // ignored;
            }
            finally
            {
                _isCalc = false;
            }
        }

        private void TimerTickBlink(object obj)
        {
            lock (_lockTickColor)
            {
                if (_isColor)
                {
                    return;
                }
                _isColor = true;
            }

            if (_countCpu > Logic.SafeLoad)
            {
                ChangeColor(Dimension.CPU, _tickColor ? Colors.Red : Colors.White);
            }
            else
            {
                ChangeColor(Dimension.CPU, Colors.White);
            }

            if (_countMemory > Logic.SafeLoad)
            {
                ChangeColor(Dimension.Memory, _tickColor ? Colors.Red : Colors.White);
            }
            else
            {
                ChangeColor(Dimension.Memory, Colors.White);
            }

            _tickColor = !_tickColor;

            _isColor = false;

        }

        /// <summary>
        /// Удаление выбранных процессов из коллекции счетчиков процессов, а так же помечаем его как удаленный, чтобы он не добавлялся при обработке всех процессов по шаблону
        /// </summary>
        /// <param name="processCount"></param>
        public void RemoveProcess(List<ProcessCount> processCount)
        {
            lock (_lock)
            {
                foreach (ProcessCount count in processCount)
                {
                    _removedProcesses.Add(count);
                    ProcessesCounts.Remove(count);
                }
            }
        }

        /// <summary>
        /// Переинициализация файла сохранения
        /// </summary>
        public void ReInitWriter()
        {
            string newPath = Logic.ChooseSaveFile(Logic.ServerName);
            if (!string.IsNullOrWhiteSpace(newPath))
            {
                _isWrite = false;
                _writer?.Close();
                InitWriter(newPath);
            }
        }

        /// <summary>
        /// Инициализация файла сохранения
        /// </summary>
        /// <param name="path"></param>
        private void InitWriter(string path)
        {
            _writer = File.AppendText(path);
            _writer.WriteLine(Logic.START_STRING);
            _isWrite = true;
        }

        /// <summary>
        /// Остановка таймера обновления счетчиков
        /// </summary>
        private void TimerOff()
        {
            _tCount.Change(Timeout.Infinite, Timeout.Infinite);
            _tBlink.Change(Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Запуск таймера обновления счетчиков
        /// </summary>
        private void TimerOn()
        {
            _tCount.Change(new TimeSpan(0), TimeSpan.FromSeconds(Logic.SECONDS_COUNT));
            _tBlink.Change(new TimeSpan(0), TimeSpan.FromSeconds(Logic.SECONDS_BLINK));
        }

        /// <summary>
        /// Действия при закрытии Счётчика
        /// </summary>
        public void Close()
        {
            _writer?.Close();
        } 

        #endregion
    }
}
