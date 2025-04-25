using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Carrot.IO.Ports
{
    public static class Win32Serial
    {
        // 打开串口
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern SafeFileHandle CreateFile(
            string lpFileName,
            FileAccess dwDesiredAccess,
            FileShare dwShareMode,
            IntPtr lpSecurityAttributes,
            FileMode dwCreationDisposition,
            int dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        // 读取
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadFile(
            SafeFileHandle hFile,
            IntPtr lpBuffer,
            int nNumberOfBytesToRead,
            out int lpNumberOfBytesRead,
            ref OVERLAPPED lpOverlapped);

        // 写入
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteFile(
            SafeFileHandle hFile,
            IntPtr lpBuffer,
            int nNumberOfBytesToWrite,
            out int lpNumberOfBytesWritten,
            ref OVERLAPPED lpOverlapped);

        // 取消 I/O 操作
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CancelIoEx(SafeFileHandle hFile, ref OVERLAPPED lpOverlapped);

        // 读取串口参数
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetCommState(SafeFileHandle hFile, ref DCB lpDCB);

        // 配置串口参数
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetCommState(SafeFileHandle hFile, ref DCB lpDCB);

        // 配置超时
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetCommTimeouts(SafeFileHandle hFile, ref COMMTIMEOUTS lpCommTimeouts);

        // 配置缓冲区
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetupComm(
            SafeFileHandle hFile,
            uint dwInQueue,
            uint dwOutQueue
            );

        // 串口配置DCB结构体
        // https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-dcb
        [StructLayout(LayoutKind.Sequential)]
        public struct DCB
        {
            public int DCBlength;
            public uint BaudRate;
            public uint Flags;
            public ushort wReserved;
            public ushort XonLim;
            public ushort XoffLim;
            public byte ByteSize;
            public byte Parity;
            public byte StopBits;
            // 其他字段省略，需根据需求补全
        }

        // 串口超时配置结构体
        // https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-commtimeouts
        [StructLayout(LayoutKind.Sequential)]
        public struct COMMTIMEOUTS
        {
            public int ReadIntervalTimeout;
            public int ReadTotalTimeoutMultiplier;
            public int ReadTotalTimeoutConstant;
            public int WriteTotalTimeoutMultiplier;
            public int WriteTotalTimeoutConstant;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct OVERLAPPED
        {
            public IntPtr Internal;
            public IntPtr InternalHigh;
            public int Offset;
            public int OffsetHigh;
            public IntPtr hEvent;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetOverlappedResult(SafeFileHandle hFile, ref OVERLAPPED lpOverlapped, out int lpNumberOfBytesTransferred, bool bWait);


        // 常量
        public const int FILE_FLAG_OVERLAPPED = 0x40000000;
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const int ERROR_INVALID_PARAMETER = 87;
        public const int ERROR_IO_PENDING = 997;
        public const int ERROR_OPERATION_ABORTED = 995;

        // 翻译错误码
        public static string GetWin32ErrorMessage(int errorCode)
        {
            return new Win32Exception(errorCode).Message;
        }

        // 校验位
        public enum Parity
        {
            None = 0,
            Odd = 1,
            Even = 2,
            Mark = 3,
            Space = 4
        }

        // 停止位
        public enum StopBits
        {
            One = 0,
            OnePointFive = 1,
            Two = 2
        }
    }
}
