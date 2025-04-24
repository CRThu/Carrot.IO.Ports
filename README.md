# Carrot.IO.Ports 

Asynchronous serial communication library for Windows using Overlapped I/O, supporting cancellation and async read/write operations. 

## Compatibility 

| Device Model          | Status       | Behavior Description                          |
|-----------------------|--------------|-----------------------------------------------|
| Virtual COM Port (VSPD) | ✔️ Full      | Matches expected behavior                    |
| CH340 Series          | ✔️ Full      | Matches expected behavior                      |
| CH343 Series          | ⚠️ Partial   | Immediate response on data arrival             |
| FTDI FT2232H           | ✔️ Full      | Matches expected behavior                      |

## 📚 API Reference

### Constructor
```csharp
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
``` 

### Open 
```csharp
public void Open()
``` 

### Close 
```csharp
public void Close()
``` 

### ReadAsync 
```csharp
Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
``` 
### WriteAsync 
```csharp
Task<int> WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
``` 

## ⏰ Timeout Behavior 


| TimeoutModel | Start read on empty buffer | Partial data arrives (less than requested bytes) | All data arrives during read |
|-|-|-|-|
| TimeoutModel.Immediately | Return 0 bytes                         |Return immediately                     | Return immediately    |
| TimeoutModel.WaitAny     | Wait until data arrives (cancellable)  | Return immediately                    | Return immediately    |
| TimeoutModel.WaitAll     | Wait until data arrives (cancellable)  | Wait until data arrives (cancellable) | Return immediately    |
