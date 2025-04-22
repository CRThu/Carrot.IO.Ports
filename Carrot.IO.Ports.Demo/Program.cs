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
                byte[] sendData = Encoding.ASCII.GetBytes("AT+TEST\r\n");
                await port.WriteAsync(sendData, 0, sendData.Length, CancellationToken.None);

                // 手动取消示例
                var cts = new CancellationTokenSource(5000);
                try
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead = await port.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                    Console.WriteLine($"读取成功: {bytesRead} 字节");
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
