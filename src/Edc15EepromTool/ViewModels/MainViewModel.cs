using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Threading;
using Edc15EepromTool.Core;
using Edc15EepromTool.Services;

namespace Edc15EepromTool.ViewModels;

public enum ToastKind { Info, Success, Error }

/// <summary>A question shown in the in-app dialog before something is discarded.</summary>
public sealed record ConfirmRequest(string Title, string Message, string ConfirmText, Action OnConfirm);

public sealed class MainViewModel : ObservableObject
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly IShellService _shell;
    private readonly DispatcherTimer _toastTimer;

    private Edc15Eeprom? _eeprom;
    private string? _filePath;
    private bool _syncing;
    private bool _immoTouched;
    private uint _originalMileageKm;

    private bool _isImmoOn;
    private string _mileageText = string.Empty;
    private string? _mileageError;
    private string _loginCodeText = string.Empty;
    private string? _loginCodeError;
    private int _pendingChanges;
    private bool _isAboutOpen;
    private ConfirmRequest? _confirmation;
    private string? _toastMessage;
    private ToastKind _toastKind;
    private bool _isToastVisible;

    public MainViewModel(IShellService shell)
    {
        _shell = shell;
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            IsToastVisible = false;
        };

        OpenCommand = new RelayCommand(Open, () => !IsOverlayOpen);
        SaveCommand = new RelayCommand(Save, () => HasFile && !HasErrors && !IsOverlayOpen);
        RevertCommand = new RelayCommand(Revert, () => IsModified || HasErrors);
        ResetImmoCommand = new RelayCommand(ResetImmo);
        ResetMileageCommand = new RelayCommand(ResetMileage);
        ResetLoginCodeCommand = new RelayCommand(ResetLoginCode);
        CopyLoginCodeCommand = new RelayCommand(CopyLoginCode, () => HasFile && LoginCodeError is null);
        AboutCommand = new RelayCommand(() => IsAboutOpen = true, () => !IsOverlayOpen);
        CloseOverlayCommand = new RelayCommand(() =>
        {
            IsAboutOpen = false;
            Confirmation = null;
        });
        OpenLinkCommand = new RelayCommand(p => Run(() => _shell.OpenUrl((string)p!)));
        ConfirmCommand = new RelayCommand(() =>
        {
            var request = Confirmation;
            Confirmation = null;
            request?.OnConfirm();
        });
        DismissToastCommand = new RelayCommand(() => IsToastVisible = false);
    }

    // ---------------------------------------------------------------- commands

    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand RevertCommand { get; }
    public ICommand ResetImmoCommand { get; }
    public ICommand ResetMileageCommand { get; }
    public ICommand ResetLoginCodeCommand { get; }
    public ICommand CopyLoginCodeCommand { get; }
    public ICommand AboutCommand { get; }
    public ICommand CloseOverlayCommand { get; }
    public ICommand OpenLinkCommand { get; }
    public ICommand ConfirmCommand { get; }
    public ICommand DismissToastCommand { get; }

    // ---------------------------------------------------------------- file and vehicle

    public string AppVersion { get; } = "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.1.0");

    public bool HasFile => _eeprom is not null;

    public string FileName => _filePath is null ? string.Empty : Path.GetFileName(_filePath);

    public string FilePath => _filePath ?? string.Empty;

    public string Vin => _eeprom?.Vin ?? "Not found";

    public string ImmobilizerId => _eeprom?.ImmobilizerId ?? "Not found";

    /// <summary>Problems found in the file; empty for a healthy dump.</summary>
    public ObservableCollection<string> Warnings { get; } = [];

    public bool HasWarnings => Warnings.Count > 0;

    public string WarningSummary => string.Join(Environment.NewLine + Environment.NewLine, Warnings);

    // ---------------------------------------------------------------- immobilizer

    public bool IsImmoOn
    {
        get => _isImmoOn;
        set
        {
            if (SetProperty(ref _isImmoOn, value) && !_syncing)
            {
                _immoTouched = true;
                ApplyImmo();
            }
        }
    }

    public string ImmoStatusText => !HasFile ? "No file open" : IsImmoOn ? "Immobilizer on" : "Immobilizer off";

    public string ImmoOriginalText => _eeprom is null ? "—" : Describe(_eeprom.OriginalImmo);

    public bool IsImmoModified => _eeprom?.IsFieldModified(EepromField.Immobilizer) == true;

    // ---------------------------------------------------------------- mileage

    public string MileageText
    {
        get => _mileageText;
        set
        {
            if (SetProperty(ref _mileageText, value) && !_syncing)
            {
                ApplyMileage();
            }
        }
    }

    public string? MileageError
    {
        get => _mileageError;
        private set
        {
            if (SetProperty(ref _mileageError, value))
            {
                OnPropertyChanged(nameof(HasErrors));
            }
        }
    }

    public string MileageOriginalText =>
        _eeprom is null ? "—" : Edc15Eeprom.RawToKm(_eeprom.OriginalOdometerRaw).ToString("#,##0.##", Invariant) + " km";

    public bool IsMileageModified => _eeprom?.IsFieldModified(EepromField.Odometer) == true;

    public bool CanResetMileage => IsMileageModified || MileageError is not null;

    // ---------------------------------------------------------------- login code

    public string LoginCodeText
    {
        get => _loginCodeText;
        set
        {
            if (SetProperty(ref _loginCodeText, value) && !_syncing)
            {
                ApplyLoginCode();
            }
        }
    }

    public string? LoginCodeError
    {
        get => _loginCodeError;
        private set
        {
            if (SetProperty(ref _loginCodeError, value))
            {
                OnPropertyChanged(nameof(HasErrors));
            }
        }
    }

    public string LoginCodeOriginalText => _eeprom is null ? "—" : _eeprom.OriginalLoginCode.ToString("D5", Invariant);

    public bool IsLoginCodeModified => _eeprom?.IsFieldModified(EepromField.LoginCode) == true;

    public bool CanResetLoginCode => IsLoginCodeModified || LoginCodeError is not null;

    // ---------------------------------------------------------------- status

    public bool HasErrors => MileageError is not null || LoginCodeError is not null;

    public bool IsModified => _eeprom?.IsModified == true;

    public int PendingChanges { get => _pendingChanges; private set => SetProperty(ref _pendingChanges, value); }

    public string StatusText => !HasFile
        ? "No file open"
        : HasErrors
            ? "Correct the highlighted value to save"
            : PendingChanges switch
            {
                0 => "No changes",
                1 => "1 unsaved change",
                var n => $"{n} unsaved changes",
            };

    public bool IsAboutOpen
    {
        get => _isAboutOpen;
        set
        {
            if (SetProperty(ref _isAboutOpen, value))
            {
                OnOverlayChanged();
            }
        }
    }

    public ConfirmRequest? Confirmation
    {
        get => _confirmation;
        private set
        {
            if (SetProperty(ref _confirmation, value))
            {
                OnPropertyChanged(nameof(IsConfirmationOpen));
                OnOverlayChanged();
            }
        }
    }

    public bool IsConfirmationOpen => Confirmation is not null;

    /// <summary>A dialog is shown on top of the window; the content behind it is disabled.</summary>
    public bool IsOverlayOpen => IsAboutOpen || IsConfirmationOpen;

    public bool IsContentEnabled => !IsOverlayOpen;

    private void OnOverlayChanged()
    {
        OnPropertyChanged(nameof(IsOverlayOpen));
        OnPropertyChanged(nameof(IsContentEnabled));
        CommandManager.InvalidateRequerySuggested();
    }

    public string? ToastMessage { get => _toastMessage; private set => SetProperty(ref _toastMessage, value); }

    public ToastKind ToastKind { get => _toastKind; private set => SetProperty(ref _toastKind, value); }

    public bool IsToastVisible { get => _isToastVisible; private set => SetProperty(ref _isToastVisible, value); }

    // ---------------------------------------------------------------- opening and saving

    /// <summary>Opens a file that was dropped on the window or passed on the command line.</summary>
    public void OpenDropped(string path) => ConfirmDiscard(() => Load(path));

    /// <summary>Returns <c>true</c> when the window may close now; otherwise asks first and calls <paramref name="close"/> later.</summary>
    public bool RequestClose(Action close)
    {
        if (!IsModified)
        {
            return true;
        }
        Confirmation = new ConfirmRequest("Quit without saving?", $"Your changes to {FileName} have not been saved.", "Quit", close);
        return false;
    }

    private void Open() => ConfirmDiscard(() =>
    {
        var path = _shell.PickFileToOpen();
        if (path is not null)
        {
            Load(path);
        }
    });

    internal void Load(string path)
    {
        try
        {
            _eeprom = Edc15Eeprom.Load(path);
            _filePath = path;
            SyncFromModel();
            ShowToast($"Opened {FileName}", ToastKind.Info);
        }
        catch (InvalidEepromException ex)
        {
            ShowToast(ex.Message, ToastKind.Error);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowToast($"The file could not be read: {ex.Message}", ToastKind.Error);
        }
    }

    private void Save()
    {
        if (_eeprom is null || HasErrors)
        {
            return;
        }

        var name = Path.GetFileNameWithoutExtension(_filePath) ?? "EEPROM";
        var suggested = name.EndsWith("_modified", StringComparison.OrdinalIgnoreCase) ? name + ".bin" : name + "_modified.bin";
        var target = _shell.PickFileToSave(suggested, Path.GetDirectoryName(_filePath));
        if (target is null)
        {
            return;
        }

        try
        {
            File.WriteAllBytes(target, _eeprom.ToArray());
            _eeprom.AcceptChanges();
            _filePath = target;
            SyncFromModel();
            ShowToast($"Saved as {FileName}", ToastKind.Success);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowToast($"The file could not be saved: {ex.Message}", ToastKind.Error);
        }
    }

    private void Revert()
    {
        if (_eeprom is null)
        {
            return;
        }
        _eeprom.RevertAll();
        SyncFromModel();
        ShowToast("All changes were undone", ToastKind.Info);
    }

    private void CopyLoginCode()
    {
        if (_shell.CopyText(LoginCodeText))
        {
            ShowToast($"Login code {LoginCodeText} copied", ToastKind.Success);
        }
        else
        {
            ShowToast("The clipboard is in use by another application", ToastKind.Error);
        }
    }

    private void ConfirmDiscard(Action action)
    {
        if (IsModified)
        {
            Confirmation = new ConfirmRequest("Discard your changes?", $"Your changes to {FileName} have not been saved.", "Discard", action);
        }
        else
        {
            action();
        }
    }

    // ---------------------------------------------------------------- editing

    private void ApplyImmo()
    {
        if (_eeprom is null)
        {
            return;
        }

        var original = _eeprom.OriginalImmo;
        if (original != ImmoState.Unknown && IsImmoOn == (original == ImmoState.On))
        {
            _eeprom.RestoreImmo();
        }
        else if (_immoTouched)
        {
            _eeprom.SetImmo(IsImmoOn);
        }
        Refresh();
    }

    private void ApplyMileage()
    {
        if (_eeprom is null)
        {
            return;
        }

        if (MileageText.Length == 0)
        {
            MileageError = "Enter the mileage in km.";
            _eeprom.RestoreOdometer();
        }
        else if (!uint.TryParse(MileageText, NumberStyles.None, Invariant, out var km) || km > Edc15Eeprom.MaxOdometerKm)
        {
            MileageError = $"The highest mileage is {Edc15Eeprom.MaxOdometerKm.ToString("N0", Invariant)} km.";
            _eeprom.RestoreOdometer();
        }
        else
        {
            MileageError = null;
            if (km == _originalMileageKm)
            {
                _eeprom.RestoreOdometer();
            }
            else
            {
                _eeprom.SetOdometerKm(km);
            }
        }
        Refresh();
    }

    private void ApplyLoginCode()
    {
        if (_eeprom is null)
        {
            return;
        }

        if (LoginCodeText.Length != 5 || LoginCodeText[0] != '0'
            || !ushort.TryParse(LoginCodeText, NumberStyles.None, Invariant, out var code) || code > Edc15Eeprom.MaxLoginCode)
        {
            LoginCodeError = "5 digits, starting with 0";
            _eeprom.RestoreLoginCode();
        }
        else
        {
            LoginCodeError = null;
            if (code == _eeprom.OriginalLoginCode)
            {
                _eeprom.RestoreLoginCode();
            }
            else
            {
                _eeprom.SetLoginCode(code);
            }
        }
        Refresh();
    }

    private void ResetImmo()
    {
        if (_eeprom is null)
        {
            return;
        }
        _eeprom.RestoreImmo();
        WhileSyncing(() =>
        {
            _immoTouched = false;
            IsImmoOn = _eeprom.Immo == ImmoState.On;
        });
        Refresh();
    }

    private void ResetMileage()
    {
        if (_eeprom is null)
        {
            return;
        }
        _eeprom.RestoreOdometer();
        WhileSyncing(() =>
        {
            MileageText = _originalMileageKm.ToString(Invariant);
            MileageError = null;
        });
        Refresh();
    }

    private void ResetLoginCode()
    {
        if (_eeprom is null)
        {
            return;
        }
        _eeprom.RestoreLoginCode();
        WhileSyncing(() =>
        {
            LoginCodeText = _eeprom.OriginalLoginCode.ToString("D5", Invariant);
            LoginCodeError = null;
        });
        Refresh();
    }

    /// <summary>Shows the values of the file as it is now and resets all inputs.</summary>
    private void SyncFromModel()
    {
        if (_eeprom is null)
        {
            return;
        }

        _originalMileageKm = (uint)Math.Round(Edc15Eeprom.RawToKm(_eeprom.OriginalOdometerRaw), MidpointRounding.AwayFromZero);
        WhileSyncing(() =>
        {
            _immoTouched = false;
            IsImmoOn = _eeprom.Immo == ImmoState.On;
            MileageText = _originalMileageKm.ToString(Invariant);
            MileageError = null;
            LoginCodeText = _eeprom.OriginalLoginCode.ToString("D5", Invariant);
            LoginCodeError = null;
        });

        Warnings.Clear();
        if (_eeprom.OriginalImmo == ImmoState.Unknown)
        {
            Warnings.Add("The immobilizer setting in this file is not recognised. The switch shows Off until you change it.");
        }
        if (!_eeprom.OriginalOdometerCopiesMatch)
        {
            Warnings.Add("The file holds two different mileages. The first one is shown and both are replaced when you change it.");
        }
        if (_eeprom.OriginalLoginCode > Edc15Eeprom.MaxLoginCode)
        {
            Warnings.Add("The login code in this file is not a valid 0XXXX code. It is shown as stored.");
        }
        if (!_eeprom.OriginalLoginCodeCopiesMatch)
        {
            Warnings.Add("The file holds two different login codes. The first one is shown and both are replaced when you change it.");
        }

        OnPropertyChanged(nameof(HasWarnings));
        OnPropertyChanged(nameof(WarningSummary));
        OnPropertyChanged(nameof(HasFile));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(Vin));
        OnPropertyChanged(nameof(ImmobilizerId));
        OnPropertyChanged(nameof(ImmoOriginalText));
        OnPropertyChanged(nameof(MileageOriginalText));
        OnPropertyChanged(nameof(LoginCodeOriginalText));
        Refresh();
    }

    private void Refresh()
    {
        if (_eeprom is null)
        {
            return;
        }

        PendingChanges = new[] { EepromField.Immobilizer, EepromField.Odometer, EepromField.LoginCode }.Count(_eeprom.IsFieldModified);
        OnPropertyChanged(nameof(ImmoStatusText));
        OnPropertyChanged(nameof(IsImmoModified));
        OnPropertyChanged(nameof(IsMileageModified));
        OnPropertyChanged(nameof(IsLoginCodeModified));
        OnPropertyChanged(nameof(CanResetMileage));
        OnPropertyChanged(nameof(CanResetLoginCode));
        OnPropertyChanged(nameof(IsModified));
        OnPropertyChanged(nameof(StatusText));
        CommandManager.InvalidateRequerySuggested();
    }

    // ---------------------------------------------------------------- helpers

    private void WhileSyncing(Action action)
    {
        _syncing = true;
        try
        {
            action();
        }
        finally
        {
            _syncing = false;
        }
    }

    private void ShowToast(string message, ToastKind kind)
    {
        ToastMessage = message;
        ToastKind = kind;
        IsToastVisible = true;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            ShowToast(ex.Message, ToastKind.Error);
        }
    }

    private static string Describe(ImmoState state) => state switch
    {
        ImmoState.On => "On",
        ImmoState.Off => "Off",
        _ => "not recognised",
    };
}
