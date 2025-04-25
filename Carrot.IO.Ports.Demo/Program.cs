using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Carrot.IO.Ports.Demo
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            SerialPort port = new SerialPort("COM250", 115200)
            {
                ReadBufferSize = 4096,
                WriteBufferSize = 4096,
                TimeoutModel = TimeoutModel.WaitAny,
            };
            try
            {
                port.Open();

                // 发送数据
                Console.WriteLine($"[{DateTime.Now}]: 发送测试");
                byte[] sendData = Encoding.ASCII.GetBytes("AT+TEST\r\n");
                await port.WriteAsync(sendData, 0, sendData.Length, CancellationToken.None);

                // 接收数据
                Console.WriteLine($"[{DateTime.Now}]: 接收测试,按任意键开始");
                //Console.ReadKey();
                Console.WriteLine($"[{DateTime.Now}]: 接收测试,运行中");
                byte[] buffer = new byte[1024];
                int bytesRead = await port.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    Console.WriteLine($"[{DateTime.Now}]: 收到 {bytesRead} 字节数据:{Encoding.ASCII.GetString(buffer, 0, bytesRead)}");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now}]: 无数据");
                }

                await port.WriteAsync(sendData, 0, sendData.Length, CancellationToken.None);

                // 手动取消示例
                Console.WriteLine($"[{DateTime.Now}]: 取消测试,按任意键开始");
                //Console.ReadKey();
                Console.WriteLine($"[{DateTime.Now}]: 取消测试,运行中");
                var cts = new CancellationTokenSource(5000);
                try
                {
                    int bytesRead2 = await port.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                    Console.WriteLine($"[{DateTime.Now}]: 收到 {bytesRead2} 字节数据:{Encoding.ASCII.GetString(buffer, 0, bytesRead)}");
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"[{DateTime.Now}]: 读取已取消");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                port.Close();
                port.Dispose();
            }
        }
    }
}
