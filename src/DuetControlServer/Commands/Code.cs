﻿using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DuetAPI;
using DuetAPI.Commands;
using DuetAPI.Connection;
using DuetControlServer.Codes;
using DuetControlServer.IPC.Processors;
using DuetControlServer.SPI;
using Nito.AsyncEx;

namespace DuetControlServer.Commands
{
    /// <summary>
    /// Implementation of the <see cref="DuetAPI.Commands.Code"/> command
    /// </summary>
    public class Code : DuetAPI.Commands.Code
    {
        /// <summary>
        /// Logger instance
        /// </summary>
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        #region Code Scheduler
        /// <summary>
        /// Queue of semaphores to guarantee the ordered execution of incoming G/M/T-codes
        /// </summary>
        private static Queue<AsyncSemaphore>[] _codeTickets;

        /// <summary>
        /// List of cancellation tokens to cancel pending codes while they are waiting for their execution
        /// </summary>
        private static CancellationTokenSource[] _cancellationTokenSources;

        /// <summary>
        /// Initialize the code scheduler
        /// </summary>
        public static void InitScheduler()
        {
            int numCodeChannels = Enum.GetValues(typeof(CodeChannel)).Length;
            _codeTickets = new Queue<AsyncSemaphore>[numCodeChannels];
            _cancellationTokenSources = new CancellationTokenSource[numCodeChannels];
            for (int i = 0; i < numCodeChannels; i++)
            {
                _codeTickets[i] = new Queue<AsyncSemaphore>();
                _cancellationTokenSources[i] = new CancellationTokenSource();
            }
        }

        /// <summary>
        /// Cancel pending codes of the given channel
        /// </summary>
        /// <param name="channel">Channel to cancel codes from</param>
        public static void CancelPending(CodeChannel channel)
        {
            lock (_cancellationTokenSources)
            {
                // Cancel and dispose the existing CTS
                CancellationTokenSource oldCTS = _cancellationTokenSources[(int)channel];
                oldCTS.Cancel();
                oldCTS.Dispose();

                // Create a new one
                _cancellationTokenSources[(int)channel] = new CancellationTokenSource();
            }
        }

        /// <summary>
        /// Get the next ticket for a code being enqueued for execution
        /// </summary>
        /// <returns>Ticket semaphore</returns>
        private AsyncSemaphore GetTicket()
        {
            lock (_codeTickets)
            {
                Queue<AsyncSemaphore> tickets = _codeTickets[(int)Channel];
                if (tickets.TryPeek(out _))
                {
                    // There are codes being executed. Create a new semaphore
                    AsyncSemaphore semaphore = new AsyncSemaphore(1);
                    tickets.Enqueue(semaphore);
                    return semaphore;
                }
                else
                {
                    // No codes are being executed, we don't need to wait for execution
                    tickets.Enqueue(null);
                    return null;
                }
            }
        }

        /// <summary>
        /// Wait until the given ticket flags readiness for the next code to be executed
        /// </summary>
        /// <param name="ticket">Ticket to wait for</param>
        /// <returns>Asynchronous task</returns>
        private async Task WaitForExecution(AsyncSemaphore ticket)
        {
            // Get the current cancellation token of this channel
            CancellationToken channelToken;
            lock (_cancellationTokenSources)
            {
                channelToken = _cancellationTokenSources[(int)Channel].Token;
            }

            // Wait until the last code has been processed or sent to RRF
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(channelToken, Program.CancelSource.Token);
            await ticket.WaitAsync(cts.Token);
        }

        /// <summary>
        /// Indicates if this code has already started the next one
        /// </summary>
        private bool nextCodeStarted;

        /// <summary>
        /// Start the next available G/M/T-code unless this code has already started one
        /// </summary>
        private void StartNextCode()
        {
            if (!nextCodeStarted)
            {
                lock (_codeTickets)
                {
                    if (_codeTickets[(int)Channel].TryDequeue(out AsyncSemaphore semaphore))
                    {
                        semaphore?.Release();
                    }
                }
                nextCodeStarted = true;
            }
        }
        #endregion

        /// <summary>
        /// Create an empty Code instance
        /// </summary>
        public Code() { }

        /// <summary>
        /// Create a new Code instance and attempt to parse the given code string
        /// </summary>
        /// <param name="code">G/M/T-Code</param>
        public Code(string code) : base(code) { }

        /// <summary>
        /// Indicates whether the code has been internally processed
        /// </summary>
        [JsonIgnore]
        public bool InternallyProcessed { get; set; }

        /// <summary>
        /// Run an arbitrary G/M/T-code and wait for it to finish
        /// </summary>
        /// <returns>Result of the code</returns>
        /// <exception cref="OperationCanceledException">Code has been cancelled</exception>
        public override async Task<CodeResult> Execute()
        {
            if (!Flags.HasFlag(CodeFlags.IsPrioritized) && !Flags.HasFlag(CodeFlags.IsFromMacro))
            {
                // Take a ticket and wait until it is our turn to execute.
                // This ensures that the order of executing G/M/T-codes is sequential
                AsyncSemaphore ticket = GetTicket();
                if (ticket != null)
                {
                    await WaitForExecution(ticket);
                }
            }

            try
            {
                // Process this code
                _logger.Debug("Processing {0}", this);
                await Process();
            }
            catch (Exception e)
            {
                // Cancelling a code clears the result
                Result = null;
                if (e is OperationCanceledException)
                {
                    _logger.Debug("Cancelled {0}", this);
                }
                else
                {
                    _logger.Error(e, "Code {0} has caused an exception", this);
                }
                await CodeExecuted();
                throw;
            }
            finally
            {
                // Always interpret the result of this code
                await CodeExecuted();
                _logger.Debug("Completed {0}", this);
            }
            return Result;
        }

        private async Task Process()
        {
            // Attempt to process the code internally first
            if (!InternallyProcessed && await ProcessInternally())
            {
                return;
            }

            // Comments are resolved in DCS
            if (Type == CodeType.Comment)
            {
                Result = new CodeResult();
                return;
            }

            // Send the code to RepRapFirmware
            if (Flags.HasFlag(CodeFlags.Asynchronous))
            {
                // Enqueue the code for execution by RRF and return no result
                Task<CodeResult> codeTask = Interface.ProcessCode(this);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Process this code via RRF asynchronously
                        Result = await codeTask;
                        await Model.Provider.Output(Result);
                    }
                    catch (OperationCanceledException)
                    {
                        // Deal with cancelled codes
                        await CodeExecuted();
                        _logger.Debug("Cancelled {0}", this);
                        throw;
                    }
                    catch (Exception e)
                    {
                        // Deal with exeptions of asynchronous codes
                        if (e is AggregateException ae)
                        {
                            e = ae.InnerException;
                        }
                        _logger.Error(e, "Failed to execute {0} asynchronously", this);
                    }
                    finally
                    {
                        // Always interpret the result of this code
                        await CodeExecuted();
                        _logger.Debug("Completed {0} asynchronously", this);
                    }
                });

                // Start the next code
                StartNextCode();
            }
            else
            {
                // RepRapFirmware buffers a number of codes so a new code can be started before the last one has finished
                StartNextCode();

                // Wait for the code to complete
                Result = await Interface.ProcessCode(this);
            }
        }

        private async Task<bool> ProcessInternally()
        {
            // Pre-process this code
            if (!Flags.HasFlag(CodeFlags.IsPreProcessed))
            {
                bool resolved = await Interception.Intercept(this, InterceptionMode.Pre);
                Flags |= CodeFlags.IsPreProcessed;

                if (resolved)
                {
                    InternallyProcessed = true;
                    return true;
                }
            }

            // Attempt to process the code internally
            switch (Type)
            {
                case CodeType.GCode:
                    Result = await GCodes.Process(this);
                    break;

                case CodeType.MCode:
                    Result = await MCodes.Process(this);
                    break;

                case CodeType.TCode:
                    Result = await TCodes.Process(this);
                    break;
            }

            if (Result != null)
            {
                InternallyProcessed = true;
                return true;
            }

            // If the code could not be interpreted internally, post-process it
            if (!Flags.HasFlag(CodeFlags.IsPostProcessed))
            {
                bool resolved = await Interception.Intercept(this, InterceptionMode.Post);
                Flags |= CodeFlags.IsPostProcessed;

                if (resolved)
                {
                    InternallyProcessed = true;
                    return true;
                }
            }

            // Code has not been interpreted yet - let RRF deal with it
            return false;
        }

        private async Task CodeExecuted()
        {
            // Start the next code if that hasn't happened yet
            StartNextCode();

            if (Result != null)
            {
                // Process the code result
                switch (Type)
                {
                    case CodeType.GCode:
                        await GCodes.CodeExecuted(this);
                        break;

                    case CodeType.MCode:
                        await MCodes.CodeExecuted(this);
                        break;

                    case CodeType.TCode:
                        await TCodes.CodeExecuted(this);
                        break;
                }

                // RepRapFirmware generally prefixes error messages with the code itself.
                // Do this only for error messages that originate either from a print or from a macro file
                if (Flags.HasFlag(CodeFlags.IsFromMacro) || Channel == CodeChannel.File)
                {
                    foreach (Message msg in Result)
                    {
                        if (msg.Type == MessageType.Error)
                        {
                            msg.Content = ToShortString() + ": " + msg.Content;
                        }
                    }
                }

                // Log warning and error replies after the code has been processed internally
                if (InternallyProcessed)
                {
                    foreach (Message msg in Result)
                    {
                        if (msg.Type != MessageType.Success && Channel != CodeChannel.File)
                        {
                            await Utility.Logger.Log(msg);
                        }
                    }
                }
            }

            // Done
            await Interception.Intercept(this, InterceptionMode.Executed);
        }
    }
}