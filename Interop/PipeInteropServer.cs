using System;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using SPCode.UI;
using SPCode.Utils;

namespace SPCode.Interop
{
    public class PipeInteropServer : IDisposable
    {
        private NamedPipeServerStream _pipeServer;
        private readonly MainWindow _window;

        public PipeInteropServer(MainWindow window)
        {
            _window = window;
        }

        public void Start()
        {
            StartInteropServer();
        }

        public void Close()
        {
            _pipeServer.Close();
        }

        public void Dispose()
        {
            _pipeServer.Close();
        }

        private void StartInteropServer()
        {
            _pipeServer?.Close();

            _pipeServer = new NamedPipeServerStream(NamesHelper.PipeServerName, PipeDirection.In, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            _pipeServer.BeginWaitForConnection(PipeConnection_MessageIn, null);
        }

        private void PipeConnection_MessageIn(IAsyncResult iar)
        {
            _pipeServer.EndWaitForConnection(iar);

            // Read data length from pipe
            Span<byte> lengthBytes = stackalloc byte[4];
            _pipeServer.Read(lengthBytes);
            var length = MemoryMarshal.Read<int>(lengthBytes);
            Console.WriteLine(length);

            // Read data from pipe
            Span<byte> dataBytes = stackalloc byte[length];
            _pipeServer.Read(dataBytes);
            var data = Encoding.UTF8.GetString(dataBytes);

            var files = data.Split('|');
            _window.Dispatcher.Invoke(() =>
            {
                var selectIt = true;
                foreach (var filePath in files)
                {
                    if (!_window.IsLoaded) continue;

                    if (_window.TryLoadSourceFile(filePath, out _, SelectMe: selectIt) &&
                        _window.WindowState == System.Windows.WindowState.Minimized)
                    {
                        _window.WindowState = System.Windows.WindowState.Normal;
                        selectIt = false;
                    }
                }
            });

            StartInteropServer();
        }
    }
}