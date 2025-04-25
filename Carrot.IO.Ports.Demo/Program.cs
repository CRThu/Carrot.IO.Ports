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

                // 同步发送数据
                Console.WriteLine($"[{DateTime.Now}]: 同步发送测试");
                byte[] sendData = Encoding.ASCII.GetBytes("AT+TEST\r\n");
                int bytesWrite = port.Write(sendData, 0, sendData.Length);

                // 同步接收数据
                Console.WriteLine($"[{DateTime.Now}]: 异步接收测试,按任意键开始");
                Console.ReadKey();
                Console.WriteLine($"[{DateTime.Now}]: 异步接收测试,运行中");
                byte[] buffer = new byte[1024];
                int bytesRead = port.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    Console.WriteLine($"[{DateTime.Now}]: 收到 {bytesRead} 字节数据:{Encoding.ASCII.GetString(buffer, 0, bytesRead)}");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now}]: 无数据");
                }

                // 异步发送数据
                Console.WriteLine($"[{DateTime.Now}]: 异步发送测试");
                await port.WriteAsync(sendData, 0, sendData.Length, CancellationToken.None);

                // 异步接收数据
                Console.WriteLine($"[{DateTime.Now}]: 异步接收测试,按任意键开始");
                Console.ReadKey();
                Console.WriteLine($"[{DateTime.Now}]: 异步接收测试,运行中");
                bytesRead = await port.ReadAsync(buffer, 0, buffer.Length);
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
                Console.ReadKey();
                Console.WriteLine($"[{DateTime.Now}]: 取消测试,运行中");
                var cts = new CancellationTokenSource(5000);
                try
                {
                    bytesRead = await port.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                    Console.WriteLine($"[{DateTime.Now}]: 收到 {bytesRead} 字节数据:{Encoding.ASCII.GetString(buffer, 0, bytesRead)}");
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
