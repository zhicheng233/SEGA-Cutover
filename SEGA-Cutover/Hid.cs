using System;
using System.Threading;

namespace SEGA_Cutover;
using HidSharp;
using System.Diagnostics;

[Flags]
public enum HidButtons
{
    None = 0,
    Button4 = 1,
    Button5 = 2
}

public class Hid
{
    public HidButtons PressedButtons { get; private set; }

    public bool TryProcessReport(ReadOnlySpan<byte> report, out HidButtons newlyPressed)
    {
        newlyPressed = HidButtons.None;

        // This device sends 63 payload bytes plus the Report ID.
        if (report.Length < 64 || report[0] != 0x01)
        {
            return false;
        }

        // HidSharp includes Report ID at index 0; payload[29] is report[30].
        byte buttonState = report[30];
        var current = HidButtons.None;
        if ((buttonState & 0x80) == 0) current |= HidButtons.Button4;
        if ((buttonState & 0x40) == 0) current |= HidButtons.Button5;

        newlyPressed = current & ~PressedButtons;
        PressedButtons = current;
        return true;
    }

    public static void ReadHid(CancellationToken ct, Action<HidButtons>? onButtonPressed = null)
    {
        const int pid = 0x0021;
        const int vid = 0x0ca3;

        var device = DeviceList.Local.GetHidDeviceOrNull(vid, pid)
                     ?? throw new InvalidOperationException("HID device not found.");

        using var stream = device.Open();
        stream.ReadTimeout = 500;

        var buffer = new byte[device.GetMaxInputReportLength()];
        var input = new Hid();

        while (!ct.IsCancellationRequested)
        {
            int count;
            try
            {
                count = stream.Read(buffer, 0, buffer.Length);
            }
            catch (TimeoutException)
            {
                // Periodically check cancellation while no input arrives.
                continue;
            }

            if (count == 0 || ct.IsCancellationRequested) break;

            var report = buffer.AsSpan(0, count);

            if (input.TryProcessReport(report, out var newlyPressed) && newlyPressed != HidButtons.None)
            {
                Console.WriteLine($"HID pressed: {newlyPressed}");
                onButtonPressed?.Invoke(newlyPressed);
            }
        }
    }
}
