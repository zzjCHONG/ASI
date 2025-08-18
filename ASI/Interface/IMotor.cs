using Simscop.Pl.Core.Constants;

namespace Simscop.Pl.Core.Interfaces;

public interface IMotor
{
    /// <summary>
    /// 设备基本属性字典，比如 model，serialNumber ，FirmwareVersion等
    /// </summary>
    public Dictionary<InfoEnum, string> InfoDirectory { get; }

    public double XSpeed { get; set; }
    public double YSpeed { get; set; }
    public double ZSpeed { get; set; }

    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public bool XLimit { get; }
    public bool YLimit { get; }
    public bool ZLimit { get; }

    public bool XTaskRunning { get; }
    public bool YTaskRunning { get; }
    public bool ZTaskRunning { get; }

    public bool InitMotor(string com);
    public bool UnInitializeMotor();

    public bool SetXPosition(double xPosition);
    public bool SetYPosition(double yPosition);
    public bool SetZPosition(double zPosition);
    public bool SetXOffset(double x);
    public bool SetYOffset(double y);
    public bool SetZOffset(double z);

    public Task<bool> SetXPositionAsync(double xPosition);
    public Task<bool> SetYPositionAsync(double yPosition);
    public Task<bool> SetZPositionAsync(double zPosition);
    public Task<bool> SetXOffsetAsync(double x);
    public Task<bool> SetYOffsetAsync(double y);
    public Task<bool> SetZOffsetAsync(double z);

    public bool XResetPosition();
    public bool YResetPosition();
    public bool ZResetPosition();
    public Task<bool> XResetPositionAsync();
    public Task<bool> YResetPositionAsync();
    public Task<bool> ZResetPositionAsync();

    public bool SetOriginPos();
    public bool Stop();

    public Task<bool> MulAxisAbsoluteMoveAsync(Dictionary<uint, double> axisPositions);

    public Task<bool> OriginPosHomeAsync();

    public bool ResetParam();
}

