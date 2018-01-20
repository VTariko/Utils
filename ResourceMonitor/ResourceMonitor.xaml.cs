using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace ResourceMonitorVT
{
    /// <summary>
    /// Interaction logic for ResourceMonitor.xaml
    /// </summary>
    public partial class ResourceMonitor : Window
    {
        public CountersLogic Counters { get; set; }


        public ResourceMonitor()
        {
            InitializeComponent();

            this.Topmost = true;
            this.Deactivated += OnDeactivated;
            Counters = new CountersLogic();
            DataContext = Counters;
            btnConnect.Click += BtnConnect_Click;
            btnDelProcess.Click += BtnDelProcess_Click;
            btnSave.Click += BtnSave_Click;
            lvCommon.SizeChanged += LvCommonOnSizeChanged;
            cbProcesses.SelectionChanged += CbProcessesSelectionChanged;
            btnDelProcess.IsEnabled = false;
            btnSave.IsEnabled = false;
            cbProcesses.IsEnabled = false;

            Counters.ChangeColor += OnChangeColor;
        }

        private void OnChangeColor(Dimension source, Color value)
        {
            switch (source)
            {
                case Dimension.CPU:
                    Dispatcher.BeginInvoke((Action)delegate
                    {
                        this.tbCpuTotal.Background = new SolidColorBrush(value);
                    });
                    break;
                case Dimension.Memory:
                    Dispatcher.BeginInvoke((Action)delegate
                    {
                        this.tbMemoryTotal.Background = new SolidColorBrush(value);
                    });
                    break;
            }
        }

        private void CbProcessesSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            object item = cbProcesses.SelectedItem;
            if (item is Process process)
            {
                Counters.SelectedProcess(process);
            }
        }

        private void OnDeactivated(object sender, EventArgs eventArgs)
        {
            this.Topmost = true;
            this.Activate();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs routedEventArgs)
        {
            Counters.ReInitWriter();
        }

        private void BtnDelProcess_Click(object sender, RoutedEventArgs e)
        {
            List<ProcessCount> listForDel = new List<ProcessCount>();
            foreach (object item in lvCommon.SelectedItems)
            {
                if (item is ProcessCount o)
                {
                    listForDel.Add(o);
                }
            }
            Counters.RemoveProcess(listForDel);
        }

        private void LvCommonOnSizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs)
        {
            Logic.ReSizeColumn(this);
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            string server = txtServer.Text;
            if (!string.IsNullOrWhiteSpace(server))
            {
                if (Logic.InitConnect(this, server))
                {
                    btnDelProcess.IsEnabled = true;
                    btnSave.IsEnabled = true;
                    cbProcesses.IsEnabled = true;
                    this.ResizeMode = ResizeMode.CanMinimize;
                    //_tBlink.Change(new TimeSpan(0), TimeSpan.FromSeconds(Logic.SECONDS_BLINK));
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            Counters.Close();
            base.OnClosing(e);
        }
    }
}
