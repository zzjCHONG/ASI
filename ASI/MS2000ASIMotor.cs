using Simscop.Pl.Core.Constants;
using Simscop.Pl.Core.Interfaces;
using System.Diagnostics;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ASI
{
    public class MS2000ASIMotor : IMotor
    {
        private readonly MS2000 _ms2000;
        private readonly uint xAxis = 1;
        private readonly uint yAxis = 2;
        private readonly uint zAxis = 3;
        private readonly Timer? _timerRefreshInfo;

        private readonly object _serialLock = new();

        public MS2000ASIMotor()
        {
            _ms2000 = new MS2000();

            _timerRefreshInfo = new Timer(200);
            _timerRefreshInfo.Elapsed += OnTimedComsEvent!;
        }

        private void OnTimedComsEvent(object sender, ElapsedEventArgs e)
        {
            GetPostionandRefresh();
            GetLimitStateandRefresh();
        }

        public void GetPostionandRefresh()
        {
            lock (_serialLock)
            {
                var res = _ms2000.GetPosition(new uint[] { xAxis, yAxis, zAxis }, out var positions);
                if (!res || positions.Count != 3)
                {
                    //todo，后续此处不输出错误提示。
                    //Console.WriteLine($"##########################################获取位置失败 {res} {positions.Count}");
                    //Debug.WriteLine($"##########################################获取位置失败 {res} {positions.Count}");
                    return;
                }
                X = positions[xAxis];
                Y = positions[yAxis];
                Z = positions[zAxis];
            }
        }

        public void GetLimitStateandRefresh()
        {
            lock (_serialLock)
            {
                var res = _ms2000.GetAxisState(new uint[] { xAxis, yAxis, zAxis }, out var states);
                if (!res || states.Count != 3)
                {
                    //Console.WriteLine("******************************************获取限位状态失败");
                    //Debug.WriteLine("******************************************获取限位状态失败");
                    return;
                }

                XLimit = states[xAxis] == "L" || states[xAxis] == "U";
                YLimit = states[yAxis] == "L" || states[yAxis] == "U";
                ZLimit = states[zAxis] == "L" || states[zAxis] == "U";
            }
        }

        public bool InitMotor(string com="")
        {
            var res = _ms2000.OpenCom(com);
            if (res) _timerRefreshInfo!.Start();
            return res;
        }

        public bool UnInitializeMotor() => _ms2000.DisConnect();

        public bool SetXPosition(double xPosition)
        {
            throw new NotImplementedException();
        }

        public bool SetYPosition(double yPosition)
        {
            throw new NotImplementedException();
        }

        public bool SetZPosition(double zPosition)
        {
            throw new NotImplementedException();
        }

        public bool SetXOffset(double x)
        {
            throw new NotImplementedException();
        }

        public bool SetYOffset(double y)
        {
            throw new NotImplementedException();
        }

        public bool SetZOffset(double z)
        {
            throw new NotImplementedException();
        }

        public Task<bool> MulAxisAbsoluteMoveAsync(Dictionary<uint, double> axisPositions) => AbsoluteMoveUnilDoneAsync(axisPositions);

        public Task<bool> SetXPositionAsync(double xPosition) => AbsoluteMoveUnilDoneAsync(new Dictionary<uint, double> { { xAxis, xPosition } });

        public Task<bool> SetYPositionAsync(double yPosition) => AbsoluteMoveUnilDoneAsync(new Dictionary<uint, double> { { yAxis, yPosition } });

        public Task<bool> SetZPositionAsync(double zPosition) => AbsoluteMoveUnilDoneAsync(new Dictionary<uint, double> { { zAxis, zPosition } });

        private CancellationTokenSource? _ctsMove;

        /// <summary>
        /// 绝对位移，等待直至到位
        /// 多轴
        /// </summary>
        /// <param name="axisPositions"></param>
        /// <param name="pollIntervalMs"></param>
        /// <returns></returns>
        private async Task<bool> AbsoluteMoveUnilDoneAsync(Dictionary<uint, double> axisPositions, int pollIntervalMs = 200)
        {
            try
            {
                _ctsMove = new CancellationTokenSource();
                var token = _ctsMove.Token;

                _timerRefreshInfo!.Stop();
                await Task.Delay(50);

                if (!await _ms2000.AbsoluteMoveAsync(axisPositions))
                {
                    Console.WriteLine("[XXX] AbsoluteMoveAsync Failed: 移动命令执行失败");
                    return false;
                }

                uint[] axisMask = axisPositions.Keys.ToArray();
                while (true)
                {
                    token.ThrowIfCancellationRequested(); 

                    GetPostionandRefresh();

                    token.ThrowIfCancellationRequested();

                    GetLimitStateandRefresh();

                    token.ThrowIfCancellationRequested();

                    if (!_ms2000.IsAxisMoving(axisMask, out var movingStates))
                    {
                        Console.WriteLine("[XXX] AbsoluteMoveUnilDoneAsync Failed: 查询轴状态失败");
                        _timerRefreshInfo!.Start();
                        return false;
                    }

                    if (movingStates.Count != 0 && movingStates.Values.All(m => !m))
                    {
                        Console.WriteLine("[XXX] AbsoluteMoveUnilDoneAsync Success: 所有轴已停止");
                        _timerRefreshInfo!.Start();
                        return true;
                    }

                    // 等待下一次查询
                    await Task.Delay(pollIntervalMs,token);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[XXX] AbsoluteMoveUnilDoneAsync Cancelled: 已被手动停止");//
                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine("[XXX] AbsoluteMoveUnilDoneAsync Failed: " + e.Message);
                _timerRefreshInfo!.Start();
                return false;
            }
        }

        public Task<bool> SetXOffsetAsync(double x) => RelativeMoveUnilDoneAsync(xAxis, x);

        public Task<bool> SetYOffsetAsync(double y) => RelativeMoveUnilDoneAsync(yAxis, y);

        public Task<bool> SetZOffsetAsync(double z) => RelativeMoveUnilDoneAsync(zAxis, z);

        /// <summary>
        /// 相对位移，等待直至到位
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="pos"></param>
        /// <param name="pollIntervalMs"></param>
        /// <returns></returns>
        private async Task<bool> RelativeMoveUnilDoneAsync(uint axis, double pos, int pollIntervalMs = 200)
        {
            try
            {
                _ctsMove = new CancellationTokenSource();
                var token = _ctsMove.Token;

                if (!await _ms2000.RelativeMoveAsync(axis, pos))
                {
                    Console.WriteLine("[XXX] RelativeMoveUnilDoneAsync Failed: 移动命令执行失败");
                    return false;
                }

                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    GetPostionandRefresh();

                    token.ThrowIfCancellationRequested();

                    GetLimitStateandRefresh();

                    token.ThrowIfCancellationRequested();

                    if (!_ms2000.IsAxisMoving(new uint[] { axis }, out var movingStates))
                    {
                        Console.WriteLine("[XXX] RelativeMoveUnilDoneAsync Failed: 查询轴状态失败");
                        _timerRefreshInfo!.Start();
                        return false;
                    }

                    if (movingStates.Count != 0 && movingStates.Values.All(m => !m))
                    {
                        Console.WriteLine("[XXX] RelativeMoveUnilDoneAsync Success: 所有轴已停止");

                        _timerRefreshInfo!.Start();
                        return true;
                    }

                    await Task.Delay(pollIntervalMs,token);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[XXX] RelativeMoveUnilDoneAsync Cancelled: 已被手动停止");
                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine("[XXX] RelativeMoveUnilDoneAsync Failed: " + e.Message);
                _timerRefreshInfo!.Start();
                return false;
            }
        }

        public bool XResetPosition()
        {
            throw new NotImplementedException();
        }

        public bool YResetPosition()
        {
            throw new NotImplementedException();
        }

        public bool ZResetPosition()
        {
            throw new NotImplementedException();
        }

        public Task<bool> XResetPositionAsync() => AbsoluteMoveUnilDoneAsync(new Dictionary<uint, double> { { xAxis, 0 } });

        public Task<bool> YResetPositionAsync() => AbsoluteMoveUnilDoneAsync(new Dictionary<uint, double> { { yAxis, 0 } });

        public Task<bool> ZResetPositionAsync() => AbsoluteMoveUnilDoneAsync(new Dictionary<uint, double> { { zAxis, 0 } });

        public bool SetOriginPos()
        {
            _timerRefreshInfo!.Stop();
            var res = _ms2000.SetHome();
            _timerRefreshInfo!.Start();
            return res;
        }

        public bool Stop()
        {
            try
            {
                _ctsMove?.Cancel();
                _timerRefreshInfo!.Stop();
                var res = _ms2000.Half();
                _timerRefreshInfo!.Start();
                return res;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[XXX] Stop Failed: " + ex.Message);
                return false;
            }
        }

        public Task<bool> OriginPosHomeAsync() => OriginHomeUnilDoneAsync(new uint[] { xAxis, yAxis, zAxis });

        /// <summary>
        /// 回原点
        /// 物理原点
        /// 等待直至到位
        /// </summary>
        /// <param name="value"></param>
        /// <param name="pollIntervalMs">校验到位间隔，单位：ms</param>
        /// <returns></returns>
        private async Task<bool> OriginHomeUnilDoneAsync(uint[] value, int pollIntervalMs = 200)
        {
            try
            {
                _ctsMove = new CancellationTokenSource();
                var token = _ctsMove.Token;

                _timerRefreshInfo!.Stop();
                await Task.Delay(50);

                if (!await _ms2000.OriginHomeAsync(value))
                {
                    Console.WriteLine("[XXX] OriginHomeAsync Failed: 移动命令执行失败");
                    return false;
                }

                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    GetPostionandRefresh();

                    token.ThrowIfCancellationRequested();

                    GetLimitStateandRefresh();

                    token.ThrowIfCancellationRequested();

                    if (!_ms2000.IsAxisMoving(value, out var movingStates))
                    {
                        Console.WriteLine("[XXX] OriginHomeAsyncUnilDoneAsync Failed: 查询轴状态失败");
                        _timerRefreshInfo!.Start();
                        return false;
                    }

                    if (movingStates.Count != 0 && movingStates.Values.All(m => !m))
                    {
                        Console.WriteLine("[XXX] OriginHomeAsyncUnilDoneAsync Success: 所有轴已停止");
                        return true;
                    }

                    await Task.Delay(pollIntervalMs);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[XXX] OriginHomeUnilDoneAsync Cancelled: 已被手动停止");
                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine("[XXX] OriginHomeUnilDoneAsync Failed: " + e.Message);
                _timerRefreshInfo!.Start();
                return false;
            }
        }

        public bool ResetParam()
        {
            _timerRefreshInfo!.Stop();
            var res = _ms2000.ResetParam();
            Thread.Sleep(1500);//系统重启需时间
            _timerRefreshInfo!.Start();
            return res;
        }

        public Dictionary<InfoEnum, string> InfoDirectory
            => new() { { InfoEnum.Model, "ASI-MS2000" }, { InfoEnum.Version, _ms2000.GetVersion(out string version) ? version : "" }, { InfoEnum.FrameWork, "" } };

        public double XSpeed
        {
            get
            {
                var res = GetSpeed(xAxis, out var speed);
                if (!res) return 0;
                return speed;
            }
            set
            {
                var res = SetSpeed(xAxis, value);
                if (!res) Console.WriteLine("设置x速度失败！");
            }
        }

        public double YSpeed
        {
            get
            {
                var res = GetSpeed(yAxis, out var speed);
                if (!res) return 0;
                return speed;
            }
            set
            {
                var res = SetSpeed(yAxis, value);
                if (!res) Console.WriteLine("设置y速度失败！");
            }
        }

        public double ZSpeed
        {
            get
            {
                var res = GetSpeed(zAxis, out var speed);
                if (!res) return 0;
                return speed;
            }
            set
            {
                var res = SetSpeed(zAxis, value);
                if (!res) Console.WriteLine("设置z速度失败！");
            }
        }

        /// <summary>
        /// 获得速度
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="speed"></param>
        /// <returns></returns>
        private bool GetSpeed(uint axis, out double speed)
        {
            _timerRefreshInfo!.Stop();
            var res = _ms2000.GetSpeed(axis, out speed);
            _timerRefreshInfo!.Start();
            return res;
        }

        /// <summary>
        /// 设置速度
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="speed"></param>
        /// <returns></returns>
        private bool SetSpeed(uint axis, double speed)
        {
            _timerRefreshInfo!.Stop();
            var res = _ms2000.SetSpeed(axis, speed);
            _timerRefreshInfo!.Start();
            return res;
        }

        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }

        public bool XLimit{ get; set; }

        public bool YLimit { get; set; }

        public bool ZLimit { get; set; }

        public bool XTaskRunning => throw new NotImplementedException();

        public bool YTaskRunning => throw new NotImplementedException();

        public bool ZTaskRunning => throw new NotImplementedException();

    }
}
