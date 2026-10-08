using System;
using System.Diagnostics;
using Microsoft.Win32;

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
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }

        // 등록(true) / 해제(false)
        public static void SetEnabled(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
                if (key == null) return;

                if (enable)
                    key.SetValue(AppName, "\"" + ExePath + "\"");   // 공백이 있는 경로도 안전하게 따옴표로 감쌈
                else
                    key.DeleteValue(AppName, false);
            }
            catch
            {
                // 실패해도 프로그램은 계속 동작
            }
        }
    }
}