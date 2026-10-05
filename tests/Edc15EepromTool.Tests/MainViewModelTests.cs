using System.IO;
using Edc15EepromTool.Services;
using Edc15EepromTool.ViewModels;
using Xunit;

namespace Edc15EepromTool.Tests;

/// <summary>Drives the window logic the way the user does: type, toggle, save, and look at what the window would show.</summary>
public sealed class MainViewModelTests : IDisposable
{
    private sealed class FakeShell : IShellService
    {
        public string? FileToOpen { get; set; }
        public string? FileToSave { get; set; }
        public string? SuggestedName { get; private set; }
        public string? Copied { get; private set; }

        public string? PickFileToOpen() => FileToOpen;

        public string? PickFileToSave(string suggestedName, string? folder)
        {
            SuggestedName = suggestedName;
            return FileToSave;
        }

        public void OpenUrl(string url)
        {
        }

        public bool CopyText(string text)
        {
            Copied = text;
            return true;
        }
    }

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "edc15-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeShell _shell = new();

    public MainViewModelTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static string DumpPath(string name) => Path.Combine(AppContext.BaseDirectory, "Dumps", $"Audi EEPROM {name}.bin");

    private MainViewModel Open(string dump = "IMMO ON")
    {
        var viewModel = new MainViewModel(_shell);
        viewModel.Load(DumpPath(dump));
        return viewModel;
    }

    [Fact]
    public void Opening_a_dump_shows_its_values()
    {
        var vm = Open();

        Assert.True(vm.HasFile);
        Assert.Equal("Audi EEPROM IMMO ON.bin", vm.FileName);
        Assert.True(vm.IsImmoOn);
        Assert.Equal("07832", vm.LoginCodeText);
        Assert.Equal("169724", vm.MileageText);              // 169,723.8 km, rounded for the input
        Assert.Equal("169,723.8 km", vm.MileageOriginalText);
        Assert.Equal("WAUZZZ4B42N149060", vm.Vin);
        Assert.False(vm.HasWarnings);
        Assert.False(vm.IsModified);
        Assert.Equal("No changes", vm.StatusText);
    }

    [Fact]
    public void Changing_a_value_and_changing_it_back_leaves_the_file_untouched()
    {
        var vm = Open();

        vm.MileageText = "150000";
        Assert.True(vm.IsMileageModified);
        Assert.Equal("1 unsaved change", vm.StatusText);

        vm.MileageText = "169724";                            // the value that was shown
        Assert.False(vm.IsMileageModified);
        Assert.False(vm.IsModified);                          // the .8 km of the original is still there
    }

    [Fact]
    public void Switching_the_immobilizer_marks_it_changed_and_switching_back_clears_it()
    {
        var vm = Open();

        vm.IsImmoOn = false;
        Assert.True(vm.IsImmoModified);
        Assert.Equal("Immobilizer off", vm.ImmoStatusText);

        vm.IsImmoOn = true;
        Assert.False(vm.IsImmoModified);
    }

    [Theory]
    [InlineData("123")]        // too short
    [InlineData("12345")]      // does not start with 0
    [InlineData("0123")]       // too short
    [InlineData("")]
    public void A_login_code_must_be_five_digits_starting_with_zero(string text)
    {
        var vm = Open();

        vm.LoginCodeText = text;

        Assert.NotNull(vm.LoginCodeError);
        Assert.True(vm.HasErrors);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.False(vm.IsLoginCodeModified);                 // an invalid entry is never written
    }

    [Fact]
    public void A_valid_login_code_is_accepted()
    {
        var vm = Open();

        vm.LoginCodeText = "01234";

        Assert.Null(vm.LoginCodeError);
        Assert.True(vm.IsLoginCodeModified);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("9999999")]    // more than the 28-bit field can hold
    public void A_mileage_must_be_in_range(string text)
    {
        var vm = Open();

        vm.MileageText = text;

        Assert.NotNull(vm.MileageError);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.True(vm.CanResetMileage);
    }

    [Fact]
    public void Undo_all_clears_changes_and_errors()
    {
        var vm = Open();
        vm.IsImmoOn = false;
        vm.MileageText = "100";
        vm.LoginCodeText = "12";
        Assert.True(vm.RevertCommand.CanExecute(null));

        vm.RevertCommand.Execute(null);

        Assert.False(vm.IsModified);
        Assert.False(vm.HasErrors);
        Assert.True(vm.IsImmoOn);
        Assert.Equal("07832", vm.LoginCodeText);
        Assert.Equal("169724", vm.MileageText);
    }

    [Fact]
    public void Restoring_one_value_leaves_the_others_changed()
    {
        var vm = Open();
        vm.IsImmoOn = false;
        vm.MileageText = "100";

        vm.ResetMileageCommand.Execute(null);

        Assert.False(vm.IsMileageModified);
        Assert.True(vm.IsImmoModified);
        Assert.Equal("169724", vm.MileageText);
    }

    [Fact]
    public void Saving_writes_a_new_file_and_leaves_the_original_alone()
    {
        var vm = Open();
        var original = File.ReadAllBytes(DumpPath("IMMO ON"));
        vm.IsImmoOn = false;
        _shell.FileToSave = Path.Combine(_folder, "result.bin");

        vm.SaveCommand.Execute(null);

        Assert.Equal("Audi EEPROM IMMO ON_modified.bin", _shell.SuggestedName);
        Assert.Equal(File.ReadAllBytes(DumpPath("IMMO OFF")), File.ReadAllBytes(_shell.FileToSave));
        Assert.Equal(original, File.ReadAllBytes(DumpPath("IMMO ON")));
        Assert.False(vm.IsModified);
        Assert.Equal("result.bin", vm.FileName);
    }

    [Fact]
    public void A_saved_file_keeps_its_name_when_it_is_saved_again()
    {
        var vm = Open();
        vm.IsImmoOn = false;
        _shell.FileToSave = Path.Combine(_folder, "car_modified.bin");
        vm.SaveCommand.Execute(null);

        vm.IsImmoOn = true;
        vm.SaveCommand.Execute(null);

        Assert.Equal("car_modified.bin", _shell.SuggestedName);
    }

    [Fact]
    public void Cancelling_the_save_dialog_changes_nothing()
    {
        var vm = Open();
        vm.IsImmoOn = false;
        _shell.FileToSave = null;

        vm.SaveCommand.Execute(null);

        Assert.True(vm.IsModified);
        Assert.Equal("Audi EEPROM IMMO ON.bin", vm.FileName);
    }

    [Fact]
    public void A_file_of_the_wrong_size_is_refused_and_the_open_file_stays()
    {
        var vm = Open();
        var wrong = Path.Combine(_folder, "flash.bin");
        File.WriteAllBytes(wrong, new byte[1024]);

        vm.Load(wrong);

        Assert.True(vm.IsToastVisible);
        Assert.Equal(ToastKind.Error, vm.ToastKind);
        Assert.Equal("Audi EEPROM IMMO ON.bin", vm.FileName);
    }

    [Fact]
    public void A_missing_file_is_reported_instead_of_crashing()
    {
        var vm = new MainViewModel(_shell);

        vm.Load(Path.Combine(_folder, "does-not-exist.bin"));

        Assert.False(vm.HasFile);
        Assert.Equal(ToastKind.Error, vm.ToastKind);
    }

    [Fact]
    public void Opening_another_file_asks_before_discarding_changes()
    {
        var vm = Open();
        vm.IsImmoOn = false;

        vm.OpenDropped(DumpPath("ODO 0"));

        Assert.True(vm.IsConfirmationOpen);
        Assert.Equal("Audi EEPROM IMMO ON.bin", vm.FileName);     // not loaded yet
        Assert.False(vm.OpenCommand.CanExecute(null));            // the dialog is modal

        vm.ConfirmCommand.Execute(null);

        Assert.False(vm.IsConfirmationOpen);
        Assert.Equal("Audi EEPROM ODO 0.bin", vm.FileName);
        Assert.False(vm.IsModified);
    }

    [Fact]
    public void Cancelling_the_question_keeps_the_changes()
    {
        var vm = Open();
        vm.IsImmoOn = false;
        vm.OpenDropped(DumpPath("ODO 0"));

        vm.CloseOverlayCommand.Execute(null);

        Assert.False(vm.IsConfirmationOpen);
        Assert.True(vm.IsImmoModified);
        Assert.Equal("Audi EEPROM IMMO ON.bin", vm.FileName);
    }

    [Fact]
    public void Closing_asks_only_when_there_are_unsaved_changes()
    {
        var vm = Open();
        Assert.True(vm.RequestClose(() => { }));

        vm.IsImmoOn = false;
        var closed = false;
        Assert.False(vm.RequestClose(() => closed = true));
        Assert.True(vm.IsConfirmationOpen);

        vm.ConfirmCommand.Execute(null);
        Assert.True(closed);
    }

    [Fact]
    public void The_about_dialog_blocks_the_content()
    {
        var vm = Open();
        vm.IsImmoOn = false;
        Assert.True(vm.SaveCommand.CanExecute(null));

        vm.AboutCommand.Execute(null);

        Assert.True(vm.IsAboutOpen);
        Assert.False(vm.IsContentEnabled);
        Assert.False(vm.SaveCommand.CanExecute(null));

        vm.CloseOverlayCommand.Execute(null);
        Assert.True(vm.IsContentEnabled);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void About_cannot_open_on_top_of_a_question()
    {
        var vm = Open();
        vm.IsImmoOn = false;
        vm.OpenDropped(DumpPath("ODO 0"));

        Assert.True(vm.IsConfirmationOpen);
        Assert.False(vm.AboutCommand.CanExecute(null));
    }

    [Fact]
    public void The_login_code_can_be_copied()
    {
        var vm = Open();

        vm.CopyLoginCodeCommand.Execute(null);

        Assert.Equal("07832", _shell.Copied);
    }

    [Fact]
    public void Warnings_appear_when_the_copies_of_a_value_differ()
    {
        var bytes = File.ReadAllBytes(DumpPath("IMMO ON"));
        bytes[Core.Edc15Eeprom.LoginCodeMirrorOffset] ^= 0x01;
        var path = Path.Combine(_folder, "odd.bin");
        File.WriteAllBytes(path, bytes);

        var vm = new MainViewModel(_shell);
        vm.Load(path);

        Assert.True(vm.HasWarnings);
        Assert.Single(vm.Warnings);
    }
}
