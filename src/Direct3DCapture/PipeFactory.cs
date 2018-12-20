using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Text;
using CoreHook.IPC.Platform;

namespace Direct3DCapture
{
    public class PipeFactory : IPipePlatform
    {
        public NamedPipeServerStream CreatePipeByName(string pipeName, string serverName)
        {
            return new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                65536,
                65536
            );
        }
    }
}
