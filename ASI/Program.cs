namespace ASI
{
    internal class Program
    {
        static void Main(string[] args)
        {
            MS2000 mS2000 = new();
            if (mS2000.OpenCom("COM10"))
            {
                Console.WriteLine(mS2000.Half()); 

                Console.ReadLine();

                //while (true)
                //{
                //    var res = mS2000.GetPosition(new uint[] { 1, 2, 3 }, out var positions); ;
                //    if (!res || positions.Count != 3)
                //    {
                //        Console.WriteLine("##########################################获取位置失败");
                //        Console.ReadLine();
                //    }
                //    else
                //    {
                //        Console.WriteLine(string.Join(" ", positions));
                //    }

                //    var res0 = mS2000.GetAxisState(new uint[] { 1, 2, 3 }, out var states);
                //    if (!res0 || states.Count != 3)
                //    {
                //        Console.WriteLine("******************************************获取限位状态失败");
                //        Console.ReadLine();
                //    }
                //    else
                //    {
                //        Console.WriteLine(string.Join(" ", states));
                //    }

                //    Thread.Sleep(200);
                //}

                //mS2000.Discard();
                //var pos = new Dictionary<uint, double>() { { 1, 1000 }, { 2, 2000 }, { 3, 50 } };
                //if (!await mS2000.AbsoluteMoveAsync(pos))
                //{
                //    Console.WriteLine("[XXX] AbsoluteMoveAsync Failed: 移动命令执行失败");

                //}

                ////W X Y
                ////S X? Y? Z?
                ////! X Y Z//HOME
                ////H X Y Z
                ////RS X Y Z
                ////M X=100
                //// R X = 100 Y = 1000
                ////STATUS

                ////SI X Y Z 回到原始原点
                ////SL 软限位 SL X=-50 Y=-50 Z?  :A Z = -110.000

                //var (RES, STR) = await mS2000.SendCommandAsync("S X? Y? Z?");//RS X Y Z
                //if (RES) Console.WriteLine(STR);

                //var (RES2, STR2) = await mS2000.SendCommandAsync("STATUS");//RS X Y Z
                //if (RES2) Console.WriteLine(STR2);

                //Console.WriteLine(await mS2000.GetAxisState(123));
                //Console.WriteLine(await mS2000.GetSpeed(123));

                ////Console.WriteLine(await mS2000.GetStatus());//暂不可用

                ////Console.WriteLine(await mS2000.Home(123));
                ////Console.WriteLine(await mS2000.SetHome(123));
                ////Console.WriteLine(await mS2000.Half());

                ////var axisSpeeds = new Dictionary<int, int> { { 1, 5 }, { 2, 5 }, { 3, 5 } };
                ////Console.WriteLine(await mS2000.SetSpeed(axisSpeeds));

                //var axisPositions = new Dictionary<int, int> { { 1, 150 }, { 2, 100 }, { 3, 50 } };
                //Console.WriteLine(await mS2000.RelativeMove(axisPositions));

                //Console.WriteLine(await mS2000.AbsoluteMove(axisPositions));

                //Console.WriteLine(await mS2000.GetPosition(123));
                //Console.WriteLine(await mS2000.GetAxisState(123));
                //Console.WriteLine(await mS2000.GetSpeed(123));

            }

            //MS2000ASIMotor mS2000ASIMotor = new MS2000ASIMotor();
            //if (mS2000ASIMotor.InitMotor("com10"))
            //{
            //    //mS2000ASIMotor.GetPostion();
            //    //mS2000ASIMotor.GetLimitState();

            //    mS2000ASIMotor.XSpeed = 5;
            //    mS2000ASIMotor.YSpeed = 5;
            //    mS2000ASIMotor.ZSpeed = 5;
            //    Console.WriteLine(mS2000ASIMotor.XSpeed);
            //    Console.WriteLine(mS2000ASIMotor.YSpeed);
            //    Console.WriteLine(mS2000ASIMotor.ZSpeed);
            //}

            Console.ReadLine();
        }
    }
}
