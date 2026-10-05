using System.IO;
using Edc15EepromTool.Core;
using Xunit;

namespace Edc15EepromTool.Tests;

/// <summary>
/// The example dumps differ from each other only in the immobilizer and odometer bytes, so editing one of them
/// must produce the other byte for byte.
/// </summary>
public class Edc15EepromTests
{
    private static byte[] Dump(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Dumps", $"Audi EEPROM {name}.bin"));

    [Fact]
    public void Reads_the_immobilizer_state()
    {
        Assert.Equal(ImmoState.On, Edc15Eeprom.FromBytes(Dump("IMMO ON")).Immo);
        Assert.Equal(ImmoState.Off, Edc15Eeprom.FromBytes(Dump("IMMO OFF")).Immo);
    }

    [Fact]
    public void Switching_the_immobilizer_off_gives_the_IMMO_OFF_dump()
    {
        var eeprom = Edc15Eeprom.FromBytes(Dump("IMMO ON"));
        eeprom.SetImmo(false);
        Assert.Equal(Dump("IMMO OFF"), eeprom.ToArray());
    }

    [Fact]
    public void Switching_the_immobilizer_on_gives_the_IMMO_ON_dump()
    {
        var eeprom = Edc15Eeprom.FromBytes(Dump("IMMO OFF"));
        eeprom.SetImmo(true);
        Assert.Equal(Dump("IMMO ON"), eeprom.ToArray());
    }

    [Theory]
    [InlineData("ODO 0", 0u)]
    [InlineData("ODO 169341", 16_934_100u)]
    public void Reads_the_odometer(string dump, uint raw)
    {
        var eeprom = Edc15Eeprom.FromBytes(Dump(dump));
        Assert.Equal(raw, eeprom.OdometerRaw);
        Assert.True(eeprom.OriginalOdometerCopiesMatch);
    }

    [Fact]
    public void Setting_the_mileage_writes_both_copies()
    {
        var eeprom = Edc15Eeprom.FromBytes(Dump("ODO 169341"));
        eeprom.SetOdometerKm(0);
        Assert.Equal(Dump("ODO 0"), eeprom.ToArray());

        eeprom = Edc15Eeprom.FromBytes(Dump("ODO 0"));
        eeprom.SetOdometerKm(169_341);
        Assert.Equal(Dump("ODO 169341"), eeprom.ToArray());
    }

    [Fact]
    public void Rejects_a_mileage_above_the_limit()
    {
        var eeprom = Edc15Eeprom.FromBytes(Dump("ODO 0"));
        Assert.Throws<ArgumentOutOfRangeException>(() => eeprom.SetOdometerKm(Edc15Eeprom.MaxOdometerKm + 1));
    }

    [Fact]
    public void Writing_the_mileage_keeps_the_reserved_high_nibble()
    {
        var bytes = Dump("ODO 0");
        // the high nibble of the fourth odometer byte belongs to another field
        const int topByte = Edc15Eeprom.OdometerOffset + 3;
        bytes[topByte] = 0xA0;
        bytes[Edc15Eeprom.OdometerMirrorOffset + 3] = 0xA0;

        var eeprom = Edc15Eeprom.FromBytes(bytes);
        Assert.Equal(0u, eeprom.OdometerRaw);     // the reserved nibble is not read as mileage

        eeprom.SetOdometerKm(123_456);
        var result = eeprom.ToArray();
        Assert.Equal(0xA0, result[topByte] & 0xF0);                     // reserved nibble kept
        Assert.Equal(123_456u, Edc15Eeprom.FromBytes(result).OdometerRaw / Edc15Eeprom.OdometerScale);
    }

    [Fact]
    public void Reads_and_writes_the_login_code_in_both_copies()
    {
        var eeprom = Edc15Eeprom.FromBytes(Dump("IMMO ON"));
        Assert.Equal(7832, eeprom.LoginCode);
        Assert.True(eeprom.OriginalLoginCodeCopiesMatch);

        eeprom.SetLoginCode(1234);
        var bytes = eeprom.ToArray();
        Assert.Equal(new byte[] { 0xD2, 0x04 }, bytes.Skip(Edc15Eeprom.LoginCodeOffset).Take(2).ToArray());
        Assert.Equal(new byte[] { 0xD2, 0x04 }, bytes.Skip(Edc15Eeprom.LoginCodeMirrorOffset).Take(2).ToArray());

        // exactly the four login code bytes differ from the original
        var original = Dump("IMMO ON");
        Assert.Equal(4, Enumerable.Range(0, Edc15Eeprom.Size).Count(i => bytes[i] != original[i]));
    }

    [Fact]
    public void Rejects_a_login_code_above_09999()
    {
        var eeprom = Edc15Eeprom.FromBytes(Dump("IMMO ON"));
        eeprom.SetLoginCode(Edc15Eeprom.MaxLoginCode);
        Assert.Throws<ArgumentOutOfRangeException>(() => eeprom.SetLoginCode(Edc15Eeprom.MaxLoginCode + 1));
    }

    [Fact]
    public void Reads_the_vehicle_data()
    {
        var eeprom = Edc15Eeprom.FromBytes(Dump("IMMO ON"));
        Assert.Equal("WAUZZZ4B42N149060", eeprom.Vin);
        Assert.Equal("AUZ7Z0B2122952", eeprom.ImmobilizerId);
    }

    [Fact]
    public void Restoring_a_field_brings_back_the_original_bytes()
    {
        var original = Dump("IMMO ON");
        var eeprom = Edc15Eeprom.FromBytes(original);
        eeprom.SetImmo(false);
        eeprom.SetOdometerKm(1);
        eeprom.SetLoginCode(1);
        Assert.True(eeprom.IsModified);
        Assert.True(eeprom.IsFieldModified(EepromField.Odometer));

        eeprom.RestoreImmo();
        eeprom.RestoreOdometer();
        eeprom.RestoreLoginCode();
        Assert.False(eeprom.IsModified);
        Assert.Equal(original, eeprom.ToArray());
    }

    [Fact]
    public void Accepting_changes_makes_them_the_new_original()
    {
        var eeprom = Edc15Eeprom.FromBytes(Dump("IMMO ON"));
        eeprom.SetImmo(false);
        eeprom.AcceptChanges();
        Assert.False(eeprom.IsModified);
        Assert.Equal(ImmoState.Off, eeprom.OriginalImmo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(511)]
    [InlineData(513)]
    [InlineData(4096)]
    public void Rejects_files_that_are_not_512_bytes(int length)
    {
        Assert.Throws<InvalidEepromException>(() => Edc15Eeprom.FromBytes(new byte[length]));
    }

    [Fact]
    public void Unknown_immobilizer_bytes_are_reported_as_unknown()
    {
        var bytes = Dump("IMMO ON");
        bytes[Edc15Eeprom.ImmoMirrorOffset] = Edc15Eeprom.ImmoOffValue;
        Assert.Equal(ImmoState.Unknown, Edc15Eeprom.FromBytes(bytes).Immo);
    }
}
