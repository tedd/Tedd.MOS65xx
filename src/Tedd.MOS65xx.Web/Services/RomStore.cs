using System.Net.Http;
using System.Runtime.Versioning;
using Tedd.MOS65xx.Emulator.C64;
using Tedd.MOS65xx.Web.Interop;

namespace Tedd.MOS65xx.Web.Services;

/// <summary>Which ROM image an uploaded file is meant for.</summary>
public enum RomSlot { Basic, Kernal, Chargen, Drive, Combined }

/// <summary>
/// Collects the ROM images the browser build can use, from three sources in priority order:
/// images the user uploaded earlier (kept in localStorage as base64), files served by the site at
/// <c>roms/basic.bin</c>, <c>roms/kernal.bin</c>, <c>roms/chargen.bin</c>, <c>roms/1541.bin</c> (the deploy job
/// puts OpenROMs there), and fresh uploads.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class RomStore
{
    private const string KeyPrefix = "tedd.mos65xx.rom.";

    public byte[]? Basic { get; private set; }
    public byte[]? Kernal { get; private set; }
    public byte[]? Chargen { get; private set; }
    public byte[]? Drive { get; private set; }
    private byte[]? _driveLow, _driveHigh;

    public string BasicName { get; private set; } = "";
    public string KernalName { get; private set; } = "";
    public string ChargenName { get; private set; } = "";
    public string DriveName { get; private set; } = "";

    /// <summary>Where the current set came from ("localStorage", "site", "upload" or a mix).</summary>
    public string Source { get; private set; } = "";

    public bool IsComplete => Basic is not null && Kernal is not null && Chargen is not null;
    public bool HasDrive => Drive is not null;

    /// <summary>True when the KERNAL looks like OpenROMs (their KERNAL contains an "OPEN ROMS" banner).</summary>
    public bool LooksLikeOpenRoms => Kernal is not null && Contains(Kernal, "OPEN ROMS"u8) || Basic is not null && Contains(Basic, "OPEN ROMS"u8);

    public string Description
    {
        get
        {
            if (!IsComplete) return "no ROM set";
            var parts = new List<string> { "BASIC=" + BasicName, "KERNAL=" + KernalName, "CHAR=" + ChargenName };
            if (Drive is not null) parts.Add("1541=" + DriveName);
            return string.Join(", ", parts) + " (" + Source + ")";
        }
    }

    public RomSet ToRomSet(bool includeDrive = true) =>
        new(Basic ?? throw new InvalidOperationException("no BASIC ROM"), Kernal!, Chargen!, includeDrive ? Drive : null, "browser", Description);

    #region localStorage

    public bool LoadFromStorage()
    {
        byte[]? Get(string slot, out string name)
        {
            name = C64Js.StorageGet(KeyPrefix + slot + ".name") ?? "";
            var b64 = C64Js.StorageGet(KeyPrefix + slot);
            if (string.IsNullOrEmpty(b64)) return null;
            try { return Convert.FromBase64String(b64); } catch { return null; }
        }
        var basic = Get("basic", out var bn);
        var kernal = Get("kernal", out var kn);
        var chargen = Get("chargen", out var cn);
        var drive = Get("drive", out var dn);
        if (basic?.Length != RomSet.BasicSize || kernal?.Length != RomSet.KernalSize || chargen?.Length != RomSet.CharSize)
            return false;
        Basic = basic; BasicName = bn;
        Kernal = kernal; KernalName = kn;
        Chargen = chargen; ChargenName = cn;
        if (drive?.Length == RomSet.DriveRomSize) { Drive = drive; DriveName = dn; }
        Source = "localStorage";
        return true;
    }

    /// <summary>Persists the current set (only complete sets are stored). Returns false when storage is unavailable/full.</summary>
    public bool SaveToStorage()
    {
        if (!IsComplete) return false;
        bool ok = true;
        void Put(string slot, byte[]? data, string name)
        {
            if (data is null) { C64Js.StorageRemove(KeyPrefix + slot); C64Js.StorageRemove(KeyPrefix + slot + ".name"); return; }
            ok &= C64Js.StorageSet(KeyPrefix + slot, Convert.ToBase64String(data));
            ok &= C64Js.StorageSet(KeyPrefix + slot + ".name", name);
        }
        Put("basic", Basic, BasicName);
        Put("kernal", Kernal, KernalName);
        Put("chargen", Chargen, ChargenName);
        Put("drive", Drive, DriveName);
        return ok;
    }

    public static void ClearStorage()
    {
        foreach (var slot in new[] { "basic", "kernal", "chargen", "drive" })
        {
            C64Js.StorageRemove(KeyPrefix + slot);
            C64Js.StorageRemove(KeyPrefix + slot + ".name");
        }
    }

    #endregion

    #region site

    /// <summary>Fetches roms/*.bin relative to the site root. Returns true when a complete set was found.</summary>
    public async Task<bool> LoadFromSiteAsync(HttpClient http, CancellationToken ct = default)
    {
        async Task<byte[]?> Fetch(string name, int size)
        {
            try
            {
                using var response = await http.GetAsync("roms/" + name, ct);
                if (!response.IsSuccessStatusCode) return null;
                var data = await response.Content.ReadAsByteArrayAsync(ct);
                return data.Length == size ? data : null;
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

        var basic = await Fetch("basic.bin", RomSet.BasicSize);
        var kernal = await Fetch("kernal.bin", RomSet.KernalSize);
        var chargen = await Fetch("chargen.bin", RomSet.CharSize);
        if (basic is null || kernal is null || chargen is null) return false;
        Basic = basic; BasicName = "basic.bin";
        Kernal = kernal; KernalName = "kernal.bin";
        Chargen = chargen; ChargenName = "chargen.bin";
        var drive = await Fetch("1541.bin", RomSet.DriveRomSize);
        if (drive is not null) { Drive = drive; DriveName = "1541.bin"; }
        else { Drive = null; DriveName = ""; }
        Source = "site";
        return true;
    }

    #endregion

    #region uploads

    /// <summary>
    /// Accepts an uploaded file for a slot. Sizes are validated (BASIC/KERNAL 8192, character 4096, 1541
    /// 16384 or two 8192 halves, combined 16384 = BASIC + KERNAL). Returns null on success, else an error text.
    /// </summary>
    public string? Accept(RomSlot slot, string fileName, byte[] data)
    {
        switch (slot)
        {
            case RomSlot.Basic:
                if (data.Length != RomSet.BasicSize) return $"{fileName}: BASIC ROM must be {RomSet.BasicSize} bytes (got {data.Length})";
                Basic = data; BasicName = fileName; break;
            case RomSlot.Kernal:
                if (data.Length != RomSet.KernalSize) return $"{fileName}: KERNAL ROM must be {RomSet.KernalSize} bytes (got {data.Length})";
                Kernal = data; KernalName = fileName; break;
            case RomSlot.Chargen:
                if (data.Length != RomSet.CharSize) return $"{fileName}: character ROM must be {RomSet.CharSize} bytes (got {data.Length})";
                Chargen = data; ChargenName = fileName; break;
            case RomSlot.Combined:
                if (data.Length != RomSet.BasicSize + RomSet.KernalSize) return $"{fileName}: combined BASIC+KERNAL image must be {RomSet.BasicSize + RomSet.KernalSize} bytes (got {data.Length})";
                Basic = data[..RomSet.BasicSize]; BasicName = fileName + "[0..8K]";
                Kernal = data[RomSet.BasicSize..]; KernalName = fileName + "[8K..16K]";
                break;
            case RomSlot.Drive:
                if (data.Length == RomSet.DriveRomSize)
                {
                    Drive = data; DriveName = fileName; _driveLow = _driveHigh = null;
                }
                else if (data.Length == 8192)
                {
                    // split 1541 ROM: the $E000 half ends with the CPU vectors (high bytes >= $C0)
                    bool high = data[0x1FFB] >= 0xC0 && data[0x1FFD] >= 0xC0 && data[0x1FFF] >= 0xC0;
                    if (high) _driveHigh = data; else _driveLow = data;
                    if (_driveLow is not null && _driveHigh is not null)
                    {
                        Drive = new byte[RomSet.DriveRomSize];
                        _driveLow.CopyTo(Drive, 0);
                        _driveHigh.CopyTo(Drive, 8192);
                        DriveName = fileName + "+" + (high ? "low" : "high");
                        _driveLow = _driveHigh = null;
                    }
                    else
                    {
                        return null; // waiting for the other half; not an error
                    }
                }
                else return $"{fileName}: 1541 ROM must be {RomSet.DriveRomSize} bytes or two 8192 byte halves (got {data.Length})";
                break;
        }
        Source = Source == "upload" || Source == "" ? "upload" : Source + "+upload";
        return null;
    }

    /// <summary>True while one half of a split 1541 ROM has been received and the other is still missing.</summary>
    public bool DriveHalfPending => (_driveLow is null) != (_driveHigh is null);

    public void RemoveDrive()
    {
        Drive = null;
        DriveName = "";
        _driveLow = _driveHigh = null;
    }

    #endregion

    private static bool Contains(byte[] haystack, ReadOnlySpan<byte> needle) => haystack.AsSpan().IndexOf(needle) >= 0;
}
