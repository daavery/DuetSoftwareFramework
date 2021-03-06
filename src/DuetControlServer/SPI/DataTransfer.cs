﻿using DuetAPI;
using DuetAPI.Machine;
using DuetAPI.Utility;
using DuetControlServer.SPI.Communication.FirmwareRequests;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Code = DuetControlServer.Commands.Code;
using Nito.AsyncEx;
using LinuxDevices;

namespace DuetControlServer.SPI
{
    /// <summary>
    /// Helper class for SPI data transfers
    /// </summary>
    public static class DataTransfer
    {
        // General transfer variables
        private static InputGpioPin _transferReadyPin;
        private static SpiDevice _spiDevice;
        private static readonly AsyncManualResetEvent _transferReadyEvent = new AsyncManualResetEvent();
        private static bool _waitingForFirstTransfer = true, _started, _hadTimeout, _resetting, _updating;
        private static ushort _lastTransferNumber;

        private static DateTime _lastMeasureTime = DateTime.Now;
        private static int _numMeasuredTransfers;

        // Transfer headers
        private static readonly Memory<byte> _rxHeaderBuffer = new byte[Marshal.SizeOf(typeof(Communication.TransferHeader))];
        private static readonly Memory<byte> _txHeaderBuffer = new byte[Marshal.SizeOf(typeof(Communication.TransferHeader))];
        private static Communication.TransferHeader _rxHeader;
        private static Communication.TransferHeader _txHeader;
        private static byte _packetId;

        // Transfer responses
        private static readonly Memory<byte> _rxResponseBuffer = new byte[4];
        private static readonly Memory<byte> _txResponseBuffer = new byte[4];

        // Transfer data. Keep two TX buffers so resend requests can be processed
        private static readonly Memory<byte> _rxBuffer = new byte[Communication.Consts.BufferSize];
        private static readonly Memory<byte>[] _txBuffers = { new byte[Communication.Consts.BufferSize], new byte[Communication.Consts.BufferSize] };
        private static int _txBufferIndex;
        private static int _rxPointer, _txPointer;
        private static Communication.PacketHeader _lastPacket;
        private static ReadOnlyMemory<byte> _packetData;

        /// <summary>
        /// Set up the SPI device and the controller for the transfer ready pin
        /// </summary>
        public static void Init()
        {
            // Initialize TX header. This only needs to happen once
            Serialization.Writer.InitTransferHeader(ref _txHeader);

            // Initialize transfer ready pin
            _transferReadyPin = new InputGpioPin(Settings.GpioChipDevice, Settings.TransferReadyPin);
            _transferReadyPin.PinChanged += (sender, pinValue) => _transferReadyEvent.Set();
            _transferReadyPin.StartMonitoring(Program.CancelSource.Token);

            // Initialize SPI device
            _spiDevice = new SpiDevice(Settings.SpiDevice, Settings.SpiFrequency);

            // Check if large transfers can be performed
            try
            {
                int maxSpiBufferSize = int.Parse(File.ReadAllText("/sys/module/spidev/parameters/bufsiz"));
                if (maxSpiBufferSize < Communication.Consts.BufferSize)
                {
                    Console.WriteLine($"[warn] Kernel SPI buffer size is smaller than RepRapFirmware buffer size ({maxSpiBufferSize} configured vs {Communication.Consts.BufferSize} required)");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("[warn] Failed to retrieve Kernel SPI buffer size:");
                Console.WriteLine(e);
            }
        }

        private static decimal GetFullTransfersPerSecond()
        {
            if (_numMeasuredTransfers == 0)
            {
                return 0;
            }

            decimal result = _numMeasuredTransfers / (decimal)(DateTime.Now - _lastMeasureTime).TotalSeconds;
            _lastMeasureTime = DateTime.Now;
            Interlocked.Exchange(ref _numMeasuredTransfers, 0);
            return result;
        }

        /// <summary>
        /// Print diagnostics to the given string builder
        /// </summary>
        /// <param name="builder">Target to write to</param>
        public static void Diagnostics(StringBuilder builder)
        {
            builder.AppendLine($"Configured SPI speed: {Settings.SpiFrequency} Hz");
            builder.AppendLine($"Full transfers per second: {GetFullTransfersPerSecond():F2}");
        }

        /// <summary>
        /// Perform a full data transfer synchronously
        /// </summary>
        /// <param name="mustSucceed">Keep retrying until the transfer succeeds</param>
        /// <returns>Whether new data could be transferred</returns>
        public static bool PerformFullTransfer(bool mustSucceed = true)
        {
            _lastTransferNumber = _rxHeader.SequenceNumber;

            // Reset RX transfer header
            _rxHeader.FormatCode = Communication.Consts.InvalidFormatCode;
            _rxHeader.NumPackets = 0;
            _rxHeader.ProtocolVersion = 0;
            _rxHeader.DataLength = 0;
            _rxHeader.ChecksumData = 0;
            _rxHeader.ChecksumHeader = 0;

            // Set up TX transfer header
            _txHeader.NumPackets = _packetId;
            _txHeader.SequenceNumber++;
            _txHeader.DataLength = (ushort)_txPointer;
            _txHeader.ChecksumData = CRC16.Calculate(_txBuffers[_txBufferIndex].Slice(0, _txPointer).Span);
            MemoryMarshal.Write(_txHeaderBuffer.Span, ref _txHeader);
            _txHeader.ChecksumHeader = CRC16.Calculate(_txHeaderBuffer.Slice(0, Marshal.SizeOf(_txHeader) - Marshal.SizeOf(typeof(ushort))).Span);
            MemoryMarshal.Write(_txHeaderBuffer.Span, ref _txHeader);

            do
            {
                try
                {
                    // Exchange transfer headers. This also deals with transfer responses
                    if (!ExchangeHeader())
                    {
                        continue;
                    }

                    // Exchange data if there is anything to transfer
                    if ((_rxHeader.DataLength != 0 || _txPointer != 0) && !ExchangeData())
                    {
                        continue;
                    }

                    // Deal with timeouts
                    if (_hadTimeout)
                    {
                        using (Model.Provider.AccessReadWrite())
                        {
                            if (Model.Provider.Get.State.Status == MachineStatus.Off)
                            {
                                Model.Provider.Get.State.Status = MachineStatus.Idle;
                            }
                            Model.Provider.Get.Messages.Add(new Message(MessageType.Success, "Connection to Duet established"));
                            Console.WriteLine("[info] Connection to Duet established");
                        }
                        _hadTimeout = _resetting = false;
                    }

                    // Deal with the first transmission
                    if (!_started)
                    {
                        _lastTransferNumber = (ushort)(_rxHeader.SequenceNumber - 1);
                        _started = true;
                    }
                    _updating = false;

                    // Transfer OK
                    Interlocked.Increment(ref _numMeasuredTransfers);
                    _txBufferIndex = (_txBufferIndex == 0) ? 1 : 0;
                    _rxPointer = _txPointer = 0;
                    _packetId = 0;

                    // Deal with reset requests
                    if (_resetting)
                    {
                        _waitingForFirstTransfer = _hadTimeout = true;
                        return PerformFullTransfer(mustSucceed);
                    }
                    return true;
                }
                catch (OperationCanceledException e)
                {
                    if (!Program.CancelSource.IsCancellationRequested && !_hadTimeout && _started && !_updating)
                    {
                        // If this is the first unexpected timeout event, report it unless the firmware is being updated
                        using (Model.Provider.AccessReadWrite())
                        {
                            if (Model.Provider.Get.State.Status == MachineStatus.Updating)
                            {
                                _waitingForFirstTransfer = _hadTimeout = true;
                                Model.Provider.Get.State.Status = MachineStatus.Off;
                                Model.Provider.Get.Messages.Add(new Message(MessageType.Warning, $"Lost connection to Duet ({e.Message})"));
                                Console.WriteLine($"[warn] Lost connection to Duet ({e.Message})");
                            }
                        }
                    }
                }
            }
            while (mustSucceed && !Program.CancelSource.IsCancellationRequested);

            return false;
        }

        /// <summary>
        /// Check if the controller has been reset
        /// </summary>
        /// <returns>Whether the controller has been reset</returns>
        public static bool HadReset()
        {
            return _started && ((ushort)(_lastTransferNumber + 1) != _rxHeader.SequenceNumber);
        }

        #region Read functions
        /// <summary>
        /// Returns the number of packets to read
        /// </summary>
        public static int PacketsToRead { get => _rxHeader.NumPackets; }

        /// <summary>
        /// Read the next packet
        /// </summary>
        /// <returns>The next packet or null if none is available</returns>
        public static Communication.PacketHeader? ReadPacket()
        {
            if (_rxPointer >= _rxHeader.DataLength)
            {
                return null;
            }

            // Header
            _lastPacket = Serialization.Reader.ReadPacketHeader(_rxBuffer.Slice(_rxPointer).Span);
            _rxPointer += Marshal.SizeOf(_lastPacket);

            // Packet data
            _packetData = _rxBuffer.Slice(_rxPointer, _lastPacket.Length);
            int padding = 4 - (_lastPacket.Length % 4);
            _rxPointer += _lastPacket.Length + ((padding == 4) ? 0 : padding);

            return _lastPacket;
        }

        /// <summary>
        /// Read the result of a <see cref="Communication.LinuxRequests.Request.GetObjectModel"/> request
        /// </summary>
        /// <param name="module">Module described by the returned JSON data</param>
        /// <param name="json">JSON data</param>
        public static void ReadObjectModel(out byte module, out byte[] json)
        {
            Serialization.Reader.ReadObjectModel(_packetData.Span, out module, out json);
        }

        /// <summary>
        /// Read a code buffer update
        /// </summary>
        /// <param name="bufferSpace">Buffer space</param>
        public static void ReadCodeBufferUpdate(out ushort bufferSpace)
        {
            Serialization.Reader.ReadCodeBufferUpdate(_packetData.Span, out bufferSpace);
        }

        /// <summary>
        /// Read the result of a <see cref="Communication.LinuxRequests.Request.Code"/> request
        /// </summary>
        /// <param name="messageType">Message type flags of the reply</param>
        /// <param name="reply">Code reply</param>
        public static void ReadCodeReply(out Communication.MessageTypeFlags messageType, out string reply)
        {
            Serialization.Reader.ReadCodeReply(_packetData.Span, out messageType, out reply);
        }

        /// <summary>
        /// Read the content of a <see cref="MacroRequest"/> packet
        /// </summary>
        /// <param name="channel">Channel requesting a macro file</param>
        /// <param name="reportMissing">Write an error message if the macro is not found</param>
        /// <param name="isSystemMacro">Indicates if this code is not bound to a code being executed (e.g. when a trigger macro is requested)</param>
        /// <param name="filename">Filename of the requested macro</param>
        public static void ReadMacroRequest(out CodeChannel channel, out bool reportMissing, out bool isSystemMacro, out string filename)
        {
            Serialization.Reader.ReadMacroRequest(_packetData.Span, out channel, out reportMissing, out isSystemMacro, out filename);
        }

        /// <summary>
        /// Read the content of an <see cref="AbortFileRequest"/> packet
        /// </summary>
        /// <param name="channel">Code channel where all files are supposed to be aborted</param>
        /// <param name="abortAll">Whether all files are supposed to be aborted</param>
        public static void ReadAbortFile(out CodeChannel channel, out bool abortAll)
        {
            Serialization.Reader.ReadAbortFile(_packetData.Span, out channel, out abortAll);
        }

        /// <summary>
        /// Read the content of a <see cref="StackEvent"/> packet
        /// </summary>
        /// <param name="channel">Code channel where the event occurred</param>
        /// <param name="stackDepth">New stack depth</param>
        /// <param name="flags">Bitmap holding info about the stack</param>
        /// <param name="feedrate">Sticky feedrate on this channel</param>
        public static void ReadStackEvent(out CodeChannel channel, out byte stackDepth, out StackFlags flags, out float feedrate)
        {
            Serialization.Reader.ReadStackEvent(_packetData.Span, out channel, out stackDepth, out flags, out feedrate);
        }

        /// <summary>
        /// Read the content of a <see cref="PrintPaused"/> packet
        /// </summary>
        /// <param name="filePosition">Position where the print has been paused</param>
        /// <param name="reason">Reason why the print has been paused</param>
        public static void ReadPrintPaused(out uint filePosition, out Communication.PrintPausedReason reason)
        {
            Serialization.Reader.ReadPrintPaused(_packetData.Span, out filePosition, out reason);
        }

        /// <summary>
        /// Read the result of a <see cref="Communication.LinuxRequests.Request.GetHeightMap"/> request
        /// </summary>
        /// <param name="map">Received heightmap</param>
        public static void ReadHeightMap(out Heightmap map)
        {
            Serialization.Reader.ReadHeightMap(_packetData.Span, out map);
        }

        /// <summary>
        /// Read the content of a <see cref="Request.Locked"/> packet
        /// </summary>
        /// <param name="channel">Code channel that has acquired the lock</param>
        /// <returns>Asynchronous task</returns>
        public static void ReadResourceLocked(out CodeChannel channel)
        {
            Serialization.Reader.ReadResourceLocked(_packetData.Span, out channel);
        }

        /// <summary>
        /// Read a chunk of a <see cref="Request.RequestFileChunk"/> packet
        /// </summary>
        /// <param name="filename">Filename</param>
        /// <param name="offset">File offset</param>
        /// <param name="maxLength">Maximum chunk size</param>
        public static void ReadFileChunkRequest(out string filename, out uint offset, out uint maxLength)
        {
            Serialization.Reader.ReadFileChunkRequest(_packetData.Span, out filename, out offset, out maxLength);
        }

        /// <summary>
        /// Write the last packet + content for diagnostic purposes
        /// </summary>
        public static void DumpMalformedPacket()
        {
            using (FileStream stream = new FileStream(Path.Combine(Directory.GetCurrentDirectory(), "transferDump.bin"), FileMode.Create, FileAccess.Write))
            {
                stream.Write(_rxBuffer.Slice(0, _rxHeader.DataLength).Span);
            }

            string dump = "[err] Received malformed packet:\n";
            dump += $"=== Packet #{_lastPacket.Id} from offset {_rxPointer} request {_lastPacket.Request} (length {_lastPacket.Length}) ===\n";
            foreach(byte c in _packetData.Span)
            {
                dump += ((int)c).ToString("x2");
            }
            dump += "\n";
            string str = Encoding.UTF8.GetString(_packetData.Span);
            foreach(char c in str)
            {
                dump += char.IsLetterOrDigit(c) ? c : '.';
            }
            dump += "\n";
            dump += "====================";
            Console.WriteLine(dump);
        }
        #endregion

        #region Write functions
        /// <summary>
        /// Resend a packet back to the firmware
        /// </summary>
        /// <param name="packet">Packet holding the resend request</param>
        public static void ResendPacket(Communication.PacketHeader packet)
        {
            Span<byte> buffer = ((_txBufferIndex == 0) ? _txBuffers[1] : _txBuffers[0]).Span;
            Communication.PacketHeader header;
            int headerSize = Marshal.SizeOf(typeof(Communication.PacketHeader));
            do
            {
                // Read next packet
                header = MemoryMarshal.Cast<byte, Communication.PacketHeader>(buffer)[0];
                if (header.Id == packet.ResendPacketId)
                {
                    Span<byte> destination = GetWriteBuffer(headerSize + header.Length);
                    buffer.Slice(0, headerSize + header.Length).CopyTo(destination);
                    return;
                }

                // Move on to the next one
                int padding = 4 - (header.Length % 4);
                buffer = buffer.Slice(headerSize + header.Length + ((padding == 4) ? 0 : padding));
            }
            while (header.Id < packet.ResendPacketId && buffer.Length > 0);

            throw new ArgumentException($"Firmware requested resend for invalid packet #{packet.ResendPacketId}");
        }

        /// <summary>
        /// Request an emergency stop
        /// </summary>
        /// <returns>True if the packet could be written</returns>
        public static bool WriteEmergencyStop()
        {
            if (!CanWritePacket())
            {
                return false;
            }

            WritePacket(Communication.LinuxRequests.Request.EmergencyStop);
            return true;
        }

        /// <summary>
        /// Request a firmware reset
        /// </summary>
        /// <returns>True if the packet could be written</returns>
        public static bool WriteReset()
        {
            if (!CanWritePacket())
            {
                return false;
            }

            _resetting = true;
            WritePacket(Communication.LinuxRequests.Request.Reset);
            return true;
        }

        /// <summary>
        /// Figure out the size of a binary G/M/T-code
        /// </summary>
        /// <param name="code">Code to write</param>
        /// <returns>Code size in bytes</returns>
        public static int GetCodeSize(Code code)
        {
            Span<byte> span = stackalloc byte[Communication.Consts.MaxCodeBufferSize];
            try
            {
                return Serialization.Writer.WriteCode(span, code);
            }
            catch (ArgumentException)
            {
                throw new ArgumentException("Code is too long");
            }
        }

        /// <summary>
        /// Request a code to be executed
        /// </summary>
        /// <param name="code">Code to send</param>
        /// <returns>True if the packet could be written</returns>
        public static bool WriteCode(Code code)
        {
            // Attempt to serialize the code first
            Span<byte> span = stackalloc byte[Communication.Consts.MaxCodeBufferSize];
            int codeLength;
            try
            {
                codeLength = Serialization.Writer.WriteCode(span, code);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new ArgumentException("Value is too big", nameof(code));
            }

            // See if the code fits into the buffer
            if (!CanWritePacket(codeLength))
            {
                return false;
            }

            // Write it
            WritePacket(Communication.LinuxRequests.Request.Code, codeLength);
            span.Slice(0, codeLength).CopyTo(GetWriteBuffer(codeLength));
            return true;
        }

        /// <summary>
        /// Request the object module of a specific module
        /// </summary>
        /// <param name="module">Module index to query</param>
        /// <returns>True if the packet could be written</returns>
        public static bool WriteGetObjectModel(byte module)
        {
            int dataLength = Marshal.SizeOf(typeof(Communication.SharedRequests.ObjectModel));
            if (!CanWritePacket(dataLength))
            {
                return false;
            }

            WritePacket(Communication.LinuxRequests.Request.GetObjectModel, dataLength);
            Serialization.Writer.WriteObjectModelRequest(GetWriteBuffer(dataLength), module);
            return true;
        }

        /// <summary>
        /// Set a specific value in the object model of RepRapFirmware
        /// </summary>
        /// <param name="field">Path to the field</param>
        /// <param name="value">New value</param>
        /// <returns>True if the packet could be written</returns>
        public static bool WriteSetObjectModel(string field, object value)
        {
            // Serialize the request first to see how much space it requires
            Span<byte> span = stackalloc byte[Communication.Consts.BufferSize - Marshal.SizeOf(typeof(Communication.PacketHeader))];
            int dataLength = Serialization.Writer.WriteObjectModel(span, field, value);

            // See if the request fits into the buffer
            if (!CanWritePacket(dataLength))
            {
                return false;
            }

            // Write it
            WritePacket(Communication.LinuxRequests.Request.SetObjectModel, dataLength);
            span.Slice(0, dataLength).CopyTo(GetWriteBuffer(dataLength));
            return true;
        }

        /// <summary>
        /// Notify the firmware that a file print has started
        /// </summary>
        /// <param name="info">Information about the file being printed</param>
        /// <returns>True if the packet could be written</returns>
        public static bool WritePrintStarted(ParsedFileInfo info)
        {
            // Serialize the request first to see how much space it requires
            Span<byte> span = stackalloc byte[Communication.Consts.BufferSize - Marshal.SizeOf(typeof(Communication.PacketHeader))];
            int dataLength = Serialization.Writer.WritePrintStarted(span, info);

            // See if the request fits into the buffer
            if (!CanWritePacket(dataLength))
            {
                return false;
            }

            // Write it
            WritePacket(Communication.LinuxRequests.Request.PrintStarted, dataLength);
            span.Slice(0, dataLength).CopyTo(GetWriteBuffer(dataLength));
            return true;
        }

        /// <summary>
        /// Notify that a file print has been stopped
        /// </summary>
        /// <param name="reason">Reason why the print has been stopped</param>
        /// <returns>True if the packet could be written</returns>
        public static bool WritePrintStopped(Communication.PrintStoppedReason reason)
        {
            int dataLength = Marshal.SizeOf(typeof(Communication.LinuxRequests.PrintStopped));
            if (!CanWritePacket(dataLength))
            {
                return false;
            }

            WritePacket(Communication.LinuxRequests.Request.PrintStopped, dataLength);
            Serialization.Writer.WritePrintStopped(GetWriteBuffer(dataLength), reason);
            return true;
        }

        /// <summary>
        /// Notify the firmware about a completed macro file.
        /// This function is only used for macro files that the firmware requested
        /// </summary>
        /// <param name="channel">Code channel of the finished macro</param>
        /// <param name="error">Whether an error occurred</param>
        /// <returns>True if the packet could be written</returns>
        public static bool WriteMacroCompleted(CodeChannel channel, bool error)
        {
            int dataLength = Marshal.SizeOf(typeof(Communication.LinuxRequests.MacroCompleted));
            if (!CanWritePacket(dataLength))
            {
                return false;
            }

            WritePacket(Communication.LinuxRequests.Request.MacroCompleted, dataLength);
            Serialization.Writer.WriteMacroCompleted(GetWriteBuffer(dataLength), channel, error);
            return true;
        }

        /// <summary>
        /// Request the heightmap from the firmware
        /// </summary>
        /// <returns>True if the packet could be written</returns>
        public static bool WriteGetHeightMap()
        {
            if (!CanWritePacket())
            {
                return false;
            }

            WritePacket(Communication.LinuxRequests.Request.GetHeightMap);
            return true;
        }
        
        /// <summary>
        /// Write a heightmap to the firmware
        /// </summary>
        /// <param name="map">Heightmap to send</param>
        /// <returns>True if the packet could be written</returns>
        public static bool WriteHeightMap(Heightmap map)
        {
            // Serialize the request first to see how much space it requires
            Span<byte> span = stackalloc byte[Communication.Consts.BufferSize - Marshal.SizeOf(typeof(Communication.PacketHeader))];
            int dataLength = Serialization.Writer.WriteHeightMap(span, map);

            // See if the request fits into the buffer
            if (!CanWritePacket(dataLength))
            {
                return false;
            }

            // Write it
            WritePacket(Communication.LinuxRequests.Request.SetHeightMap, dataLength);
            span.Slice(0, dataLength).CopyTo(GetWriteBuffer(dataLength));
            return true;
        }

        /// <summary>
        /// Request the movement to be locked and wait for standstill
        /// </summary>
        /// <param name="channel">Code channel that requires the lock</param>
        /// <returns>True if the packet could be written</returns>
        public static bool WriteLockMovementAndWaitForStandstill(CodeChannel channel)
        {
            int dataLength = Marshal.SizeOf(typeof(Communication.SharedRequests.LockUnlock));
            if (!CanWritePacket(dataLength))
            {
                return false;
            }

            WritePacket(Communication.LinuxRequests.Request.LockMovementAndWaitForStandstill);
            Serialization.Writer.WriteLockUnlock(GetWriteBuffer(dataLength), channel);
            return true;
        }

        /// <summary>
        /// Release all acquired locks again
        /// </summary>
        /// <param name="channel">Code channel that releases the locks</param>
        /// <returns>True if the packet could be written</returns>
        public static bool WriteUnlock(CodeChannel channel)
        {
            int dataLength = Marshal.SizeOf(typeof(Communication.SharedRequests.LockUnlock));
            if (!CanWritePacket(dataLength))
            {
                return false;
            }

            WritePacket(Communication.LinuxRequests.Request.Unlock);
            Serialization.Writer.WriteLockUnlock(GetWriteBuffer(dataLength), channel);
            return true;
        }

        /// <summary>
        /// Write another segment of the IAP binary
        /// </summary>
        /// <param name="stream">IAP binary</param>
        /// <returns>Whether another segment could be written</returns>
        public static bool WriteIapSegment(Stream stream)
        {
            _updating = true;

            Span<byte> data = stackalloc byte[Communication.Consts.IapSegmentSize];
            int bytesRead = stream.Read(data);
            if (bytesRead <= 0)
            {
                return false;
            }

            WritePacket(Communication.LinuxRequests.Request.WriteIap, bytesRead);
            data.Slice(0, bytesRead).CopyTo(GetWriteBuffer(bytesRead));
            return true;
        }

        /// <summary>
        /// Instruct the firmware to start the IAP binary
        /// </summary>
        public static void StartIap()
        {
            // Tell the firmware to boot the IAP program
            WritePacket(Communication.LinuxRequests.Request.StartIap);
            PerformFullTransfer(false);

            // Wait for IAP to start...
            do
            {
                Thread.Sleep(250);
            }
            while (!_transferReadyPin.Value);
            _transferReadyEvent.Set();
        }

        /// <summary>
        /// Flash another segment of the firmware via the IAP binary
        /// </summary>
        /// <param name="stream"></param>
        /// <returns>Whether another segment could be sent</returns>
        public static bool FlashFirmwareSegment(Stream stream)
        {
            Span<byte> segment = stackalloc byte[Communication.Consts.FirmwareSegmentSize];
            int bytesRead = stream.Read(segment);
            if (bytesRead <= 0)
            {
                return false;
            }

            if (bytesRead != Communication.Consts.FirmwareSegmentSize)
            {
                // Fill up the remaining space with 0xFF. The IAP program does the same once complete
                segment.Slice(bytesRead).Fill(0xFF);
            }

            // In theory the response of this could be checked to consist only of 0x1A bytes
            WaitForTransfer();
            _spiDevice.TransferFullDuplex(segment, segment);
            return true;
        }

        /// <summary>
        /// Send the CRC16 checksum of the firmware binary to the IAP program and verify the written data
        /// </summary>
        /// <param name="firmwareLength">Length of the written firmware in bytes</param>
        /// <param name="crc16">CRC16 checksum of the firmware</param>
        /// <returns>Whether the firmware has been written successfully</returns>
        public static bool VerifyFirmwareChecksum(long firmwareLength, ushort crc16)
        {
            // At this point IAP expects another segment so wait for it to be ready first. After that, wait a moment for IAP to acknowledge we're done
            WaitForTransfer();
            Thread.Sleep(Communication.Consts.FirmwareFinishedDelay);

            // Send the final firmware size plus CRC16 checksum to IAP
            Communication.LinuxRequests.FlashVerifyRequest verifyRequest = new Communication.LinuxRequests.FlashVerifyRequest
            {
                firmwareLength = (uint)firmwareLength,
                crc16 = crc16
            };
            Span<byte> transferData = stackalloc byte[Marshal.SizeOf(typeof(Communication.LinuxRequests.FlashVerifyRequest))];
            MemoryMarshal.Write(transferData, ref verifyRequest);
            WaitForTransfer();
            _spiDevice.TransferFullDuplex(transferData, transferData);

            // Check if the IAP can confirm our CRC16 checksum
            Span<byte> writeOk = stackalloc byte[1];
            WaitForTransfer();
            _spiDevice.TransferFullDuplex(writeOk, writeOk);
            return (writeOk[0] == 0x0C);
        }

        /// <summary>
        /// Wait for the IAP program to reset the controller
        /// </summary>
        public static void WaitForIapReset()
        {
            Thread.Sleep(Communication.Consts.IapRebootDelay);
            _waitingForFirstTransfer = true;
        }
        
        /// <summary>
        /// Assign a filament name to the given extruder drive
        /// </summary>
        /// <param name="extruder"></param>
        /// <param name="filamentName"></param>
        /// <returns>Whether the firmware has been written successfully</returns>
        public static bool WriteAssignFilament(int extruder, string filamentName)
        {
            // Serialize the request first to see how much space it requires
            Span<byte> span = stackalloc byte[Communication.Consts.BufferSize - Marshal.SizeOf(typeof(Communication.PacketHeader))];
            int dataLength = Serialization.Writer.WriteAssignFilament(span, extruder, filamentName);

            // See if the request fits into the buffer
            if (!CanWritePacket(dataLength))
            {
                return false;
            }

            // Write it
            WritePacket(Communication.LinuxRequests.Request.AssignFilament, dataLength);
            span.Slice(0, dataLength).CopyTo(GetWriteBuffer(dataLength));
            return true;
        }

        /// <summary>
        /// Write another chunk of the file being requested
        /// </summary>
        /// <param name="data">File chunk data</param>
        /// <param name="fileLength">Total length of the file in bytes</param>
        /// <returns>Whether the firmware has been written successfully</returns>
        public static bool WriteFileChunk(Span<byte> data, long fileLength)
        {
            // Serialize the request first to see how much space it requires
            Span<byte> span = stackalloc byte[Communication.Consts.BufferSize - Marshal.SizeOf(typeof(Communication.PacketHeader))];
            int dataLength = Serialization.Writer.WriteFileChunk(span, data, fileLength);

            // See if the request fits into the buffer
            if (!CanWritePacket(dataLength))
            {
                return false;
            }

            // Write it
            WritePacket(Communication.LinuxRequests.Request.FileChunk, dataLength);
            span.Slice(0, dataLength).CopyTo(GetWriteBuffer(dataLength));
            return true;
        }

        private static bool CanWritePacket(int dataLength = 0)
        {
            return _txPointer + Marshal.SizeOf(typeof(Communication.PacketHeader)) + dataLength <= Communication.Consts.BufferSize;
        }

        private static void WritePacket(Communication.LinuxRequests.Request request, int dataLength = 0)
        {
            Communication.PacketHeader header = new Communication.PacketHeader
            {
                Request = (ushort)request,
                Id = _packetId++,
                Length = (ushort)dataLength,
                ResendPacketId = 0
            };

            Span<byte> span = _txBuffers[_txBufferIndex].Slice(_txPointer).Span;
            MemoryMarshal.Write(span, ref header);
            _txPointer += Marshal.SizeOf(header);
        }

        private static Span<byte> GetWriteBuffer(int dataLength)
        {
            Span<byte> result = _txBuffers[_txBufferIndex].Slice(_txPointer, dataLength).Span;
            int padding = 4 - (dataLength % 4);
            _txPointer += dataLength + ((padding == 4) ? 0 : padding);
            return result;
        }
        #endregion

        #region Functions for data transfers
        private static void WaitForTransfer()
        {
            if (_updating)
            {
                _transferReadyEvent.Wait(Program.CancelSource.Token);
            }
            else if (_waitingForFirstTransfer)
            {
                _transferReadyEvent.Reset();
                if (!_transferReadyPin.Value)
                {
                    using CancellationTokenSource timeoutCts = new CancellationTokenSource(Settings.SpiTransferTimeout);
                    using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(Program.CancelSource.Token, timeoutCts.Token);
                    _transferReadyEvent.Wait(cts.Token);

                    if (Program.CancelSource.IsCancellationRequested)
                    {
                        throw new OperationCanceledException("Program termination");
                    }
                    if (timeoutCts.IsCancellationRequested)
                    {
                        throw new OperationCanceledException("Timeout while waiting for transfer ready pin");
                    }
                }
                _waitingForFirstTransfer = false;
            }
            else
            {
                using CancellationTokenSource timeoutCts = new CancellationTokenSource(Settings.SpiTransferTimeout);
                using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(Program.CancelSource.Token, timeoutCts.Token);
                _transferReadyEvent.Wait(cts.Token);

                if (timeoutCts.IsCancellationRequested)
                {
                    throw new OperationCanceledException("Timeout while waiting for transfer ready pin");
                }
            }
            _transferReadyEvent.Reset();
        }

        private static bool ExchangeHeader()
        {
            for (int retry = 0; retry < Settings.MaxSpiRetries; retry++)
            {
                // Perform SPI header exchange
                WaitForTransfer();
                _spiDevice.TransferFullDuplex(_txHeaderBuffer.Span, _rxHeaderBuffer.Span);

                // Check for possible response code
                uint responseCode = MemoryMarshal.Read<uint>(_rxHeaderBuffer.Span);
                if (responseCode == Communication.TransferResponse.BadResponse)
                {
                    Console.WriteLine("[warn] Restarting transfer because the Duet received a bad response (header)");
                    return false;
                }

                // Inspect received header
                _rxHeader = MemoryMarshal.Cast<byte, Communication.TransferHeader>(_rxHeaderBuffer.Span)[0];
                if (_rxHeader.FormatCode == 0 || _rxHeader.FormatCode == 0xFF)
                {
                    throw new OperationCanceledException("Board is not available (no header)");
                }

                ushort checksum = CRC16.Calculate(_rxHeaderBuffer.Slice(0, Marshal.SizeOf(_rxHeader) - Marshal.SizeOf(typeof(ushort))).Span);
                if (_rxHeader.ChecksumHeader != checksum)
                {
                    Console.WriteLine($"[warn] Bad header checksum (expected 0x{_rxHeader.ChecksumHeader:x4}, got 0x{checksum:x4})");
                    responseCode = ExchangeResponse(Communication.TransferResponse.BadHeaderChecksum);
                    if (responseCode == Communication.TransferResponse.BadResponse)
                    {
                        Console.WriteLine("[warn] Restarting transfer because the Duet received a bad response (header response)");
                        return false;
                    }
                    if (responseCode != Communication.TransferResponse.Success)
                    {
                        Console.WriteLine($"[warn] Note: RepRapFirmware didn't receive valid data either (code 0x{responseCode:x8})");
                    }
                    continue;
                }

                if (_rxHeader.FormatCode != Communication.Consts.FormatCode)
                {
                    ExchangeResponse(Communication.TransferResponse.BadFormat);
                    throw new Exception($"Invalid format code {_rxHeader.FormatCode:x2}");
                }
                if (_rxHeader.ProtocolVersion != Communication.Consts.ProtocolVersion)
                {
                    ExchangeResponse(Communication.TransferResponse.BadProtocolVersion);
                    throw new Exception($"Invalid protocol version {_rxHeader.ProtocolVersion}");
                }
                if (_rxHeader.DataLength > Communication.Consts.BufferSize)
                {
                    ExchangeResponse(Communication.TransferResponse.BadDataLength);
                    throw new Exception($"Data too long ({_rxHeader.DataLength} bytes)");
                }

                // Acknowledge reception
                uint response = ExchangeResponse(Communication.TransferResponse.Success);
                switch (response)
                {
                    case 0:
                    case 0xFFFFFFFF:
                        throw new OperationCanceledException("Board is not available (no header response)");

                    case Communication.TransferResponse.Success:
                        return true;

                    case Communication.TransferResponse.BadFormat:
                        throw new Exception("RepRapFirmware refused message format");

                    case Communication.TransferResponse.BadProtocolVersion:
                        throw new Exception("RepRapFirmware refused protocol version");

                    case Communication.TransferResponse.BadDataLength:
                        throw new Exception("RepRapFirmware refused data length");

                    case Communication.TransferResponse.BadHeaderChecksum:
                        Console.WriteLine("[warn] RepRapFirmware got a bad header checksum");
                        continue;

                    case Communication.TransferResponse.BadResponse:
                        Console.WriteLine("[warn] Restarting transfer because RepRapFirmware received a bad response (header response)");
                        return false;

                    default:
                        Console.WriteLine($"[warn] Restarting transfer because a bad header response was received (0x{response:x8})");
                        ExchangeResponse(Communication.TransferResponse.BadResponse);
                        return false;
                }
            }

            Console.WriteLine("[warn] Restarting transfer because the number of maximum retries has been exceeded");
            ExchangeResponse(Communication.TransferResponse.BadResponse);
            return false;
        }

        private static uint ExchangeResponse(uint response)
        {
            MemoryMarshal.Write(_txResponseBuffer.Span, ref response);

            WaitForTransfer();
            _spiDevice.TransferFullDuplex(_txResponseBuffer.Span, _rxResponseBuffer.Span);

            return MemoryMarshal.Read<uint>(_rxResponseBuffer.Span);
        }

        private static bool ExchangeData()
        {
            int bytesToTransfer = Math.Max(_rxHeader.DataLength, _txPointer);
            for (int retry = 0; retry < Settings.MaxSpiRetries; retry++)
            {
                WaitForTransfer();
                _spiDevice.TransferFullDuplex(_txBuffers[_txBufferIndex].Slice(0, bytesToTransfer).Span, _rxBuffer.Slice(0, bytesToTransfer).Span);

                // Check for possible response code
                uint responseCode = MemoryMarshal.Read<uint>(_rxBuffer.Span);
                if (responseCode == Communication.TransferResponse.BadResponse)
                {
                    Console.WriteLine("[warn] Restarting transfer because RepRapFirmware received a bad response (data content)");
                    return false;
                }

                // Inspect received data
                ushort checksum = CRC16.Calculate(_rxBuffer.Slice(0, _rxHeader.DataLength).Span);
                if (_rxHeader.ChecksumData != checksum)
                {
                    Console.WriteLine($"[warn] Bad data checksum (expected 0x{_rxHeader.ChecksumData:x4}, got 0x{checksum:x4})");
                    responseCode = ExchangeResponse(Communication.TransferResponse.BadDataChecksum);
                    if (responseCode == Communication.TransferResponse.BadResponse)
                    {
                        Console.WriteLine("[warn] Restarting transfer because the Duet received a bad response (data response)");
                        return false;
                    }
                    if (responseCode != Communication.TransferResponse.Success)
                    {
                        Console.WriteLine($"[warn] Note: RepRapFirmware didn't receive valid data either (code 0x{responseCode:x8})");
                    }
                    continue;
                }

                uint response = ExchangeResponse(Communication.TransferResponse.Success);
                switch (response)
                {
                    case 0:
                    case 0xFFFFFFFF:
                        throw new OperationCanceledException("Board is not available (no data response)");

                    case Communication.TransferResponse.Success:
                        return true;

                    case Communication.TransferResponse.BadDataChecksum:
                        Console.WriteLine("[warn] RepRapFirmware got a bad data checksum");
                        continue;

                    case Communication.TransferResponse.BadResponse:
                        Console.WriteLine("[warn] Restarting transfer because RepRapFirmware received a bad response (data response)");
                        return false;

                    default:
                        Console.WriteLine($"[warn] Restarting transfer because a bad data response was received (0x{response:x8})");
                        ExchangeResponse(Communication.TransferResponse.BadResponse);
                        return false;
                }
            }

            Console.WriteLine("[warn] Restarting transfer because the number of maximum retries has been exceeded");
            ExchangeResponse(Communication.TransferResponse.BadResponse);
            return false;
        }
        #endregion
    }
}
