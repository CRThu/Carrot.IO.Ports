using Microsoft.Win32.SafeHandles;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using static Carrot.IO.Ports.Win32Serial;

namespace Carrot.IO.Ports
{
    public class SerialPort : IDisposable
    {
        private readonly SafeFileHandle _handle;
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentDictionary<IntPtr, OVERLAPPED> _pendingOperations = new();
        private readonly object _ioLock = new();

        public SerialPort(string portName, int baudRate, int dataBits = 8/*, Parity parity = Parity.None, StopBits stopBits = StopBits.One*/)
        {
            // 打开串口
            _handle = Win32Serial.CreateFile(
                @"\\.\" + portName, // 支持 COM 号大于 9
                FileAccess.ReadWrite,
                FileShare.None,
                IntPtr.Zero,
                FileMode.Open,
                Win32Serial.FILE_FLAG_OVERLAPPED,
                IntPtr.Zero);

            if (_handle.IsInvalid)
                throw new IOException("无法打开串口", Marshal.GetLastWin32Error());

            //// 配置 DCB
            DCB dcb = new();
            //if (!Win32Serial.GetCommState(_handle, ref dcb))
            //    throw new IOException("获取串口状态失败", Marshal.GetLastWin32Error());

            dcb.BaudRate = (uint)baudRate;
            dcb.ByteSize = (byte)dataBits;
            //dcb.Parity = (byte)parity;
            //dcb.StopBits = (byte)(stopBits == StopBits.One ? 0 : stopBits == StopBits.Two ? 2 : 1);

            if (!SetCommState(_handle, ref dcb))
                throw new IOException("配置串口失败", Marshal.GetLastWin32Error());

            // 配置超时（非阻塞模式）
            Win32Serial.COMMTIMEOUTS timeouts = new COMMTIMEOUTS
            {
                ReadIntervalTimeout = 0,
                ReadTotalTimeoutMultiplier = 0,
                ReadTotalTimeoutConstant = 0,
                WriteTotalTimeoutMultiplier = 0,
                WriteTotalTimeoutConstant = 0
            };
            if (!SetCommTimeouts(_handle, ref timeouts))
                throw new IOException("配置超时失败", Marshal.GetLastWin32Error());
        }

        // 实现真正的可取消异步读取
        public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var overlapped = new OVERLAPPED();
            var waitHandle = new ManualResetEvent(false);
            overlapped.hEvent = waitHandle.SafeWaitHandle.DangerousGetHandle();
            IntPtr key = overlapped.hEvent;

            try
            {
                // 注册取消回调
                using (cancellationToken.Register(() => CancelOperation(key)))
                {
                    int bytesRead;
                    bool immediateSuccess;

                    lock (_ioLock)
                    {
                        if (_handle.IsClosed || cancellationToken.IsCancellationRequested)
                            throw new OperationCanceledException();

                        _pendingOperations.TryAdd(key, overlapped);
                        immediateSuccess = ReadFile(_handle, buffer, count, out bytesRead, ref overlapped);
                    }

                    if (!immediateSuccess)
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error != ERROR_IO_PENDING)
                            throw new IOException("读取启动失败", error);

                        // 异步等待完成或取消
                        await WaitHandleAsync(waitHandle, cancellationToken);

                        if (cancellationToken.IsCancellationRequested)
                            throw new OperationCanceledException();

                        // 获取最终结果
                        if (!GetOverlappedResult(_handle, ref overlapped, out bytesRead, true))
                        {
                            error = Marshal.GetLastWin32Error();
                            if (error == ERROR_OPERATION_ABORTED)
                                throw new OperationCanceledException();
                            throw new IOException("异步读取失败", error);
                        }
                    }
                    return bytesRead;
                }
            }
            finally
            {
                _pendingOperations.TryRemove(key, out _);
                waitHandle.Dispose();
            }
        }

        public async Task<int> WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var overlapped = new OVERLAPPED();
            var waitHandle = new ManualResetEvent(false);
            overlapped.hEvent = waitHandle.SafeWaitHandle.DangerousGetHandle();
            IntPtr key = overlapped.hEvent;

            try
            {
                using (cancellationToken.Register(() => CancelOperation(key)))
                {
                    int bytesWritten;
                    bool immediateSuccess;

                    lock (_ioLock)
                    {
                        if (_handle.IsClosed || cancellationToken.IsCancellationRequested)
                            throw new OperationCanceledException();

                        _pendingOperations.TryAdd(key, overlapped);
                        immediateSuccess = WriteFile(
                            _handle,
                            buffer,
                            count,
                            out bytesWritten,
                            ref overlapped);
                    }

                    if (!immediateSuccess)
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error != ERROR_IO_PENDING)
                            throw new IOException("写入启动失败", error);

                        await WaitHandleAsync(waitHandle, cancellationToken);

                        if (cancellationToken.IsCancellationRequested)
                            throw new OperationCanceledException();

                        if (!GetOverlappedResult(_handle, ref overlapped, out bytesWritten, true))
                        {
                            error = Marshal.GetLastWin32Error();
                            if (error == ERROR_OPERATION_ABORTED)
                                throw new OperationCanceledException();
                            throw new IOException("异步写入失败", error);
                        }
                    }
                    return bytesWritten;
                }
            }
            finally
            {
                _pendingOperations.TryRemove(key, out _);
                waitHandle.Dispose();
            }
        }

        private async Task WaitHandleAsync(WaitHandle waitHandle, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (ct.Register(() => tcs.TrySetCanceled()))
            {
                RegisteredWaitHandle rwh = ThreadPool.RegisterWaitForSingleObject(
                    waitHandle,
                    (state, timedOut) => tcs.TrySetResult(!timedOut),
                    null,
                    -1,
                    true);

                try
                {
                    await tcs.Task;
                }
                finally
                {
                    rwh.Unregister(null);
                }
            }
        }

        private void CancelOperation(IntPtr operationKey)
        {
            if (_pendingOperations.TryGetValue(operationKey, out OVERLAPPED overlapped))
            {
                CancelIoEx(_handle, ref overlapped);
            }
        }

        private bool _disposed = false;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            _cts.Cancel();
            foreach (var key in _pendingOperations.Keys.ToArray())
            {
                if (_pendingOperations.TryGetValue(key, out OVERLAPPED overlapped))
                    CancelIoEx(_handle, ref overlapped);
            }
            _handle?.Close();
            _handle?.Dispose();
            _cts.Dispose();
        }
    }
}
