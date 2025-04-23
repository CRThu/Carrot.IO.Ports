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
    int baudRate,
    int dataBits = 8,
    Parity parity = Parity.None,
    StopBits stopBits = StopBits.One,
    uint readBufferSize = 8192,
    uint writeBufferSize = 8192)
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
| Scenario                        | Behavior Description                     |
|----------------------------------|-----------------------------------------|
| Start read on empty buffer       | Wait indefinitely until data arrives (cancellable)    |
| All data arrives during read     | Return immediately with available data  |
| Partial data arrives (less than requested bytes) | Return immediately with available data |
