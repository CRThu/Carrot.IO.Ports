using System;
using System.Text;

namespace Carrot.IO.Ports.Demo
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            using (var port = new SerialPort("COM250", 115200))
            {
                // 发送数据
                Console.WriteLine($"[{DateTime.Now}]: 发送测试");
                byte[] sendData = Encoding.ASCII.GetBytes("AT+TEST\r\n");
                await port.WriteAsync(sendData, 0, sendData.Length, CancellationToken.None);

                // 接收数据
                Console.WriteLine($"[{DateTime.Now}]: 接收测试,按任意键开始");
                //Console.ReadKey();
                Console.WriteLine($"[{DateTime.Now}]: 接收测试,运行中");
                byte[] buffer = new byte[1024];
                try
                {
                    int bytesRead = await port.ReadAsync(buffer,0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        Console.WriteLine($"[{DateTime.Now}]: 收到 {bytesRead} 字节数据:{Encoding.ASCII.GetString(buffer, 0, bytesRead)}");
                    }
                    else
                    {
                        Console.WriteLine($"[{DateTime.Now}]: 无数据");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }

                await port.WriteAsync(sendData, 0, sendData.Length, CancellationToken.None);

                // 手动取消示例
                Console.WriteLine($"[{DateTime.Now}]: 取消测试,按任意键开始");
                //Console.ReadKey();
                Console.WriteLine($"[{DateTime.Now}]: 取消测试,运行中");
                var cts = new CancellationTokenSource(5000);
                try
                {
                    int bytesRead = await port.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                    Console.WriteLine($"[{DateTime.Now}]: 收到 {bytesRead} 字节数据:{Encoding.ASCII.GetString(buffer, 0, bytesRead)}");
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"[{DateTime.Now}]: 读取已取消");
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"[{DateTime.Now}]: IO错误: {ex.Message} (0x{ex.HResult:x8})");
                }
            }
        }
    }
}
