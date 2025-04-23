# Carrot.IO.Ports 

Asynchronous serial communication library for Windows using Overlapped I/O, supporting cancellation and async read/write operations. 

## Compatibility 

| Device Model          | Status       | Behavior Description                          |
|-----------------------|--------------|-----------------------------------------------|
| Virtual COM Port (VSPD) | ✔️ Full      | Matches expected behavior                     |
| CH340 Series          | ✔️ Full      | Immediate response on data arrival            |
| CH343 Series          | ⚠️ Partial   | Returns immediately regardless of data        |
| FTDI FT2232           | ✔️ Full      | Stable async waiting support                  |