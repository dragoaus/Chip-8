using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace Chip_8
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Emulator emulator;
        private Thread backgroundWorker;
        private ObservableCollection<bool> screen;
        // For timing..
        readonly Stopwatch stopWatch = Stopwatch.StartNew();
        readonly TimeSpan targetElapsedTime60Hz;
        readonly TimeSpan targetElapsedTime;
        TimeSpan lastTime;

        private Dictionary<string, byte> KeyboardMap = new Dictionary<string, byte>()
        {
            { "0", 0x0 },
            { "1", 0x1 },
            { "2", 0x2 },
            { "3", 0x3 },
            { "4", 0x4 },
            { "5", 0x5 },
            { "6", 0x6 },
            { "7", 0x7 },
            { "8", 0x8 },
            { "9", 0x9 },
            { "A", 0xA },
            { "B", 0xB },
            { "C", 0xC },
            { "D", 0xD },
            { "E", 0xE },
            { "F", 0xF }
        };

        public MainWindow()
        {
            InitializeComponent();
            targetElapsedTime60Hz = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);
            targetElapsedTime = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 1000);
            screen = new ObservableCollection<bool>(new bool[64*32]);
            emulator = new Emulator();
            emulator.InitializeChip8();
            this.DataContext = this;
            Screen.ItemsSource = screen;
            backgroundWorker = new Thread(Run);

        }

        /// <summary>
        /// Runs emulation
        /// </summary>
        void Run()
        {
            while (true)
            {
                var currentTime = stopWatch.Elapsed;
                var elapsedTime = currentTime - lastTime;

                while (elapsedTime >= targetElapsedTime60Hz)
                {
                    Dispatcher.BeginInvoke(Tick60Hz);
                    elapsedTime -= targetElapsedTime60Hz;
                    lastTime += targetElapsedTime60Hz;
                }
                Dispatcher.BeginInvoke(emulator.EmulationCycle);
                Thread.Sleep(targetElapsedTime);  
            }
        }

        /// <summary>
        /// Ticks in 60Hz
        /// </summary>
        void Tick60Hz()
        {
            emulator.Tick60Hz();
            ScreenRefresh();
        }

        /// <summary>
        /// refresh screen
        /// </summary>
        public void ScreenRefresh()
        {
            if (emulator.drawFlag == true)
            {
                for (int i = 0; i < 2048; i++)
                {
                    screen[i] = emulator.gfx[i] == 1;
                }
            }
            emulator.drawFlag = false;
        }


        private void Button_Open_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Text (txt, ch8)|*.txt;*.ch8;";
            openFileDialog.ShowDialog();
            if (openFileDialog.FileName != "")
            {
                FilePath.Text = openFileDialog.FileName;
                emulator.LoadApplication(openFileDialog.FileName);
                Open.IsEnabled = false;
            }
            else
            {
                return;
            }

            backgroundWorker.Start();

        }

        //Mouse Input events and handling
        private void PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            emulator.key[KeyboardMap[GetMouseKey(sender as Button)]] = 0x1;
        }
        private void PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            emulator.key[KeyboardMap[GetMouseKey(sender as Button)]] = 0x0;
        }
        private string GetMouseKey(Button sender)
        {
            return sender.Content.ToString();
        }

        //Keyboard Input events and handling
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            emulator.key[KeyboardMap[GetKeyboardKey(e)]] = 0x1;
        }
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            emulator.key[KeyboardMap[GetKeyboardKey(e)]] = 0x0;
        }
        private string GetKeyboardKey(KeyEventArgs e)
        {
            return e.Key.ToString()[^1].ToString();
        }
    }
}
