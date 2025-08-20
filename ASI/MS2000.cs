using System.IO.Ports;
using System.Text.RegularExpressions;

namespace ASI
{
    public partial class MS2000
    {
        private readonly SerialPort? _serialPort;
        private string? _portName;
        private readonly ManualResetEventSlim _dataReceivedEvent = new(false);
        private string _receivedDataforValid = string.Empty;
        private readonly int _validTimeout = 1500;

        public MS2000()
        {
            _serialPort = new SerialPort()
            {
                BaudRate = 9600,
                StopBits = StopBits.One,
                DataBits = 8,
                Parity = Parity.None,
            };
        }

        public bool OpenCom(string com = "")
        {
            if (Valid(com))
            {
                Console.WriteLine(_portName);
                _serialPort!.Open();

                _serialPort.DataReceived += SerialPort_DataReceived;
                return true;
            }
            return false;
        }

        private bool Valid(string com)
        {
            try
            {
                bool isAutoMode = com == "";

                if (isAutoMode)
                {
                    string[] portNames = SerialPort.GetPortNames();
                    foreach (string portName in portNames)
                    {
                        if (!CheckPort(portName)) continue;

                        if (_serialPort!.IsOpen) _serialPort.Close();

                        _serialPort.PortName = portName;
                        _serialPort.DataReceived -= SerialPort_DataReceived_Valid;
                        _serialPort.DataReceived += SerialPort_DataReceived_Valid;

                        _dataReceivedEvent.Reset();
                        _receivedDataforValid = string.Empty;

                        _serialPort.Open();
                        _serialPort.Write("N\r"); // 发送验证命令

                        if (_dataReceivedEvent.Wait(_validTimeout))
                        {
                            if (!string.IsNullOrEmpty(_receivedDataforValid) && _receivedDataforValid.Contains("MS2000"))
                            {
                                _portName = portName;
                                _serialPort.Close();
                                break;
                            }
                        }

                        _serialPort.Close();
                    }

                    return !string.IsNullOrEmpty(_portName);
                }
                else
                {
                    if (!CheckPort(com)) return false;

                    if (_serialPort!.IsOpen) _serialPort.Close();

                    _serialPort.PortName = com;
                    _serialPort.DataReceived -= SerialPort_DataReceived_Valid;
                    _serialPort.DataReceived += SerialPort_DataReceived_Valid;

                    _dataReceivedEvent.Reset();
                    _receivedDataforValid = string.Empty;

                    _serialPort.Open();
                    _serialPort.Write("N\r"); // 发送验证命令

                    if (_dataReceivedEvent.Wait(_validTimeout))
                    {
                        Console.WriteLine("未超时:" + _receivedDataforValid);
                        if (!string.IsNullOrEmpty(_receivedDataforValid) && _receivedDataforValid.Contains("MS2000"))
                        {
                            _portName = com;
                            _serialPort.Close();
                            return true;
                        }
                    }
                    else
                    {
                        Console.WriteLine("超时:" + _validTimeout);
                    }

                    _serialPort.Close();

                    return !string.IsNullOrEmpty(_portName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Valid_" + ex.Message);
                return false;
            }
            finally
            {
                _serialPort!.DataReceived -= SerialPort_DataReceived_Valid;
            }
        }

        private void SerialPort_DataReceived_Valid(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = _serialPort!.ReadExisting();
                if (!string.IsNullOrEmpty(data))
                {
                    _receivedDataforValid += data;

                    while (_receivedDataforValid.Contains('\r'))
                    {
                        _dataReceivedEvent.Set(); // 通知有数据
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("SerialPort_DataReceived_" + ex.Message);
            }
        }

        private static bool CheckPort(string portName)
        {
            SerialPort port = new SerialPort(portName);
            try
            {
                port.Open();
                Console.WriteLine($"串口 {portName} 未被占用");
                if (port.IsOpen) port.Close();
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"串口 {portName} 已被占用");

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"打开串口 {portName} 发生错误: {ex.Message}");
                return true;
            }
        }

        public bool DisConnect()
        {
            _portName = string.Empty;
            if (_serialPort!.IsOpen)
                _serialPort.Close();
            return true;
        }

        public void Dispose()
        {
            DisConnect();
            _serialPort!.Dispose();
        }

        ~MS2000() => Dispose();
    }

    public partial class MS2000
    {
        private readonly Dictionary<uint, string> map = new() { { 1, "X" }, { 2, "Y" }, { 3, "Z" } };

        private readonly double unitTran = 0.1;

        /// <summary>
        /// 获取版本号
        /// </summary>
        /// <returns></returns>
        public bool GetVersion(out string version)
        {
            version = string.Empty;
            try
            {
                string command = $"V";

                if (!SendCommand(command, out var respond)) return false;

                if (!CheckReturnMsg(command, respond)) return false;

                version = respond;

                Console.WriteLine("[XXX] GetVersion Success");

                return string.IsNullOrEmpty(version);
            }
            catch (Exception e)
            {
                Console.WriteLine("[XXX] GetVersion Failed:" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 获取轴位置
        /// </summary>
        /// <param name="axes"></param>
        /// <returns></returns>
        public bool GetPosition(uint[] axes, out Dictionary<uint, double> positions)
        {
            positions = new();
            try
            {
                var axis = string.Join(" ", axes.Where(v => map.ContainsKey(v)).Select(v => map[v]));
                string command = $"W {axis}";

                if (!SendCommand(command, out var resp)) return false;

                if (!CheckReturnMsg(command, resp)) return false;

                var disp = resp.Replace("\r\n", "").Replace(":A ", "");
                string[] values = disp.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (values.Length == 0)
                {
                    Console.WriteLine("GetPosition_Error_" + resp);
                }

                for (int i = 0; i < axes.Length && i < values.Length; i++)
                {
                    if (int.TryParse(values[i], out int pos))
                    {
                        positions[axes[i]] = Math.Round(pos * unitTran, 5);
                    }
                }

                //Console.WriteLine("[XXX] GetPosition Success");
                return true;

            }
            catch (Exception e)
            {
                Console.WriteLine("[XXX] GetPosition Failed:" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 获取轴状态
        /// fs-快速慢速模式
        /// ul-上限下限
        /// </summary>
        /// <param name="axes"></param>
        /// <returns></returns>
        public bool GetAxisState(uint[] axes, out Dictionary<uint, string> states)
        {
            states = new();
            try
            {
                var axis = string.Join(" ", axes.Where(v => map.ContainsKey(v)).Select(v => $"{map[v]}-"));
                string command = $"RS {axis}";

                if (!SendCommand(command, out var resp)) return false;

                if (!CheckReturnMsg(command, resp)) return false;

                var disp = resp.Replace("\r\n", "").Replace(":A", "");
                var values = disp.Select(c => c.ToString()).ToArray();//fff，返回字符之间没有空格

                if (values.Length == 0)
                {
                    Console.WriteLine("GetAxisState_Error_" + resp);
                }

                for (int i = 0; i < axes.Length && i < values.Length; i++)
                {
                    states[axes[i]] = values[i];
                }

                //Console.WriteLine("[XXX] GetAxisState Success");
                return true;

            }
            catch (Exception e)
            {
                Console.WriteLine("[XXX] GetAxisState Failed:" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 是否处于移动状态
        /// 多轴
        /// </summary>
        /// <param name="axes"></param>
        /// <returns></returns>
        public bool IsAxisMoving(uint[] axes, out Dictionary<uint, bool> axisMovingStates)
        {
            //rs + =>能返回的是手动杆的移动信号
            //RS =>返回未知信号
            //RS ? =>获取移动状态信号，移动中则B，否则为N

            axisMovingStates = new();
            try
            {
                var axis = string.Join(" ", axes.Where(v => map.ContainsKey(v)).Select(v => $"{map[v]}?"));
                string command = $"RS {axis}";

                if (!SendCommand(command, out var resp)) return false;

                if (!CheckReturnMsg(command, resp)) return false;

                resp = resp.Replace("\r\n", "").Replace(":A", "");
                string[] values = resp.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < axes.Length && i < values.Length; i++)
                {
                    axisMovingStates[axes[i]] = values[i].Contains('B');//B|N才校验，B为移动中
                }

                Console.WriteLine("[XXX] GetAxisState Success");
                return true;

            }
            catch (Exception e)
            {
                Console.WriteLine("[XXX] GetAxisState Failed:" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 获取速度
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="speed"></param>
        /// <returns></returns>
        public bool GetSpeed(uint axis,out double speed)
        {
            speed = 0;
            try
            {
                if (!map.TryGetValue(axis, out var str)) return false;
                string command = $"S {str}?";

                if (!SendCommand(command, out var resp)) return false;

                if (!CheckReturnMsg(command, resp)) return false;

                resp = resp.Replace("\r\n", "").Replace(":A ", "");
                var match = Regex.Match(resp.ToString(), @"-?\d+(\.\d+)?");

                if (match.Success)
                    speed = double.Parse(match.Value);

                Console.WriteLine("[XXX] GetSpeed Success");

                return true;

            }
            catch (Exception e)
            {
                Console.WriteLine("[XXX] GetSpeed Failed:" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 设置速度
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="speed"></param>
        /// <returns></returns>
        public bool SetSpeed(uint axis,  double speed)
        {
            try
            {
                if (!map.TryGetValue(axis, out var str)) return false;
                string command = $"S {str}={speed}";//S X=1.23 Y=3.21 Z=0.2

                if (!SendCommand(command, out var resp)) return false;

                if (!CheckReturnMsg(command, resp)) return false;

                Console.WriteLine("[XXX] SetSpeed Success");
                return true;

            }
            catch (Exception e)
            {
                Console.WriteLine("[XXX] SetSpeed Failed:" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 获取轴速度
        /// 多轴
        /// </summary>
        /// <param name="axes"></param>
        /// <returns></returns>
        public bool GetSpeeds(uint[] axes, out Dictionary<uint, double> speeds)
        {
            speeds = new();
            try
            {
                var axis = string.Join(" ", axes.Where(v => map.ContainsKey(v)).Select(v => $"{map[v]}?"));
                string command = $"S {axis}";
                if (!SendCommand(command, out var resp)) return false;

                if (!CheckReturnMsg(command, resp)) return false;

                resp = resp.Replace("\r\n", "").Replace(":A ", "");
                string[] values = resp.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < axes.Length && i < values.Length; i++)
                {
                    var match = Regex.Match(values[i].ToString(), @"-?\d+(\.\d+)?");
                    if (match.Success) speeds[axes[i]] = double.Parse(match.Value);
                }

                Console.WriteLine("[XXX] GetSpeeds Success");

                return true;

            }
            catch (Exception e)
            {
                Console.WriteLine("[XXX] GetSpeeds Failed:" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 设置速度
        /// 多轴
        ///  Maximum speed is = 7.5 mm/s for standard 6.5 mm pitch leadscrews
        /// </summary>
        /// <param name="axisSpeeds"></param>
        /// <returns></returns>
        public bool SetSpeeds(Dictionary<uint, double> axisSpeeds)
        {
            try
            {
                var axis = string.Join(" ", axisSpeeds
                    .Where(kv => map.ContainsKey(kv.Key)) 
                    .Select(kv => $"{map[kv.Key]} = {kv.Value}"));//S X=1.23 Y=3.21 Z=0.2

                string command = $"S {axis}";

                if (!SendCommand(command, out var resp)) return false;

                if (!CheckReturnMsg(command, resp)) return false;

                Console.WriteLine("[XXX] SetSpeedAsync Success");
                return true;

            }
            catch (Exception e)
            {
                Console.WriteLine("[XXX] SetSpeedAsync Failed:" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 重置
        /// 所有变量恢复至设定数值
        /// 即控制器后Reset按键
        /// </summary>
        /// <returns></returns>
        public bool ResetParam()
        {
            try
            {
                string command = "RESET";

                if (!SendCommand(command, out var resp)) return false;

                if (!CheckReturnMsg(command, resp)) return false;

                Console.WriteLine("[XXX] ResetParam Success");
                return true;

            }
            catch (Exception e)
            {
                Console.WriteLine("[XXX] ResetParam Failed:" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 设置零点
        /// </summary>
        /// <returns></returns>
        public bool SetHome()
        {
            //HM，设定固定硬件的起始位置
            //H,定义该位置为距离零点若干位置的点（即原点会发生变化）。若设置单个轴，使用此命令
            //Z，定义当前位置为零点，无需输入轴。
            try
            {
                string command = "Z";

                if (!SendCommand(command, out var resp)) return false;

                if (!CheckReturnMsg(command, resp)) return false;

                Console.WriteLine("[XXX] SetHome Success");
                return true;

            }
            catch (Exception e)
            {
                Console.WriteLine("[XXX] SetHome Failed:" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 急停
        /// </summary>
        /// <returns></returns>
        public bool Half()
        {
            try
            {
                string command = "HALT";
                if (!SendCommand(command, out var resp)) return false;

                if (!CheckReturnMsg(command, resp)) return false;

                Console.WriteLine("[XXX] Half Success");
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("[XXX] Half Failed:" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 绝对位移
        /// 多轴
        /// </summary>
        /// <param name="axisPositions">轴号1~3，对应位置，单位0.1微米</param>
        /// <returns></returns>
        public async Task<bool> AbsoluteMoveAsync(Dictionary<uint, double> axisPositions)
        {
            try
            {
                var axis = string.Join(" ", axisPositions 
                    .Where(kv => map.ContainsKey(kv.Key)) 
                    .Select(kv => $"{map[kv.Key]} = {(int)(kv.Value / unitTran)}")); 

                string command = $"M {axis}";

                var (ok, resp) = await SendCommandAsync(command);
                if (ok)
                {
                    if (!CheckReturnMsg(command, resp)) return false;

                    Console.WriteLine("[XXX] AbsoluteMoveAsync Success");
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine("[XXX] AbsoluteMoveAsync Failed:" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 相对位移
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        public async Task<bool> RelativeMoveAsync(uint axis,double pos)
        {
            try
            {
                if (!map.TryGetValue(axis, out var str)) return false;
                string command = $"R {str}={(int)(pos / unitTran)}";

                var (ok, resp) = await SendCommandAsync(command);
                if (ok)
                {
                    if (!CheckReturnMsg(command, resp)) return false;

                    Console.WriteLine("[XXX] RelativeMoveAsync Success");
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine("[XXX] RelativeMoveAsync Failed:" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 相对位移
        /// 多轴
        /// </summary>
        /// <param name="axisPositions">轴号1~3，对应位置，单位0.1微米</param>
        /// <returns></returns>
        private async Task<bool> RelativeMoveAsync(Dictionary<uint, double> axisPositions)
        {
            try
            {
                var axis = string.Join(" ", axisPositions 
                    .Where(kv => map.ContainsKey(kv.Key))
                    .Select(kv => $"{map[kv.Key]} = {(int)(kv.Value / unitTran)}"));

                string command = $"R {axis}";

                var (ok, resp) = await SendCommandAsync(command);
                if (ok)
                {
                    if (!CheckReturnMsg(command, resp)) return false;

                    Console.WriteLine("[XXX] RelativeMoveAsync Success");
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine("[XXX] RelativeMoveAsync Failed:" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 回原点
        /// 物理原点
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<bool> OriginHomeAsync(uint[] value)
        {
            //XY找寻上下限后回到设置的原点（不是零点）
            try
            {
                var axis = string.Join(" ", value.Where(v => map.ContainsKey(v)).Select(v => map[v]));
                string command = $"SI {axis}";

                var (ok, resp) = await SendCommandAsync(command);
                if (ok)
                {
                    if (!CheckReturnMsg(command, resp)) return false;

                    Console.WriteLine("[XXX] OriginHomeAsync Success");
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine("[XXX] OriginHomeAsync Failed:" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 回到设定的零点
        /// 官方文档建议直接使用move
        /// 即（0,0,0）位置
        /// !（home） 返回的位置与控制器的home不是同一个位置
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<bool> HomeAsync(uint[] value)
        {
            try
            {
                var axis = string.Join(" ", value.Where(v => map.ContainsKey(v)).Select(v => map[v]));
                string command = $"! {axis}";

                var (ok, resp) = await SendCommandAsync(command);
                if (ok)
                {
                    if (!CheckReturnMsg(command, resp)) return false;

                    Console.WriteLine(resp);

                    Console.WriteLine("[XXX] HomeAsync Success");
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine("[XXX] HomeAsync Failed:" + e.Message);
                return false;
            }
        }

        public void Discard()
        {
            _serialPort!.DiscardInBuffer();
        }
    }

    public partial class MS2000
    {
        //发送指令以\r为结束符
        //返回指令以\r\n为结束符

        private TaskCompletionSource<string>? _commandTcs;
        private string _receiveBuffer = string.Empty;

        private readonly ManualResetEventSlim _waitHandle = new(false);
        private string _lastResponse = string.Empty;

        public async Task<(bool, string)> SendCommandAsync(string command, int timeoutMs = 2000)
        {
            if (!_serialPort!.IsOpen)
                throw new InvalidOperationException("串口未打开");

            // 准备 TaskCompletionSource 等待返回
            _commandTcs = new TaskCompletionSource<string>();

            // 清空缓冲
            _receiveBuffer = string.Empty;

            // 发送命令
            Console.WriteLine($"[SEND] {command}");
            _serialPort.Write(command + "\r");

            // 等待返回或超时
            var completedTask = await Task.WhenAny(_commandTcs.Task, Task.Delay(timeoutMs));
            if (completedTask == _commandTcs.Task)
            {
                string response = _commandTcs.Task.Result;
                return (true, response);
            }
            else
            {
                Console.WriteLine($"[TIMEOUT] {command} 超时未收到返回");
                return (false, string.Empty);
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = _serialPort!.ReadExisting();
                _receiveBuffer += data;

                //while (true)
                //{
                //    // 找到所有帧头 :A 的最后一个位置
                //    int lastStartIndex = _receiveBuffer.LastIndexOf(":A", StringComparison.Ordinal);
                //    if (lastStartIndex < 0)
                //        break; // 没有帧头，退出

                //    // 找最后一个帧头后面是否有完整帧尾 \r\n
                //    int endIndex = _receiveBuffer.IndexOf("\r\n", lastStartIndex, StringComparison.Ordinal);
                //    if (endIndex < 0)
                //        break; // 没有完整帧尾，等待下一批数据

                //    // 只截取最后一个 :A 到帧尾的数据
                //    string frame = _receiveBuffer.Substring(lastStartIndex, endIndex - lastStartIndex);

                //    // 清空缓冲区，丢弃之前所有数据
                //    _receiveBuffer = _receiveBuffer.Substring(endIndex + 2);

                //    // 触发结果
                //    _lastResponse = frame;
                //    _commandTcs?.TrySetResult(frame);
                //    _waitHandle.Set();

                //    // 退出循环，不处理其他帧
                //    break;
                //}

                while (true)
                {
                    // 找第一个帧头
                    int startIndex = _receiveBuffer.IndexOf(":A", StringComparison.Ordinal);
                    if (startIndex < 0)
                        break; // 没有帧头，退出

                    // 找帧尾
                    int endIndex = _receiveBuffer.IndexOf("\r\n", startIndex, StringComparison.Ordinal);
                    if (endIndex < 0)
                        break; // 没有完整帧尾，等待下一批数据

                    // 截取完整帧
                    string frame = _receiveBuffer.Substring(startIndex, endIndex - startIndex);

                    // 清理已处理的数据
                    _receiveBuffer = _receiveBuffer.Substring(endIndex + 2);

                    // 触发结果
                    _lastResponse = frame;
                    _commandTcs?.TrySetResult(frame);
                    _waitHandle.Set();

                }

                // 防止缓存无限增长
                if (_receiveBuffer.Length > 4096)
                    _receiveBuffer = string.Empty;

            }
            catch (Exception ex)
            {
                _commandTcs?.TrySetException(ex);
            }
        }

        /// <summary>
        /// 同步发送命令并等待返回
        /// </summary>
        /// <param name="command">要发送的命令</param>
        /// <param name="respond">收到的返回</param>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <returns>是否成功收到响应</returns>
        public bool SendCommand(string command, out string respond, int timeoutMs = 2000)
        {
            respond = string.Empty;

            if (_serialPort == null || !_serialPort.IsOpen)
                throw new InvalidOperationException("串口未打开");

            try
            {
                // 清理上一次的状态
                _lastResponse = string.Empty;
                _waitHandle.Reset();
                _receiveBuffer = string.Empty;

                // 发送命令
                Console.WriteLine($"[SEND] {command}");
                _serialPort.Write(command + "\r");

                // 等待返回
                if (_waitHandle.Wait(timeoutMs))
                {
                    respond = _lastResponse;
                    return !string.IsNullOrEmpty(respond); // 有返回内容才算成功
                }
                else
                {
                    Console.WriteLine($"[TIMEOUT] {command} 超时未收到返回");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
                throw;
            }
        }

        private static bool CheckReturnMsg(string command, string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                Console.WriteLine($"[ERR] 命令 {command} 返回为空");
                return false;
            }

            response = response.Replace("\r\n", "").Trim('\r', '\n', ' ');

            if (response.Contains(":A"))
            {
                if (response.Contains(":N"))
                {
                    var match = Regex.Match(response, @"\d+$");
                    if (match.Success)
                    {
                        int code = int.Parse(match.Value);
                        if (Enum.IsDefined(typeof(ErrorCodeEnum), code))
                        {
                            ErrorCodeEnum error = (ErrorCodeEnum)code;
                            Console.WriteLine($"错误码: {code}, 枚举值: {error}");
                        }
                    }

                    return false;
                }
                else
                {
                    if (!command.Contains("W") && !command.Contains("-"))

                        Console.WriteLine($"[OK] 命令 {command} 校验通过，返回: {response}");
                    return true;
                }
            }
            else
            {
                Console.WriteLine($"[ERR] 命令 {command} 返回 {response} 与期望不一致");
                return false;
            }
        }

        public enum ErrorCodeEnum
        {
            UnknownCommand = 1,
            UnrecognizedAxisParameter = 2,
            MissingParameters = 3,
            ParameterOutofRange = 4,
            OperationFailed = 5,
            UndefinedError = 6,
            InvalidCardAddress = 7,
            SerialCommandHalted = 21,
        }
    }

}
