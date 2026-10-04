using System;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;

class Program {
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetPrivateProfileString(string lpAppName, string lpKeyName, string lpDefault, StringBuilder lpReturnedString, uint nSize, string lpFileName);

    [DllImport("user32.dll", CharSet = CharSet.Auto)] 
    public static extern int SystemParametersInfo(uint uAction, uint uParam, string lpvParam, uint fuWinIni);
    
    [DllImport("user32.dll", CharSet = CharSet.Auto)] 
    public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    static string GetValue(string file, string section, string key) {
        StringBuilder sb = new StringBuilder(512);
        GetPrivateProfileString(section, key, "", sb, (uint)sb.Capacity, file);
        return sb.ToString();
    }

    static void SetRegistry(string keyPath, string valueName, object value, RegistryValueKind kind = RegistryValueKind.String) {
        if (value == null || string.IsNullOrEmpty(value.ToString())) return;
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath, true)) {
            if (key != null) key.SetValue(valueName, value, kind);
        }
    }

    static void Main(string[] args) {
        if (args.Length == 0) return;
        string themePath = args[0];

        // 1. 壁紙の取得と反映
        string wallpaper = GetValue(themePath, @"Control Panel\Desktop", "Wallpaper");
        SetRegistry(@"Control Panel\Desktop", "Wallpaper", wallpaper);
        if (!string.IsNullOrEmpty(wallpaper)) SystemParametersInfo(0x0014, 0, wallpaper, 0x01 | 0x02);

        // 2. マウスカーソル（全15種類）をすべて網羅して反映
        string[] cursorKeys = { 
            "Arrow", "AppStarting", "Wait", "Help", "SizeNWSE", "SizeNESW", 
            "SizeWE", "SizeNS", "SizeAll", "No", "Hand", "Crosshair", "IBeam", "NWPen", "UpArrow" 
        };
        foreach (string cursorKey in cursorKeys) {
            string cPath = GetValue(themePath, @"Control Panel\Cursors", cursorKey);
            SetRegistry(@"Control Panel\Cursors", cursorKey, cPath);
        }
        SystemParametersInfo(0x0065, 0, null, 0x01 | 0x02); // カーソル一括リロード

        // 3. 配色・アクセントカラーの反映
        string colorPalette = GetValue(themePath, @"VisualStyles", "ColorPalette");
        if (!string.IsNullOrEmpty(colorPalette)) {
            // DWM（ウィンドウマネージャー）のカラー変更
            SetRegistry(@"Software\Microsoft\Windows\DWM", "AccentColor", colorPalette);
            // ダークモード(0) / ライトモード(1) の判定と切り替え
            int isLightMode = colorPalette.ToLower().Contains("light") ? 1 : 0;
            SetRegistry(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", isLightMode, RegistryValueKind.DWord);
            SetRegistry(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", isLightMode, RegistryValueKind.DWord);
        }

        // 4. 効果音（主要なシステムサウンド）の反映
        string[] soundEvents = { "SystemNotification", "SystemHand", "SystemQuestion", "SystemExclamation", "SystemAsterisk" };
        foreach (string sndEvent in soundEvents) {
            string sndPath = GetValue(themePath, @"AppEvents\Schemes\Apps\.Default\" + sndEvent + @"\.Current", "DefaultValue");
            if (!string.IsNullOrEmpty(sndPath)) {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"AppEvents\Schemes\Apps\.Default\" + sndEvent + @"\.Current", true)) {
                    if (k != null) k.SetValue("", sndPath);
                }
            }
        }

        // 5. OS全体へ「設定が変わったよ」と通知をブロードキャスト（フォーカスは奪いません）
        IntPtr res = IntPtr.Zero;
        SendMessageTimeout((IntPtr)0xffff, 0x001A, IntPtr.Zero, "Environment", 2, 5000, out res);
        SendMessageTimeout((IntPtr)0xffff, 0x001A, IntPtr.Zero, "Sound", 2, 5000, out res);
    }
}
