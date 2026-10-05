using System.IO;
using System.Text;

namespace Edc15EepromTool.Core;

/// <summary>Immobilizer state as stored in the two immobilizer bytes of the dump.</summary>
public enum ImmoState
{
    /// <summary>The two bytes hold neither the ON nor the OFF value, or they disagree.</summary>
    Unknown,
    On,
    Off,
}

/// <summary>A value of the dump that the tool reads and writes.</summary>
public enum EepromField
{
    LoginCode,
    Immobilizer,
    Odometer,
}

public sealed class InvalidEepromException(string message) : Exception(message);

/// <summary>
/// A 512-byte 24C04 EEPROM dump of a Bosch EDC15 engine control unit.
/// Every value the tool edits is stored twice; both copies are always read and written.
/// The original bytes are kept so that changes can be shown and reverted.
/// </summary>
public sealed class Edc15Eeprom
{
    public const int Size = 512;

    public const int LoginCodeOffset = 0x012E;
    public const int LoginCodeMirrorOffset = 0x0160;
    public const int ImmobilizerIdOffset = 0x0131;
    public const int ImmobilizerIdLength = 14;
    public const int VinOffset = 0x0140;
    public const int VinLength = 17;
    public const int ImmoOffset = 0x01B0;
    public const int ImmoMirrorOffset = 0x01DE;
    public const int OdometerOffset = 0x01BF;
    public const int OdometerMirrorOffset = 0x01ED;

    public const byte ImmoOnValue = 0x73;
    public const byte ImmoOffValue = 0x60;

    /// <summary>The odometer counts in steps of 10 m, so the stored value is the mileage in km times 100.</summary>
    public const uint OdometerScale = 100;

    /// <summary>
    /// The odometer is a 28-bit value: the three low bytes plus the low nibble of the fourth byte.
    /// The high nibble of the fourth byte belongs to another field and must be kept.
    /// </summary>
    private const uint OdometerRawMask = 0x0FFF_FFFF;

    /// <summary>Largest mileage the tool accepts; the 28-bit field cannot hold more.</summary>
    public const uint MaxOdometerKm = OdometerRawMask / OdometerScale;

    /// <summary>The login code is the 4-digit immobilizer PIN; it is written with a leading zero as 0XXXX.</summary>
    public const ushort MaxLoginCode = 9_999;

    private static readonly (int Offset, int Length, EepromField Field)[] FieldRanges =
    [
        (LoginCodeOffset, 2, EepromField.LoginCode),
        (LoginCodeMirrorOffset, 2, EepromField.LoginCode),
        (ImmoOffset, 1, EepromField.Immobilizer),
        (ImmoMirrorOffset, 1, EepromField.Immobilizer),
        (OdometerOffset, 4, EepromField.Odometer),
        (OdometerMirrorOffset, 4, EepromField.Odometer),
    ];

    private readonly byte[] _original;
    private readonly byte[] _data;

    private Edc15Eeprom(byte[] bytes)
    {
        _original = (byte[])bytes.Clone();
        _data = (byte[])bytes.Clone();
    }

    public static Edc15Eeprom FromBytes(byte[] bytes)
    {
        if (bytes is null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }
        if (bytes.Length != Size)
        {
            throw new InvalidEepromException(
                $"The file is {bytes.Length:N0} bytes. A Bosch EDC15 24C04 EEPROM dump is exactly {Size} bytes.");
        }
        return new Edc15Eeprom(bytes);
    }

    public static Edc15Eeprom Load(string path)
    {
        var length = new FileInfo(path).Length;
        if (length != Size)
        {
            throw new InvalidEepromException(
                $"The file is {length:N0} bytes. A Bosch EDC15 24C04 EEPROM dump is exactly {Size} bytes.");
        }
        return FromBytes(File.ReadAllBytes(path));
    }

    public byte[] ToArray() => (byte[])_data.Clone();

    public bool IsModified => !_data.SequenceEqual(_original);

    public bool IsFieldModified(EepromField field) =>
        FieldRanges.Where(r => r.Field == field)
                   .Any(r => !RangeEquals(_data, r.Offset, _original, r.Offset, r.Length));

    /// <summary>Discards every change.</summary>
    public void RevertAll() => _original.CopyTo(_data, 0);

    /// <summary>Makes the current bytes the new original, for example after they were saved.</summary>
    public void AcceptChanges() => _data.CopyTo(_original, 0);

    // ---------------------------------------------------------------- immobilizer

    public ImmoState Immo => ReadImmo(_data);

    public ImmoState OriginalImmo => ReadImmo(_original);

    public void SetImmo(bool on)
    {
        var value = on ? ImmoOnValue : ImmoOffValue;
        _data[ImmoOffset] = value;
        _data[ImmoMirrorOffset] = value;
    }

    public void RestoreImmo()
    {
        Restore(ImmoOffset, 1);
        Restore(ImmoMirrorOffset, 1);
    }

    private static ImmoState ReadImmo(byte[] data) => (data[ImmoOffset], data[ImmoMirrorOffset]) switch
    {
        (ImmoOnValue, ImmoOnValue) => ImmoState.On,
        (ImmoOffValue, ImmoOffValue) => ImmoState.Off,
        _ => ImmoState.Unknown,
    };

    // ---------------------------------------------------------------- odometer

    public uint OdometerRaw => ReadOdometer(_data, OdometerOffset);

    public uint OriginalOdometerRaw => ReadOdometer(_original, OdometerOffset);

    /// <summary>Compares only the 28 odometer bits, so a difference in the reserved nibble does not count.</summary>
    public bool OriginalOdometerCopiesMatch => ReadOdometer(_original, OdometerOffset) == ReadOdometer(_original, OdometerMirrorOffset);

    public static decimal RawToKm(uint raw) => raw / (decimal)OdometerScale;

    public void SetOdometerKm(uint km)
    {
        if (km > MaxOdometerKm)
        {
            throw new ArgumentOutOfRangeException(nameof(km), km, $"The mileage must be {MaxOdometerKm} km or less.");
        }
        var raw = km * OdometerScale;
        WriteOdometer(OdometerOffset, raw);
        WriteOdometer(OdometerMirrorOffset, raw);
    }

    private static uint ReadOdometer(byte[] data, int offset) => ReadUInt32(data, offset) & OdometerRawMask;

    private void WriteOdometer(int offset, uint raw)
    {
        // keep the high nibble of the fourth byte, which does not belong to the odometer
        var reserved = ReadUInt32(_data, offset) & ~OdometerRawMask;
        WriteUInt32(_data, offset, reserved | (raw & OdometerRawMask));
    }

    public void RestoreOdometer()
    {
        Restore(OdometerOffset, 4);
        Restore(OdometerMirrorOffset, 4);
    }

    // ---------------------------------------------------------------- login code

    public ushort LoginCode => ReadUInt16(_data, LoginCodeOffset);

    public ushort OriginalLoginCode => ReadUInt16(_original, LoginCodeOffset);

    public bool OriginalLoginCodeCopiesMatch => RangeEquals(_original, LoginCodeOffset, _original, LoginCodeMirrorOffset, 2);

    public void SetLoginCode(ushort code)
    {
        if (code > MaxLoginCode)
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, $"The login code must be {MaxLoginCode} or less.");
        }
        WriteUInt16(_data, LoginCodeOffset, code);
        WriteUInt16(_data, LoginCodeMirrorOffset, code);
    }

    public void RestoreLoginCode()
    {
        Restore(LoginCodeOffset, 2);
        Restore(LoginCodeMirrorOffset, 2);
    }

    // ---------------------------------------------------------------- vehicle data (read only)

    /// <summary>The VIN, or <c>null</c> when the area does not hold 17 VIN characters.</summary>
    public string? Vin => ReadText(VinOffset, VinLength);

    /// <summary>The immobilizer ID, or <c>null</c> when the area does not hold 14 ID characters.</summary>
    public string? ImmobilizerId => ReadText(ImmobilizerIdOffset, ImmobilizerIdLength);

    private string? ReadText(int offset, int length)
    {
        for (var i = offset; i < offset + length; i++)
        {
            var b = _original[i];
            if (!(b is >= (byte)'0' and <= (byte)'9' || b is >= (byte)'A' and <= (byte)'Z'))
            {
                return null;
            }
        }
        return Encoding.ASCII.GetString(_original, offset, length);
    }

    private void Restore(int offset, int length) => Array.Copy(_original, offset, _data, offset, length);

    // ---------------------------------------------------------------- little-endian helpers

    private static ushort ReadUInt16(byte[] data, int offset) => (ushort)(data[offset] | data[offset + 1] << 8);

    private static void WriteUInt16(byte[] data, int offset, ushort value)
    {
        data[offset] = (byte)value;
        data[offset + 1] = (byte)(value >> 8);
    }

    private static uint ReadUInt32(byte[] data, int offset) =>
        (uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24);

    private static void WriteUInt32(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)value;
        data[offset + 1] = (byte)(value >> 8);
        data[offset + 2] = (byte)(value >> 16);
        data[offset + 3] = (byte)(value >> 24);
    }

    private static bool RangeEquals(byte[] a, int aOffset, byte[] b, int bOffset, int length)
    {
        for (var i = 0; i < length; i++)
        {
            if (a[aOffset + i] != b[bOffset + i])
            {
                return false;
            }
        }
        return true;
    }
}
