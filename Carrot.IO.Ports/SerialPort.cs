using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static Carrot.IO.Ports.Win32Serial;

namespace Carrot.IO.Ports
{
    public enum TimeoutModel
    {
        /// <summary>
        /// Return immediately with or without any data
        /// </summary>
        Immediately,
        /// <summary>
        /// Return until any data received or cancelled
        /// </summary>
        WaitAny,
        /// <summary>
        /// Return until all data received or timeout/cancelled
        /// </summary>
        WaitAll
    }

    public class SerialPort : IDisposable
    {
        private string _portName;
        private int _baudRate;
        private int _dataBits;
        private Parity _parity;
        private StopBits _stopBits;
        private int _readBufferSize;
        private int _writeBufferSize;
        private int _timeout;
        private TimeoutModel _timeoutModel;

        private SafeFileHandle? _handle;
        private CancellationTokenSource _cts = new();
        private readonly ConcurrentDictionary<IntPtr, OVERLAPPED> _pendingOperations = new();
        private readonly object _ioLock = new();

        public string PortName
        {
            get { return _portName; }
            set
            {
                ValidatePortClosed();
                _portName = value;
            }
        }
        public int BaudRate
        {
            get { return _baudRate; }
            set
            {
                ValidatePortClosed();
                _baudRate = value;
            }
        }

        public int DataBits
        {
            get { return _dataBits; }
            set
            {
                ValidatePortClosed();
                _dataBits = value;
            }
        }

        public Parity Parity
        {
            get { return _parity; }
            set
            {
                ValidatePortClosed();
                _parity = value;
            }
        }

        public StopBits StopBits
        {
            get { return _stopBits; }
            set
            {
                ValidatePortClosed();
                _stopBits = value;
            }
        }

        public int ReadBufferSize
        {
            get { return _readBufferSize; }
            set
            {
                ValidatePortClosed();
                _readBufferSize = value;
            }
        }

        public int WriteBufferSize
        {
            get { return _writeBufferSize; }
            set
            {
                ValidatePortClosed();
                _writeBufferSize = value;
            }
        }

        public int Timeout
        {
            get { return _timeout; }
            set
            {
                ValidatePortClosed();
                _timeout = value;
            }
        }

        public TimeoutModel TimeoutModel
        {
            get { return _timeoutModel; }
            set
            {
                ValidatePortClosed();
                _timeoutModel = value;
            }
        }

        public bool IsOpen => _handle != null && !_handle.IsClosed;

        private void ValidatePortClosed()
        {
            if (IsOpen)
                throw new InvalidOperationException("Cannot change port property when port is open.");
        }

        public SerialPort(
            string portName,
            int baudRate = 115200,
            int dataBits = 8,
            Parity parity = Parity.None,
            StopBits stopBits = StopBits.One,
            int readBufferSize = 8192,
            int writeBufferSize = 8192,
            TimeoutModel timeoutModel = TimeoutModel.WaitAny,
            int timeout = 2000)
        {
            PortName = portName;
            BaudRate = baudRate;
            DataBits = dataBits;
            Parity = parity;
            StopBits = stopBits;
            ReadBufferSize = readBufferSize;
            WriteBufferSize = writeBufferSize;
            TimeoutModel = timeoutModel;
            Timeout = timeout;
        }


        public void Open()
        {
            if (IsOpen)
                return;

            try
            {
                // open serial port
                _handle = CreateFile(
                    @"\\.\" + PortName,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    IntPtr.Zero,
                    FileMode.Open,
                    FILE_FLAG_OVERLAPPED,
                    IntPtr.Zero);

                if (_handle.IsInvalid)
                    throw new IOException(GetWin32ErrorMessage(Marshal.GetLastWin32Error()), Marshal.GetLastWin32Error());

                if (!SetupComm(_handle, (uint)ReadBufferSize, (uint)WriteBufferSize))
                    throw new IOException(GetWin32ErrorMessage(Marshal.GetLastWin32Error()), Marshal.GetLastWin32Error());

                // 配置 DCB
                DCB dcb = new();
                if (!GetCommState(_handle, ref dcb))
                    throw new IOException(GetWin32ErrorMessage(Marshal.GetLastWin32Error()), Marshal.GetLastWin32Error());


                dcb.BaudRate = (uint)BaudRate;
                dcb.ByteSize = (byte)DataBits;
                dcb.Parity = (byte)Parity;
                dcb.StopBits = (byte)StopBits;

                if (Parity != Parity.None)
                    dcb.Flags |= 0x0020U;    // fParity=1
                else
                    dcb.Flags &= ~0x0020U;    // fParity=0

                if (!SetCommState(_handle, ref dcb))
                    throw new IOException(GetWin32ErrorMessage(Marshal.GetLastWin32Error()), Marshal.GetLastWin32Error());

                // 配置超时
                // 超时配置      |   数据满     |   数据未满    |   无数据     |
                // ---------------------------------------------------------
                // -1 0 0 0 0   | 立刻返回      |  立刻返回     | 立刻返回    |
                // -1 -1 -2 0 0 | 立刻返回      |  立刻返回     | 等待        |
                // 0 0 2000 0 0 | 立刻返回      |  等待2000ms   | 等待2000ms |
                COMMTIMEOUTS timeouts;
                if (TimeoutModel == TimeoutModel.Immediately)
                {
                    timeouts = new COMMTIMEOUTS
                    {
                        ReadIntervalTimeout = -1,           // 字符间最大延迟, 此参数-1且TotalTimeout均为0为立即返回
                        ReadTotalTimeoutMultiplier = 0,     // 总延迟，字节数*延迟倍数, 0表示不使用超时
                        ReadTotalTimeoutConstant = 0,       // 总延迟，延迟常数, 0表示不使用超时
                        WriteTotalTimeoutMultiplier = 0,
                        WriteTotalTimeoutConstant = 0
                    };
                }
                else if (TimeoutModel == TimeoutModel.WaitAny)
                {
                    timeouts = new COMMTIMEOUTS
                    {
                        ReadIntervalTimeout = -1,           // 字符间最大延迟, 此参数-1且TotalTimeout均为0为立即返回
                        ReadTotalTimeoutMultiplier = -1,    // 总延迟，字节数*延迟倍数, 0表示不使用超时
                        ReadTotalTimeoutConstant = -2,      // 总延迟，延迟常数, 0表示不使用超时
                        WriteTotalTimeoutMultiplier = 0,
                        WriteTotalTimeoutConstant = 0
                    };
                }
                else if (TimeoutModel == TimeoutModel.WaitAll)
                {
                    timeouts = new COMMTIMEOUTS
                    {
                        ReadIntervalTimeout = 0,           // 字符间最大延迟, 此参数-1且TotalTimeout均为0为立即返回
                        ReadTotalTimeoutMultiplier = 0,     // 总延迟，字节数*延迟倍数, 0表示不使用超时
                        ReadTotalTimeoutConstant = Timeout, // 总延迟，延迟常数, 0表示不使用超时
                        WriteTotalTimeoutMultiplier = 0,
                        WriteTotalTimeoutConstant = 0
                    };
                }
                else
                    throw new NotImplementedException($"TimeoutModel: {TimeoutModel} is not implemented.");


                if (!SetCommTimeouts(_handle, ref timeouts))
                    throw new IOException(GetWin32ErrorMessage(Marshal.GetLastWin32Error()), Marshal.GetLastWin32Error());
            }
            catch
            {
                _handle?.Dispose();
                _handle = null;
                throw;
            }
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            _cts.Cancel();
            foreach (var key in _pendingOperations.Keys.ToArray())
            {
                if (_pendingOperations.TryGetValue(key, out OVERLAPPED overlapped))
                    CancelIoEx(_handle, ref overlapped);
            }
            _handle?.Close();
        }

        // 可取消异步读取
        public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            if (!IsOpen)
                throw new InvalidOperationException("port is closed.");
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Invalid offset/count combination");

            GCHandle pinnedBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            IntPtr bufferPtr = IntPtr.Add(pinnedBuffer.AddrOfPinnedObject(), offset);

            var overlapped = new OVERLAPPED();
            var waitHandle = new ManualResetEvent(false);
            overlapped.hEvent = waitHandle.SafeWaitHandle.DangerousGetHandle();
            IntPtr key = overlapped.hEvent;

            try
            {
                using (cancellationToken.Register(() => CancelOperation(key)))
                {
                    int bytesRead;
                    bool immediateSuccess;

                    lock (_ioLock)
                    {
                        if (_handle.IsClosed)
                            throw new ObjectDisposedException(nameof(SerialPort), "串口已被关闭");
                        if (cancellationToken.IsCancellationRequested)
                            return 0;

                        _pendingOperations.TryAdd(key, overlapped);
                        immediateSuccess = ReadFile(
                            _handle,
                            bufferPtr,
                            count,
                            out bytesRead,
                            ref overlapped);
                    }

                    if (!immediateSuccess)
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error != ERROR_IO_PENDING)
                        {
                            // 立即失败的情况
                            if (error == ERROR_OPERATION_ABORTED)
                                return 0;
                            throw new IOException(GetWin32ErrorMessage(error), error);
                        }

                        // 使用带取消的异步等待
                        await WaitHandleAsync(waitHandle, cancellationToken);
                        if (cancellationToken.IsCancellationRequested)
                            return 0;

                        if (!GetOverlappedResult(_handle, ref overlapped, out bytesRead, true))
                        {
                            error = Marshal.GetLastWin32Error();
                            if (error == ERROR_OPERATION_ABORTED)
                                return 0;
                            throw new IOException(GetWin32ErrorMessage(error), error);
                        }
                    }
                    return bytesRead;
                }
            }
            finally
            {
                _pendingOperations.TryRemove(key, out _);
                waitHandle.Dispose();
                pinnedBuffer.Free();
            }
        }

        // 可取消异步写入
        public async Task<int> WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!IsOpen)
                throw new InvalidOperationException("port is closed.");
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Invalid offset/count combination");

            GCHandle pinnedBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            IntPtr bufferPtr = IntPtr.Add(pinnedBuffer.AddrOfPinnedObject(), offset);

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
                        if (_handle.IsClosed)
                            throw new ObjectDisposedException(nameof(SerialPort), "串口已被关闭");
                        if (cancellationToken.IsCancellationRequested)
                            return 0;

                        _pendingOperations.TryAdd(key, overlapped);
                        immediateSuccess = WriteFile(
                            _handle,
                            bufferPtr,
                            count,
                            out bytesWritten,
                            ref overlapped);
                    }

                    if (!immediateSuccess)
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error != ERROR_IO_PENDING)
                        {
                            // 立即失败的情况
                            if (error == ERROR_OPERATION_ABORTED)
                                return 0;
                            throw new IOException(GetWin32ErrorMessage(error), error);
                        }

                        // 使用带取消的异步等待
                        await WaitHandleAsync(waitHandle, cancellationToken);
                        if (cancellationToken.IsCancellationRequested)
                            return 0;

                        if (!GetOverlappedResult(_handle, ref overlapped, out bytesWritten, true))
                        {
                            error = Marshal.GetLastWin32Error();
                            if (error == ERROR_OPERATION_ABORTED)
                                return 0;
                            throw new IOException(GetWin32ErrorMessage(error), error);
                        }
                    }
                    return bytesWritten;
                }
            }
            finally
            {
                _pendingOperations.TryRemove(key, out _);
                waitHandle.Dispose();
                pinnedBuffer.Free();
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

            Close();

            _handle?.Dispose();
            _cts.Dispose();
        }
    }
}
