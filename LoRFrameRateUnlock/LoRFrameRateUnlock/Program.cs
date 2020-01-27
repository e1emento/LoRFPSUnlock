﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace LoRFrameRateUnlock
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
               await FpsUnlock();
            }
            catch (Exception e)
            {
                DoLog(e.Message, ConsoleColor.Red);
            }

            DoLog("press any key to exit");
            Console.ReadKey(true);
        }

        private static async Task FpsUnlock()
        {
            // we need to be running in 64bit
            if(IntPtr.Size == sizeof(int))
                throw new Exception("Please compile and run as 64 bit process");

            DoLog("Waiting for Legends for Runeterra process...");

            // find target process
            var process = await GetOrWaitForProcess("LoR");

            DoLog($"{process.ProcessName} 0x{process.Id:x}", ConsoleColor.Green);

            // obtain handle
            var handle = WinApi.OpenProcess(
                ProcessAccessFlags.All,
                false,
                process.Id);

            if (handle == IntPtr.Zero)
                throw new Exception("Failed to open process");

            // find unity player module
            var unityPlayer = process.Modules.Cast<ProcessModule>().First(m => m.ModuleName == "UnityPlayer.dll")
                .BaseAddress;

            DoLog($"UnityPlayer.dll 0x{unityPlayer.ToInt64():x}", ConsoleColor.Green);

            // our GraphicsManager pointer chain
            var offsetChain = new[] {0x48, 0x158, 0x930};
            var offset0 = 0x014B0A60;

            var address = unityPlayer + offset0;

            foreach (var offset in offsetChain)
            {
                address = Read(handle, address);

                if (address == IntPtr.Zero)
                    throw new Exception("Failed to read pointer chain, make sure your offsets are up to date");

                address += offset;
            }

            // override the default value
            if (!Write(handle, address + 0x3c, 999))
                throw new Exception("Could not override memory value");

            // we are done
            DoLog("Frame rate has been unlocked");
        }

        private static async Task<Process> GetOrWaitForProcess(string name, TimeSpan? minimumInitTime = null)
        {
            Process p;

            while ((p = Process.GetProcessesByName(name).FirstOrDefault()) == null)
                await Task.Delay(500);

            while ((DateTime.Now - p.StartTime) < (minimumInitTime ?? TimeSpan.FromSeconds(5)))
                await Task.Delay(500);

            return p;
        }

        private static void DoLog(string log, ConsoleColor? color = null)
        {
            Console.ForegroundColor = color ?? ConsoleColor.White;
            Console.WriteLine(log);
        }

        private static IntPtr Read(IntPtr handle, IntPtr address)
        {
            var result = WinApi.ReadProcessMemory(handle, address, out var value, sizeof(long), out _);
            return !result ? IntPtr.Zero : new IntPtr(value);
        }

        private static bool Write(IntPtr handle, IntPtr address, int value)
        {
            return WinApi.WriteProcessMemory(handle, address, ref value, sizeof(int), out _);
        }
    }
}
