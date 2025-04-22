using System;
using System.Text;

namespace Carrot.IO.Ports.Demo
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            using (var port = new SerialPort("COM250", 2000000))
            {
                // 发送数据
                Console.WriteLine("发送测试");
                byte[] sendData = Encoding.ASCII.GetBytes("AT+TEST\r\n");
                await port.WriteAsync(sendData/*, 0*/, sendData.Length, CancellationToken.None);

                // 接收数据
                Console.WriteLine("接收测试,按任意键开始");
                Console.ReadKey();
                byte[] buffer = new byte[1024];
                try
                {
                    int bytesRead = await port.ReadAsync(buffer, buffer.Length);
                    if (bytesRead > 0)
                    {
                        Console.WriteLine($"收到 {bytesRead} 字节数据:{Encoding.ASCII.GetString(buffer, 0, bytesRead)}");
                    }
                    else
                    {
                        Console.WriteLine("无数据");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }

                // 手动取消示例
                Console.WriteLine("取消测试,按任意键开始");
                Console.ReadKey();
                var cts = new CancellationTokenSource(5000);
                try
                {
                    int bytesRead = await port.ReadAsync(buffer/*, 0*/, buffer.Length, cts.Token);
                    Console.WriteLine($"收到 {bytesRead} 字节数据:{Encoding.ASCII.GetString(buffer, 0, bytesRead)}");
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("读取已取消");
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"IO错误: {ex.Message} (0x{ex.HResult:x8})");
                }
            }
        }
    }
}
