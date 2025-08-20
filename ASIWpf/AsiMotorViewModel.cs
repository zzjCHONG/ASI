using ASI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simscop.Pl.Core.Interfaces;
using System.Diagnostics;
using System.IO.Ports;
using System.Timers;
using System.Windows;
using System.Windows.Threading;

namespace ASIWpf
{
  public partial  class AsiMotorViewModel: ObservableObject
    {
        private readonly IMotor? _motor;
        private readonly DispatcherTimer _timerPos;
        private readonly System.Timers.Timer? _timerComs;
        private static readonly string? currentPortname;

        public AsiMotorViewModel()
        {
            _motor =new MS2000ASIMotor();
            _timerPos = new DispatcherTimer(priority: DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };

            SerialComs?.AddRange(SerialPort.GetPortNames());
            if (_timerComs == null)
            {
                _timerComs = new System.Timers.Timer(100);
                _timerComs.Elapsed += OnTimedComsEvent!;
                _timerComs.AutoReset = true;
                _timerComs.Enabled = true;
            }
        }

        [ObservableProperty]
        private List<string>? _serialComs = new();

        [ObservableProperty]
        public int _serialIndex = 0;

        private void OnTimedComsEvent(object sender, ElapsedEventArgs e)
        {
            try
            {
                var com = SerialPort.GetPortNames();

                bool areEqual = SerialComs?.Count == com.Length
                    && !SerialComs.Except(com).Any() && !com.Except(SerialComs).Any();
                if (!areEqual)
                {
                    SerialComs = new();
                    SerialComs.AddRange(com);
                    if (SerialComs.Count != 0)
                    {
                        if (!string.IsNullOrEmpty(currentPortname) && IsConnect)
                        {
                            int index = SerialComs.IndexOf(currentPortname);
                            SerialIndex = index;
                        }
                        else
                        {
                            SerialIndex = SerialComs.Count - 1;
                        }
                    }

                    if (!SerialComs.Contains(currentPortname!) && !string.IsNullOrEmpty(currentPortname))
                        IsConnect = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("OnTimedComsEvent" + ex.ToString());
            }
        }

        public bool IsOperable => !IsConnect;

        [NotifyPropertyChangedFor(nameof(IsOperable))]
        [ObservableProperty]
        public bool _isConnect = false;

        [ObservableProperty]
        public bool _isZAxisEnable = true;

        [ObservableProperty]
        public double _x = 0;

        [ObservableProperty]
        public double _y = 0;

        [ObservableProperty]
        public double _z = 0;

        [ObservableProperty]
        public bool _xLimit;

        [ObservableProperty]
        public bool _yLimit;

        [ObservableProperty]
        public bool _zLimit;

        [ObservableProperty]
        public bool _xTaskRunning;

        [ObservableProperty]
        public bool _yTaskRunning;

        [ObservableProperty]
        public bool _zTaskRunning;

        [ObservableProperty]
        public double _xStep = 100;

        [ObservableProperty]
        public double _yStep = 100;

        [ObservableProperty]
        public double _zStep = 100;

        [ObservableProperty]
        public double _targetX = 0;

        [ObservableProperty]
        public double _targetY = 0;

        [ObservableProperty]
        public double _targetZ = 0;

        [RelayCommand]
        async Task Init()
        {
            await Task.Run(() =>
            {
                IsConnect = _motor!.InitMotor("");
            });

            if (IsConnect)
            {
                InitSetting();

                _timerPos.Tick += Timer_Tick;
                _timerPos.Start();

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(Application.Current.MainWindow, $"连接成功！", "连接提示", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            else
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(Application.Current.MainWindow, $"连接失败！", "连接提示", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
        }

        [RelayCommand]
        async Task InitManual()
        {
            var com = SerialComs![SerialIndex];
            await Task.Run(() =>
            {
                IsConnect = _motor!.InitMotor(com);
            });

            if (IsConnect)
            {
                InitSetting();
                _timerPos.Tick += Timer_Tick;
                _timerPos.Start();

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(Application.Current.MainWindow, $"连接成功！", "连接提示", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            else
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(Application.Current.MainWindow, $"连接失败！", "连接提示", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
        }

        void InitSetting()
        {
            XSpeed = _motor!.XSpeed;
            YSpeed = _motor.YSpeed;
            ZSpeed = _motor!.ZSpeed;
        }

        [ObservableProperty]
        private double _xSpeed;

        partial void OnXSpeedChanged(double value)
        {
            _motor!.XSpeed = value;
        }

        [ObservableProperty]
        private double _ySpeed;
        partial void OnYSpeedChanged(double value)
        {
            _motor!.YSpeed = value;
        }

        [ObservableProperty]
        private double _zSpeed;

        partial void OnZSpeedChanged(double value)
        {
            _motor!.ZSpeed = value;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            Task.Run(() => 
            {
                X = _motor!.X;
                Y = _motor!.Y;
                Z = _motor!.Z;

                XLimit = _motor!.XLimit;
                YLimit = _motor!.YLimit;
                ZLimit = _motor!.ZLimit;
            });
        }

        [RelayCommand]
        void GetCurrentPosition()
        {
            TargetX = Math.Round(X, 1);
            TargetY = Math.Round(Y, 1);
            TargetZ = IsZAxisEnable ? Math.Round(Z, 1) : 0;
        }

        [RelayCommand]
        async Task SetAbsolutePosition()
        {
            var pos = new Dictionary<uint, double>() { { 1, TargetX }, { 2, TargetY }, { 3, TargetZ } };
            var res = await _motor!.MulAxisAbsoluteMoveAsync(pos);
            if (!res) Console.WriteLine("绝对移动错误！");
        }

        [RelayCommand]
        async Task SetXRelativePosition()
        {
            if (!await _motor!.SetXOffsetAsync(XStep))
                Console.WriteLine("X轴相对移动错误！");
        }

        [RelayCommand]
        async Task SetYRelativePosition()
        {
            if (!await _motor!.SetYOffsetAsync(YStep))
                Console.WriteLine("Y轴相对移动错误！");
        }

        [RelayCommand]
        async Task SetZRelativePosition()
        {
            if (IsZAxisEnable)
                if (!await _motor!.SetZOffsetAsync(ZStep))
                    Console.WriteLine("Z轴相对移动错误！");
        }

        [RelayCommand]
        async Task SetXInverseRelativePosition()
        {
            if (!await _motor!.SetXOffsetAsync(-1.0 * XStep))
               Console.WriteLine("X轴相对移动错误！");
        }

        [RelayCommand]
        async Task SetYInverseRelativePosition()
        {
            if (!await _motor!.SetYOffsetAsync(-1.0 * YStep))
               Console.WriteLine("Y轴相对移动错误！");
        }

        [RelayCommand]
        async Task SetZInverseRelativePosition()
        {
            if (IsZAxisEnable)       
                if (!await _motor!.SetZOffsetAsync(-1.0 * ZStep))
                   Console.WriteLine("Z轴相对移动错误！");
        }

        [RelayCommand]
        async Task XHome()
        {
            if (!await _motor!.XResetPositionAsync())
               Console.WriteLine("X轴回原点错误！");
        }

        [RelayCommand]
        async Task YHome()
        {
            if (!await _motor!.YResetPositionAsync())
               Console.WriteLine("Y轴回原点错误！");
        }

        [RelayCommand]
        async Task ZHome()
        {
            if (IsZAxisEnable)
                if (!await _motor!.ZResetPositionAsync())
                   Console.WriteLine("Z轴回原点错误！");
        }

        [RelayCommand]
        void SetOriginPos()
        {
            if (!_motor!.SetOriginPos())
               Console.WriteLine("设置原点错误！");
        }

        [RelayCommand]
        void Stop()
        {
            var res =  _motor!.Stop();
            if (!res)
                Console.WriteLine("急停错误!");
        }

        [RelayCommand]
        async Task OriginPosHome()
        {
            var res = await _motor!.OriginPosHomeAsync();
            if (!res)
                Console.WriteLine("回物理原点错误!");
        }

        [RelayCommand]
        void Reset()
        {
            var res = _motor!.ResetParam();
            if (!res)
                Console.WriteLine("复位变量错误！");
        }

        [RelayCommand]
        async Task ScanforStitching()
        {
            double XStart = 0;
            double YStart = 0;
            double XEnd = 2000;
            double YEnd = 2000;

            (int Width, int Height) img = new(1600, 1100);

            int XClipCount = 200;//采样横向像素裁切数
            int YClipCount = 200;//采样纵向像素裁切数

            double unit = 0.5;//比例尺，μm/pix

            bool IsScanbySnakeLike = true;//是否蛇形扫描

            var width = img.Width - 2.0 * XClipCount;
            var height = img.Height - 2.0 * YClipCount;
            width *= unit;
            height *= unit;

            var x = Math.Min(XStart, XEnd);
            var y = Math.Min(YStart, YEnd);
            var xCount = (int)Math.Ceiling(Math.Abs(XStart - XEnd) / width);
            var yCount = (int)Math.Ceiling(Math.Abs(YStart - YEnd) / height);
            xCount = Math.Max(xCount, 1);
            yCount = Math.Max(yCount, 1);

            var props = new List<Point>();
            var xPos = x;
            var yPos = y;

            var count = xCount * yCount;
            for (var i = 0; i < yCount; i++)
            {
                yPos = y + i * height;
                for (var j = 0; j < xCount; j++)
                {
                    xPos = IsScanbySnakeLike ? (x + (i % 2 == 0 ? (j * width) : (xCount - j - 1) * width)) : (x + j * width);
                    props.Add(new Point((float)xPos, (float)yPos));
                }
            }

            if (props!.Count <= 0) return;

            var output = string.Join(" ", props.Select(p => $"（{p.X}，{p.Y}）"));
            var res = MessageBox.Show($"STITCH-确认扫描？具体点位：{output}\r\n共计{props.Count}个\r\n间距：X_{width} Y_{height}", "提醒", MessageBoxButton.OKCancel, MessageBoxImage.Information);
            if (res == MessageBoxResult.OK)
            {
                var sp = Stopwatch.StartNew();
                foreach (var item in props)
                {
                    await _motor!.SetXPositionAsync(item.X);
                    await _motor.SetYPositionAsync(item.Y);

                    Debug.WriteLine($"X_{item.X},Y_{item.Y}");
                }
                sp.Stop();
                Debug.WriteLine($"STITCH-FINISH_{props.Count}_{sp.ElapsedMilliseconds}ms!\r\n");
            }
        }

        [RelayCommand]
        async Task ScanforRaman()
        {
            double RectXStart = -1500;
            double RectYStart = -300;
            double RectXEnd = -1200;
            double RectYEnd = 100;
            var startx = Math.Min(RectXStart, RectXEnd);
            var starty = Math.Min(RectYStart, RectYEnd);

            ////控制数量
            //int RectXCount = 10;
            //int RectYCount = 10;//10*10
            //double RectXInterval = Math.Abs(RectXStart - RectXEnd) / RectXCount;
            //double RectYInterval = Math.Abs(RectYStart - RectYEnd) / RectYCount;//间距

            //控制间距
            double RectXInterval = 40;
            double RectYInterval = 40;
            int RectXCount = (int)Math.Floor(Math.Abs(RectYStart - RectYEnd) / RectXInterval);
            int RectYCount = (int)Math.Floor(Math.Abs(RectYStart - RectYEnd) / RectYInterval);

            bool IsScanbySnakeLike = true;//是否蛇形扫描

            var props = new List<Point>();
            for (var i = 0; i < RectYCount; i++)
            {
                for (var j = 0; j < RectXCount; j++)
                {
                    var column = IsScanbySnakeLike ? i % 2 == 0 ? j : (RectXCount - j - 1) : j;
                    var x = startx + RectXInterval / 2 + column * RectXInterval;
                    var y = starty + RectYInterval / 2 + i * RectYInterval;

                    props.Add(new Point(x, y));
                }
            }

            if (props.Count > 200)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(Application.Current.MainWindow, $"数量过多-{props.Count}！请重新设置！", "扫描提示", MessageBoxButton.OK, MessageBoxImage.Information);
                });
                return;
            }

            var output = string.Join(" ", props.Select(p => $"（{p.X}，{p.Y}）"));
            var res = MessageBox.Show($"RAMAN-确认扫描？具体点位：{output}\r\n共计{props.Count}个\r\n间距：X_{RectXInterval} Y_{RectYInterval}", "提醒", MessageBoxButton.OKCancel, MessageBoxImage.Information);
            if (res == MessageBoxResult.OK)
            {
                var index = 0;
                var sp = Stopwatch.StartNew();
                foreach (var item in props)
                {
                    index++;
                    await _motor!.SetXPositionAsync(item.X);
                    await _motor.SetYPositionAsync(item.Y);
                    await Task.Delay(10);
                    Debug.WriteLine($"{index} X_{item.X}__{_motor!.X:0.00}__{Math.Abs(item.X - _motor!.X):0.00},Y_{item.Y}__{_motor!.Y:0.00}__{Math.Abs(item.Y - _motor!.Y):0.00}");
                }
                sp.Stop();
                Debug.WriteLine($"RAMAN-FINISH_{props.Count}_{sp.ElapsedMilliseconds}ms!\r\n");
            }
        }

        [RelayCommand]
        async Task ScanforZAxis()
        {
            // 区间模式：下限=0，上限=10，步进=2，数量×
            var props1 = GenerateZPoints(0, -75, 75, 2, -1);

            // 区间模式：下限=0，上限=10，步进×，数量=5
            var props2 = GenerateZPoints(0, 0, 10, -1, 5);

            // 起点模式：起点=5，终点×，步进=3，数量=4
            var props3 = GenerateZPoints(1, 15, -1, 1, 10);

            // 终点模式：终点10，步长2，共5个点
            var props4 = GenerateZPoints(2, -1, 10, 3, 17); // [2, 4, 6, 8, 10]

            // 中点模式：中心5，步长1，共5个点
            var props5 = GenerateZPoints(3, 5, -1, 4, 35); // [3, 4, 5, 6, 7]

            var props = props5;

            var output = string.Join(" ", props);
            var res = MessageBox.Show($"Z-确认扫描？具体点位：{output}\r\n共计{props.Count}个", "提醒", MessageBoxButton.OKCancel, MessageBoxImage.Information);
            if (res == MessageBoxResult.OK)
            {
                var index = 0;
                var sp = Stopwatch.StartNew();
                foreach (var item in props)
                {
                    index++;
                    await _motor!.SetZPositionAsync(item);
         
                    await Task.Delay(10);
                    Debug.WriteLine($"{index} Z_{item}__{_motor!.Z:0.00}__{Math.Abs(item - _motor!.Z):0.00}");
                }
                sp.Stop();
                Debug.WriteLine($"Z-FINISH_{props.Count}_{sp.ElapsedMilliseconds}ms!\r\n");
            }
        }

        /// <summary>
        /// 生成Z轴点位集合
        /// </summary>
        /// <param name="mode">
        /// 模式：
        /// 0 = 区间模式（zStart, zEnd, step 或 count）
        /// 1 = 起点模式（zStart, step, count）
        /// 2 = 终点模式（zEnd, step, count）
        /// 3 = 中点模式（zCenter, step, count）
        /// </param>
        /// <param name="zStart">起点 / 下限 / 中点</param>
        /// <param name="zEnd">上限 / 终点</param>
        /// <param name="step">步进</param>
        /// <param name="count">数量（点数）</param>
        /// <returns>Z点位集合</returns>
        public static List<double> GenerateZPoints(int mode, double zStart, double zEnd, double step, int count)
        {
            var points = new List<double>();

            switch (mode)
            {
                case 0: // 区间模式：zStart,zEnd,step 或 count
                    if (count > 0)
                    {
                        // 按数量均分
                        double interval = (zEnd - zStart) / (count - 1);
                        for (int i = 0; i < count; i++)
                            points.Add(Math.Round(zStart + i * interval, 6));
                    }
                    else if (step > 0)
                    {
                        if (zStart <= zEnd)
                        {
                            for (double z = zStart; z <= zEnd; z += step)
                                points.Add(Math.Round(z, 6));
                        }
                        else
                        {
                            for (double z = zStart; z >= zEnd; z -= step)
                                points.Add(Math.Round(z, 6));
                        }
                    }
                    break;

                case 1: // 起点模式：zStart, step, count
                    for (int i = 0; i < count; i++)
                        points.Add(Math.Round(zStart + i * step, 6));
                    break;

                case 2: // 终点模式：zEnd, step, count
                    for (int i = count - 1; i >= 0; i--)
                        points.Insert(0, Math.Round(zEnd - i * step, 6));
                    break;

                case 3: // 中点模式：zStart 作为中心点 zCenter
                    if (count <= 0) break;

                    int half = (count - 1) / 2;
                    for (int i = -half; i <= half; i++)
                        points.Add(Math.Round(zStart + i * step, 6));

                    // 如果是偶数个点，最后一个点会多出来，需要按需求处理
                    if (count % 2 == 0)
                        points.RemoveAt(0); // 移除第一个点，使区间对称
                    break;
            }

            return points;
        }

    }
}
