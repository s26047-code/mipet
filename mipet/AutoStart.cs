using System;
using System.Diagnostics;
using Microsoft.Win32;
using System.IO;

namespace mipet
{
    // Windows 시작 시 자동 실행 등록/해제
    public static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "mipet";

        // 지금 실행 중인 exe의 경로
        private static string ExePath
        {
            get
            {
                return Environment.ProcessPath
                       ?? Process.GetCurrentProcess().MainModule?.FileName
                       ?? "";
            }
        }

        // 자동 실행이 등록되어 있는가?
        public static bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
                string? registeredPath = key?.GetValue(AppName) as string;

                return string.Equals(
                    registeredPath?.Trim('"'),
                    ExePath,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static bool SetEnabled(bool enable, out string? error)
        {
            error = null;

            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
                if (key == null)
                    throw new InvalidOperationException("자동 시작 레지스트리 키를 열 수 없습니다.");

                if (enable)
                {
                    string exePath = ExePath;

                    if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
                        throw new FileNotFoundException("실행 중인 mipet.exe 경로를 찾을 수 없습니다.", exePath);

                    string command = $"\"{exePath}\"";
                    key.SetValue(AppName, command, RegistryValueKind.String);

                    string? savedCommand = key.GetValue(AppName) as string;
                    if (!string.Equals(savedCommand, command, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("자동 시작 경로 저장을 확인하지 못했습니다.");
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}