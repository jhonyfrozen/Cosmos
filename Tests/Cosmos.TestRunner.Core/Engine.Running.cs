﻿using System;
using System.Threading;

using Cosmos.Debug.DebugConnectors;
using Cosmos.Debug.Hosts;

namespace Cosmos.TestRunner.Core
{
    partial class Engine
    {
        // this file contains code handling situations when a kernel is running
        // most of this is debug stub related

        private volatile bool mKernelRunning;
        private volatile bool mKernelResult;
        private int mSucceededAssertions;

        private void InitializeDebugConnector(DebugConnector aDebugConnector)
        {
            void LogMessage(string aMessage) => OutputHandler.LogMessage(aMessage);

            void AbortTestAndLogError(string aMessage)
            {
                OutputHandler.LogError(aMessage);
                mKernelRunning = false;
            }

            void AbortTestAndLogException(Exception aException, string aMessage)
            {
                OutputHandler.LogError(aMessage);
                OutputHandler.UnhandledException(aException);

                mKernelRunning = false;
            }

            if (aDebugConnector == null)
            {
                throw new ArgumentNullException(nameof(aDebugConnector));
            }

            aDebugConnector.OnDebugMsg = s => OutputHandler.LogDebugMessage(s);

            aDebugConnector.ConnectionLost = e => AbortTestAndLogException(e, "DC: Connection lost.");

            aDebugConnector.CmdChannel = (a1, a2, a3) => ChannelPacketReceived(a1, a2, a3);

            aDebugConnector.CmdStarted = () =>
            {
                LogMessage("DC: Started");
                aDebugConnector.SendCmd(Vs2Ds.BatchEnd);
            };

            aDebugConnector.Error = e => AbortTestAndLogException(e, "DC Error.");

            aDebugConnector.CmdText += s => LogMessage("Text from kernel: " + s);

            aDebugConnector.CmdSimpleNumber += n => LogMessage(
                "Number from kernel: 0x" + n.ToString("X8").ToUpper());

            aDebugConnector.CmdSimpleLongNumber += n => LogMessage(
                "Number from kernel: 0x" + n.ToString("X16").ToUpper());

            aDebugConnector.CmdComplexNumber += f => LogMessage(
                "Number from kernel: 0x" + f.ToString("X8").ToUpper());

            aDebugConnector.CmdComplexLongNumber += d => LogMessage(
                "Number from kernel: 0x" + d.ToString("X16").ToUpper());

            aDebugConnector.CmdMessageBox = s => LogMessage(
                "MessageBox from kernel: " + s);

            aDebugConnector.CmdKernelPanic = n =>
            {
                LogMessage("Kernel panic! Number = " + n);
                // todo: add core dump here, call stack.
            };

            aDebugConnector.CmdTrace = t => { };

            aDebugConnector.CmdBreak = t => { };

            aDebugConnector.CmdStackCorruptionOccurred = a => AbortTestAndLogError(
                "Stackcorruption occurred at: 0x" + a.ToString("X8"));

            aDebugConnector.CmdStackOverflowOccurred = a => AbortTestAndLogError(
                "Stack overflow occurred at: 0x" + a.ToString("X8"));

            aDebugConnector.CmdNullReferenceOccurred = a => AbortTestAndLogError(
                "Null Reference Exception occurred at: 0x" + a.ToString("X8"));

            aDebugConnector.CmdCoreDump = b =>
            {
                string xCallStack = "";
                int i = 0;

                OutputHandler.LogMessage("Core dump:");
                string eax = "EAX = 0x" +
                             b[i + 3].ToString("X2") + b[i + 2].ToString("X2") +
                             b[i + 0].ToString("X2") + b[i + 1].ToString("X2");
                i += 4;
                string ebx = "EBX = 0x" +
                             b[i + 3].ToString("X2") + b[i + 2].ToString("X2") +
                             b[i + 0].ToString("X2") + b[i + 1].ToString("X2");
                i += 4;
                string ecx = "ECX = 0x" +
                             b[i + 3].ToString("X2") + b[i + 2].ToString("X2") +
                             b[i + 0].ToString("X2") + b[i + 1].ToString("X2");
                i += 4;
                string edx = "EDX = 0x" +
                             b[i + 3].ToString("X2") + b[i + 2].ToString("X2") +
                             b[i + 0].ToString("X2") + b[i + 1].ToString("X2");
                i += 4;
                string edi = "EDI = 0x" +
                             b[i + 3].ToString("X2") + b[i + 2].ToString("X2") +
                             b[i + 0].ToString("X2") + b[i + 1].ToString("X2");
                i += 4;
                string esi = "ESI = 0x" +
                             b[i + 3].ToString("X2") + b[i + 2].ToString("X2") +
                             b[i + 0].ToString("X2") + b[i + 1].ToString("X2");
                i += 4;
                string ebp = "EBP = 0x" +
                             b[i + 3].ToString("X2") + b[i + 2].ToString("X2") +
                             b[i + 0].ToString("X2") + b[i + 1].ToString("X2");
                i += 4;
                string eip = "EIP = 0x" +
                             b[i + 3].ToString("X2") + b[i + 2].ToString("X2") +
                             b[i + 0].ToString("X2") + b[i + 1].ToString("X2");
                i += 4;
                string esp = "ESP = 0x" +
                             b[i + 3].ToString("X2") + b[i + 2].ToString("X2") +
                             b[i + 0].ToString("X2") + b[i + 1].ToString("X2");
                i += 4;

                OutputHandler.LogMessage(eax + " " + ebx + " " + ecx + " " + edx);
                OutputHandler.LogMessage(edi + " " + esi);
                OutputHandler.LogMessage(ebp + " " + esp + " " + eip);
                OutputHandler.LogMessage("");

                while (i < b.Length)
                {
                    string xAddress = "0x" +
                                      b[i + 3].ToString("X2") + b[i + 2].ToString("X2") +
                                      b[i + 0].ToString("X2") + b[i + 1].ToString("X2");
                    xCallStack += xAddress + " ";
                    if ((i != 0) && (i % 12 == 0))
                    {
                        OutputHandler.LogMessage(xCallStack.Trim());
                        xCallStack = "";
                    }
                    i += 4;
                }
                if (xCallStack != "")
                {
                    OutputHandler.LogMessage(xCallStack.Trim());
                    xCallStack = "";
                }
            };

            if (RunWithGDB)
            {
                aDebugConnector.CmdInterruptOccurred = a =>
                {
                    OutputHandler.LogMessage($"Interrupt {a} occurred");
                };
            }
        }

        private void HandleRunning(DebugConnector debugConnector, Host host)
        {
            if (debugConnector == null)
            {
                throw new ArgumentNullException("debugConnector");
            }
            if (host == null)
            {
                throw new ArgumentNullException("host");
            }

            mKernelRunning = true;
            host.Start();

            try
            {
                var xStartTime = DateTime.Now;
                Interlocked.Exchange(ref mSucceededAssertions, 0);

                while (mKernelRunning)
                {
                    Thread.Sleep(50);

                    if (Math.Abs(DateTime.Now.Subtract(xStartTime).TotalSeconds) > AllowedSecondsInKernel)
                    {
                        throw new TimeoutException("Timeout exceeded!");
                    }
                }
            }
            finally
            {
                host.Stop();
                debugConnector.Dispose();
                Thread.Sleep(50);
            }
        }

        private void ChannelPacketReceived(byte arg1, byte arg2, byte[] arg3)
        {
            if (arg1 == 129)
            {
                // for now, skip
                return;
            }
            if (arg1 == TestController.TestChannel)
            {
                switch (arg2)
                {
                    case (byte)TestChannelCommandEnum.TestCompleted:
                        KernelTestCompleted();
                        break;
                    case (byte)TestChannelCommandEnum.TestFailed:
                        KernelTestFailed();
                        break;
                    case (byte)TestChannelCommandEnum.AssertionSucceeded:
                        KernelAssertionSucceeded();
                        break;
                }
            }
            else
            {
                OutputHandler.LogMessage($"ChannelPacketReceived, Channel = {arg1}, Command = {arg2}");
            }
        }

        private void KernelAssertionSucceeded()
        {
            Interlocked.Increment(ref mSucceededAssertions);
        }

        private void KernelTestFailed()
        {
            OutputHandler.SetKernelTestResult(false, "Test failed");

            mKernelResult = false;
            mKernelRunning = false;
        }

        private void KernelTestCompleted()
        {
            OutputHandler.SetKernelTestResult(true, "Test completed");

            mKernelResult = true;
            mKernelRunning = false;
        }
    }
}
