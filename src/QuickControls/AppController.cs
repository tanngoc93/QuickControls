using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using QuickControls.Models;
using QuickControls.Services;
using QuickControls.UI;

namespace QuickControls
{
    public sealed class AppController : ApplicationContext, IDisposable
    {
        private readonly SettingsStore _settingsStore;
        private readonly IAudioService _audioService;
        private IBrightnessService _brightnessService;
        private readonly HotkeyManager _hotkeyManager;
        private readonly TrayService _trayService;
        private readonly PanelForm _panel;
        private readonly OsdForm _osd;
        private readonly System.Windows.Forms.Timer _pollTimer;
        private readonly System.Windows.Forms.Timer _brightnessDebounceTimer;
        private readonly System.Windows.Forms.Timer _placementSaveTimer;
        private readonly RegisteredWaitHandle _showWaitRegistration;
        private readonly RegisteredWaitHandle _exitWaitRegistration;
        private AppSettings _settings;
        private AudioState _audioState;
        private int _brightnessValue;
        private int? _pendingBrightness;
        private int _pendingBrightnessDelta;
        private string _pendingBrightnessDeviceId;
        private bool _pendingBrightnessShowOsd;
        private bool _brightnessSetInProgress;
        private bool _brightnessReadInProgress;
        private bool _brightnessReadPending;
        private bool _displayRefreshPending;
        private bool _displayRefreshInProgress;
        private bool _suspendHotkeys;
        private bool _exiting;

        public AppController(EventWaitHandle showEvent, EventWaitHandle exitEvent, bool startInBackground, bool suppressStartup)
        {
            _settingsStore = new SettingsStore();
            _settings = _settingsStore.Load();
            _settings.LanguageCode = AppText.NormalizeLanguageCode(_settings.LanguageCode);
            AppText.SetLanguage(_settings.LanguageCode);

            bool firstRun = _settings.FirstRun;
            bool startupInitializationFailed = false;
            if (firstRun)
            {
                bool startupInitialized = suppressStartup || StartupService.SetEnabled(_settings.StartWithWindows);
                if (startupInitialized)
                {
                    _settings.FirstRun = false;
                }
                else
                {
                    startupInitializationFailed = true;
                }
            }

            _audioService = new AudioService();
            _brightnessService = new BrightnessService(false);
            _hotkeyManager = new HotkeyManager();
            _panel = new PanelForm(_settings);
            _panel.EnsureMessageHandle();
            _osd = new OsdForm();
            _trayService = new TrayService(StartupService.IsEnabled());
            _trayService.SetLayoutChecked(_settings.PanelLayoutMode);

            _placementSaveTimer = new System.Windows.Forms.Timer();
            _placementSaveTimer.Interval = 350;
            _placementSaveTimer.Tick += delegate
            {
                _placementSaveTimer.Stop();
                SavePanelPlacement();
            };

            BindEvents();
            IList<HotkeyAction> hotkeyFailures = RegisterInitialHotkeys(firstRun);
            try { _settingsStore.Save(_settings); }
            catch { }

            _panel.SetBrightnessDevices(_brightnessService.Devices, _settings.SelectedDisplayId);
            RefreshAudio();
            RefreshDisplayDevices();

            _pollTimer = new System.Windows.Forms.Timer();
            _pollTimer.Interval = 1250;
            _pollTimer.Tick += PollTimerTick;
            _pollTimer.Start();

            _brightnessDebounceTimer = new System.Windows.Forms.Timer();
            _brightnessDebounceTimer.Interval = 170;
            _brightnessDebounceTimer.Tick += BrightnessDebounceTick;

            _showWaitRegistration = ThreadPool.RegisterWaitForSingleObject(
                showEvent,
                delegate { ShowPanelFromAnyThread(); },
                null,
                Timeout.Infinite,
                false);
            _exitWaitRegistration = ThreadPool.RegisterWaitForSingleObject(
                exitEvent,
                delegate { ExitFromAnyThread(); },
                null,
                Timeout.Infinite,
                false);

            SystemEvents.DisplaySettingsChanged += SystemDisplaySettingsChanged;
            SystemEvents.PowerModeChanged += SystemPowerModeChanged;

            if (!startInBackground)
            {
                _panel.ShowPreferred();
            }
            else if (_settings.PanelLayoutMode == PanelLayoutMode.EdgeDock)
            {
                _panel.ShowPreferredPassive();
            }

            if (hotkeyFailures.Count > 0)
            {
                _trayService.ShowInfo(
                    AppText.Get("Notification.ShortcutsUnavailable.Title"),
                    AppText.Get("Notification.ShortcutsUnavailable.Message"));
            }
            else if (startupInitializationFailed)
            {
                _trayService.ShowInfo(
                    AppText.Get("Notification.StartupFailed.Title"),
                    AppText.Get("Notification.StartupFailed.Message"));
            }
            else if (firstRun && !startInBackground)
            {
                _trayService.ShowInfo(
                    AppText.Get("Notification.Ready.Title"),
                    AppText.Get("Notification.Ready.Message"));
            }
        }

        public new void Dispose()
        {
            if (!_exiting) ExitApplication(false);
            base.Dispose();
        }

        private void BindEvents()
        {
            _panel.VolumeChanged += delegate(object sender, IntValueEventArgs args) { SetVolume(args.Value, false); };
            _panel.VolumeStepRequested += delegate(object sender, IntValueEventArgs args) { StepVolume(args.Value, false); };
            _panel.MuteRequested += delegate { ToggleMute(false); };
            _panel.BrightnessChanged += delegate(object sender, IntValueEventArgs args) { QueueBrightness(args.Value, false); };
            _panel.BrightnessStepRequested += delegate(object sender, IntValueEventArgs args) { StepBrightness(args.Value, false); };
            _panel.DisplaySelectionRequested += PanelDisplaySelectionRequested;
            _panel.BrightnessRetryRequested += delegate { RefreshDisplayDevices(); };
            _panel.SettingsRequested += delegate { ShowSettings(); };
            _panel.OpenWindowsDisplaySettingsRequested += delegate { OpenWindowsDisplaySettings(); };
            _panel.PanelPositionChanged += delegate { QueuePanelPlacementSave(); };
            _panel.CompactStateChanged += delegate { QueuePanelPlacementSave(); };

            _hotkeyManager.HotkeyPressed += HotkeyPressed;
            _trayService.OpenRequested += delegate { _panel.TogglePanel(); };
            _trayService.MuteRequested += delegate { ToggleMute(false); };
            _trayService.SettingsRequested += delegate { ShowSettings(); };
            _trayService.AboutRequested += delegate { ShowAbout(); };
            _trayService.ExitRequested += delegate { ExitApplication(true); };
            _trayService.StartupToggleRequested += TrayStartupToggleRequested;
            _trayService.LayoutRequested += delegate(object sender, PanelLayoutEventArgs args)
            {
                _panel.SetLayoutMode(args.Mode, true);
                QueuePanelPlacementSave();
            };
        }

        private IList<HotkeyAction> RegisterInitialHotkeys(bool firstRun)
        {
            IList<HotkeyAction> failures = _hotkeyManager.RegisterAll(_settings);
            if (!firstRun || failures.Count == 0) return failures;

            AppSettings fallback = _settings.Clone();
            for (int index = 0; index < failures.Count; index++)
            {
                HotkeyBinding binding = fallback.GetHotkey(failures[index]).Clone();
                binding.Modifiers |= HotkeyModifiers.Shift;
                fallback.SetHotkey(failures[index], binding);
            }

            IList<HotkeyAction> fallbackFailures = _hotkeyManager.RegisterAll(fallback);
            if (fallbackFailures.Count == 0)
            {
                _settings = fallback;
                _panel.ApplySettings(_settings);
                return fallbackFailures;
            }

            _hotkeyManager.RegisterAll(_settings);
            return failures;
        }

        private void HotkeyPressed(object sender, HotkeyPressedEventArgs args)
        {
            if (_suspendHotkeys) return;
            switch (args.Action)
            {
                case HotkeyAction.VolumeUp: StepVolume(1, true); break;
                case HotkeyAction.VolumeDown: StepVolume(-1, true); break;
                case HotkeyAction.BrightnessUp: StepBrightness(1, true); break;
                case HotkeyAction.BrightnessDown: StepBrightness(-1, true); break;
                case HotkeyAction.ToggleMute: ToggleMute(true); break;
                case HotkeyAction.TogglePanel: _panel.TogglePanel(); break;
            }
        }

        private void SetVolume(int value, bool showOsd)
        {
            int clamped = Clamp(value);
            if (_audioService.SetVolume(clamped))
            {
                _audioState = new AudioState(true, clamped, false);
                _panel.SetAudioState(_audioState);
                if (showOsd) _osd.ShowValue(AppText.Get("Osd.Volume"), clamped, false);
            }
            else
            {
                RefreshAudio();
            }
        }

        private void StepVolume(int direction, bool showOsd)
        {
            RefreshAudio();
            int current = _audioState != null && _audioState.Available ? _audioState.Volume : 0;
            SetVolume(current + direction * _settings.StepPercent, showOsd);
        }

        private void ToggleMute(bool showOsd)
        {
            if (!_audioService.ToggleMute()) return;
            RefreshAudio();
            if (showOsd && _audioState != null && _audioState.Available)
            {
                _osd.ShowMuted(_audioState.Muted, _audioState.Volume);
            }
        }

        private void QueueBrightness(int value, bool showOsd)
        {
            if (_displayRefreshInProgress) return;
            BrightnessDevice device = GetSelectedBrightnessDevice();
            if (device == null) return;
            _pendingBrightness = Clamp(value);
            _pendingBrightnessDelta = 0;
            _pendingBrightnessDeviceId = device.Id;
            _pendingBrightnessShowOsd = showOsd;
            _brightnessValue = _pendingBrightness.Value;
            _panel.SetBrightnessState(true, _brightnessValue, string.Empty);
            if (showOsd) _osd.ShowValue(AppText.Get("Osd.Brightness"), _brightnessValue, true);
            _brightnessDebounceTimer.Stop();
            _brightnessDebounceTimer.Interval = showOsd ? 60 : 170;
            _brightnessDebounceTimer.Start();
        }

        private void StepBrightness(int direction, bool showOsd)
        {
            if (_displayRefreshInProgress) return;
            BrightnessDevice device = GetSelectedBrightnessDevice();
            if (device == null) return;

            if (!string.IsNullOrEmpty(_pendingBrightnessDeviceId) &&
                !string.Equals(_pendingBrightnessDeviceId, device.Id, StringComparison.Ordinal))
            {
                _pendingBrightness = null;
                _pendingBrightnessDelta = 0;
            }

            int change = direction * _settings.StepPercent;
            if (_pendingBrightness.HasValue)
            {
                _pendingBrightness = Clamp(_pendingBrightness.Value + change);
            }
            else
            {
                _pendingBrightnessDelta += change;
            }
            _pendingBrightnessDeviceId = device.Id;
            _pendingBrightnessShowOsd = _pendingBrightnessShowOsd || showOsd;

            _brightnessValue = Clamp(_brightnessValue + change);
            _panel.SetBrightnessState(true, _brightnessValue, string.Empty);
            if (showOsd) _osd.ShowValue(AppText.Get("Osd.Brightness"), _brightnessValue, true);

            if (!HasPendingBrightness)
            {
                _pendingBrightnessDeviceId = null;
                _pendingBrightnessShowOsd = false;
                _brightnessDebounceTimer.Stop();
                return;
            }

            _brightnessDebounceTimer.Stop();
            _brightnessDebounceTimer.Interval = showOsd ? 60 : 120;
            _brightnessDebounceTimer.Start();
        }

        private void BrightnessDebounceTick(object sender, EventArgs eventArgs)
        {
            _brightnessDebounceTimer.Stop();
            StartPendingBrightnessOperation();
        }

        private void StartPendingBrightnessOperation()
        {
            if (_displayRefreshInProgress || _brightnessSetInProgress || !HasPendingBrightness) return;
            BrightnessDevice device = GetSelectedBrightnessDevice();
            if (device == null || !string.Equals(device.Id, _pendingBrightnessDeviceId, StringComparison.Ordinal))
            {
                _pendingBrightness = null;
                _pendingBrightnessDelta = 0;
                _pendingBrightnessDeviceId = null;
                _pendingBrightnessShowOsd = false;
                RefreshBrightness();
                return;
            }

            bool hasAbsoluteTarget = _pendingBrightness.HasValue;
            int target = hasAbsoluteTarget ? _pendingBrightness.Value : 0;
            int delta = _pendingBrightnessDelta;
            bool showOsd = _pendingBrightnessShowOsd;
            _pendingBrightness = null;
            _pendingBrightnessDelta = 0;
            _pendingBrightnessDeviceId = null;
            _pendingBrightnessShowOsd = false;
            _brightnessSetInProgress = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool success;
                if (hasAbsoluteTarget)
                {
                    success = device.SetPercent(target);
                }
                else
                {
                    int current;
                    success = device.TryGetPercent(out current);
                    if (success)
                    {
                        target = Clamp(current + delta);
                        success = device.SetPercent(target);
                    }
                }
                try
                {
                    _panel.BeginInvoke(new Action(delegate
                    {
                        _brightnessSetInProgress = false;
                        if (success && !HasPendingBrightness)
                        {
                            _brightnessValue = target;
                            _panel.SetBrightnessState(true, target, string.Empty);
                            if (showOsd) _osd.ShowValue(AppText.Get("Osd.Brightness"), target, true);
                        }
                        if (!success) RefreshBrightness();
                        if (HasPendingBrightness)
                        {
                            _brightnessDebounceTimer.Stop();
                            _brightnessDebounceTimer.Interval = 80;
                            _brightnessDebounceTimer.Start();
                        }
                        else
                        {
                            ContinueDeferredBrightnessWork();
                        }
                    }));
                }
                catch
                {
                }
            });
        }

        private void RefreshAudio()
        {
            _audioState = _audioService.GetState();
            _panel.SetAudioState(_audioState);
        }

        private void RefreshBrightness()
        {
            if (_displayRefreshInProgress)
            {
                _brightnessReadPending = true;
                return;
            }
            if (_brightnessSetInProgress || HasPendingBrightness)
            {
                _brightnessReadPending = true;
                return;
            }
            if (_brightnessReadInProgress)
            {
                _brightnessReadPending = true;
                return;
            }
            _brightnessReadPending = false;

            BrightnessDevice device = GetSelectedBrightnessDevice();
            if (device == null)
            {
                _brightnessValue = 0;
                _panel.SetBrightnessState(false, 0, _brightnessService.StatusMessage);
                return;
            }

            string deviceId = device.Id;
            _brightnessReadInProgress = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                int value;
                bool success = device.TryGetPercent(out value);
                try
                {
                    _panel.BeginInvoke(new Action(delegate
                    {
                        _brightnessReadInProgress = false;
                        BrightnessDevice selectedDevice = GetSelectedBrightnessDevice();
                        bool sameDevice = selectedDevice != null &&
                            string.Equals(selectedDevice.Id, deviceId, StringComparison.Ordinal);
                        bool canApply = sameDevice && !_brightnessSetInProgress && !HasPendingBrightness;

                        if (canApply && success)
                        {
                            _brightnessValue = Clamp(value);
                            _panel.SetBrightnessState(true, _brightnessValue, string.Empty);
                        }
                        else if (canApply)
                        {
                            _panel.SetBrightnessState(false, 0, AppText.Get("Panel.DisplayUnsupported"));
                        }

                        ContinueDeferredBrightnessWork();
                    }));
                }
                catch
                {
                }
            });
        }

        private void RefreshDisplayDevices()
        {
            if (_displayRefreshInProgress || _brightnessSetInProgress || _brightnessReadInProgress || HasPendingBrightness)
            {
                _displayRefreshPending = true;
                return;
            }
            _displayRefreshPending = false;
            _displayRefreshInProgress = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                IBrightnessService replacement = null;
                try
                {
                    replacement = new BrightnessService();
                    _panel.BeginInvoke(new Action(delegate
                    {
                        IBrightnessService previous = _brightnessService;
                        _brightnessService = replacement;
                        replacement = null;
                        _displayRefreshInProgress = false;
                        _brightnessReadPending = false;
                        _panel.SetBrightnessDevices(_brightnessService.Devices, _settings.SelectedDisplayId);
                        SaveSelectedDisplay();
                        previous.Dispose();

                        if (_displayRefreshPending)
                        {
                            RefreshDisplayDevices();
                        }
                        else
                        {
                            RefreshBrightness();
                        }
                    }));
                }
                catch
                {
                    if (replacement != null) replacement.Dispose();
                    try
                    {
                        _panel.BeginInvoke(new Action(delegate
                        {
                            _displayRefreshInProgress = false;
                            _panel.SetBrightnessState(false, 0, AppText.Get("Panel.NoDisplays"));
                        }));
                    }
                    catch
                    {
                    }
                }
            });
        }

        private BrightnessDevice GetSelectedBrightnessDevice()
        {
            int index = _panel.SelectedDisplayIndex;
            IList<BrightnessDevice> devices = _brightnessService.Devices;
            return index >= 0 && index < devices.Count ? devices[index] : null;
        }

        private void PanelDisplaySelectionRequested(object sender, IntValueEventArgs args)
        {
            _brightnessDebounceTimer.Stop();
            _pendingBrightness = null;
            _pendingBrightnessDelta = 0;
            _pendingBrightnessDeviceId = null;
            _pendingBrightnessShowOsd = false;
            SaveSelectedDisplay();
            RefreshBrightness();
        }

        private void ContinueDeferredBrightnessWork()
        {
            if (_displayRefreshInProgress || _brightnessSetInProgress || _brightnessReadInProgress || HasPendingBrightness) return;

            if (_displayRefreshPending)
            {
                RefreshDisplayDevices();
                return;
            }

            if (_brightnessReadPending)
            {
                _brightnessReadPending = false;
                RefreshBrightness();
            }
        }

        private void SaveSelectedDisplay()
        {
            BrightnessDevice device = GetSelectedBrightnessDevice();
            _settings.SelectedDisplayId = device == null ? string.Empty : device.Id;
            try { _settingsStore.Save(_settings); }
            catch { }
        }

        private void PollTimerTick(object sender, EventArgs eventArgs)
        {
            RefreshAudio();
            RefreshBrightness();
        }

        private void ShowSettings()
        {
            _suspendHotkeys = true;
            try
            {
                using (SettingsForm form = new SettingsForm(_settings, ApplySettings))
                {
                    form.ShowDialog(_panel.Visible ? _panel : null);
                }
            }
            finally
            {
                _suspendHotkeys = false;
            }
        }

        private string ApplySettings(AppSettings candidate)
        {
            AppSettings previous = _settings.Clone();
            bool languageChanged = !string.Equals(
                AppText.NormalizeLanguageCode(previous.LanguageCode),
                AppText.NormalizeLanguageCode(candidate.LanguageCode),
                StringComparison.OrdinalIgnoreCase);
            bool displaySelectionChanged = !string.Equals(
                previous.SelectedDisplayId,
                candidate.SelectedDisplayId,
                StringComparison.Ordinal);
            bool resetPanelPosition = candidate.PanelLeft == -1D && candidate.PanelTop == -1D;
            bool previousStartupEnabled = StartupService.IsEnabled();
            IList<HotkeyAction> failures = _hotkeyManager.RegisterAll(candidate);
            if (failures.Count > 0)
            {
                _hotkeyManager.RegisterAll(previous);
                return AppText.Format("Error.ShortcutInUse", FriendlyActionName(failures[0]));
            }

            if (!StartupService.SetEnabled(candidate.StartWithWindows))
            {
                _hotkeyManager.RegisterAll(previous);
                return AppText.Get("Error.StartupNotAllowed");
            }

            candidate.FirstRun = false;
            candidate.LanguageCode = AppText.NormalizeLanguageCode(candidate.LanguageCode);
            if (!resetPanelPosition)
            {
                System.Drawing.Point savedLocation = _panel.GetSavedLocationFor(
                    candidate.PanelLayoutMode, candidate.DockEdge);
                candidate.PanelLeft = savedLocation.X;
                candidate.PanelTop = savedLocation.Y;
            }
            candidate.PanelCompact = candidate.PanelLayoutMode == PanelLayoutMode.HorizontalMini;
            candidate.SettingsVersion = AppSettings.CurrentSettingsVersion;
            try
            {
                _settingsStore.Save(candidate);
            }
            catch
            {
                _hotkeyManager.RegisterAll(previous);
                StartupService.SetEnabled(previousStartupEnabled);
                return AppText.Get("Error.SaveSettings");
            }
            _settings = candidate;
            AppText.SetLanguage(_settings.LanguageCode);
            _panel.ApplySettings(_settings);
            if (resetPanelPosition) _panel.ResetPosition();
            _osd.ApplyLanguage();
            _trayService.ApplyLanguage();
            _trayService.SetStartupChecked(_settings.StartWithWindows);
            RefreshAudio();
            if (languageChanged || displaySelectionChanged) RefreshDisplayDevices();
            else RefreshBrightness();
            return null;
        }

        private void TrayStartupToggleRequested(object sender, StartupToggleEventArgs args)
        {
            bool previousValue = _settings.StartWithWindows;
            bool previousStartupEnabled = StartupService.IsEnabled();
            bool succeeded = StartupService.SetEnabled(args.Enabled);
            if (succeeded)
            {
                _settings.StartWithWindows = args.Enabled;
                try
                {
                    _settingsStore.Save(_settings);
                }
                catch
                {
                    _settings.StartWithWindows = previousValue;
                    StartupService.SetEnabled(previousStartupEnabled);
                    _trayService.SetStartupChecked(previousStartupEnabled);
                    _trayService.ShowInfo(
                        AppText.Get("Notification.SaveFailed.Title"),
                        AppText.Get("Notification.SaveFailed.Message"));
                }
            }
            else
            {
                _trayService.SetStartupChecked(StartupService.IsEnabled());
                _trayService.ShowInfo(
                    AppText.Get("Notification.StartupChangeFailed.Title"),
                    AppText.Get("Notification.StartupChangeFailed.Message"));
            }
        }

        private void SavePanelPlacement()
        {
            if (_settings == null || _panel == null) return;
            System.Drawing.Point savedLocation = _panel.GetSavedLocationFor(
                _panel.PreferredLayout, _settings.DockEdge);
            _settings.PanelLeft = savedLocation.X;
            _settings.PanelTop = savedLocation.Y;
            _settings.PanelLayoutMode = _panel.PreferredLayout;
            _settings.PanelCompact = _panel.PreferredLayout == PanelLayoutMode.HorizontalMini;
            _trayService.SetLayoutChecked(_settings.PanelLayoutMode);
            try { _settingsStore.Save(_settings); }
            catch { }
        }

        private void QueuePanelPlacementSave()
        {
            _placementSaveTimer.Stop();
            _placementSaveTimer.Start();
        }

        private void ShowAbout()
        {
            using (AboutForm form = new AboutForm())
            {
                form.ShowDialog(_panel.Visible ? _panel : null);
            }
        }

        private static void OpenWindowsDisplaySettings()
        {
            try { Process.Start("ms-settings:display"); }
            catch { }
        }

        private void ShowPanelFromAnyThread()
        {
            try
            {
                _panel.BeginInvoke(new Action(delegate { _panel.ShowPreferred(); }));
            }
            catch
            {
            }
        }

        private void ExitFromAnyThread()
        {
            try
            {
                _panel.BeginInvoke(new Action(delegate { ExitApplication(false); }));
            }
            catch
            {
            }
        }

        private void SystemDisplaySettingsChanged(object sender, EventArgs eventArgs)
        {
            ShowOnUiThread(delegate
            {
                _panel.HandleDisplayConfigurationChanged();
                RefreshDisplayDevices();
            });
        }

        private void SystemPowerModeChanged(object sender, PowerModeChangedEventArgs eventArgs)
        {
            if (eventArgs.Mode == PowerModes.Resume) ShowOnUiThread(RefreshDisplayDevices);
        }

        private void ShowOnUiThread(Action action)
        {
            try { _panel.BeginInvoke(action); }
            catch { }
        }

        private void ExitApplication(bool askForConfirmation)
        {
            if (_exiting) return;
            if (askForConfirmation)
            {
                DialogResult answer = MessageBox.Show(
                    AppText.Get("Exit.Confirmation"),
                    AppText.Get("Exit.Title"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);
                if (answer != DialogResult.Yes) return;
            }

            _exiting = true;
            SystemEvents.DisplaySettingsChanged -= SystemDisplaySettingsChanged;
            SystemEvents.PowerModeChanged -= SystemPowerModeChanged;
            if (_showWaitRegistration != null) _showWaitRegistration.Unregister(null);
            if (_exitWaitRegistration != null) _exitWaitRegistration.Unregister(null);
            _pollTimer.Stop();
            _brightnessDebounceTimer.Stop();
            _placementSaveTimer.Stop();
            SavePanelPlacement();
            _panel.PrepareForExit();
            _trayService.Dispose();
            _hotkeyManager.Dispose();
            _brightnessService.Dispose();
            _audioService.Dispose();
            _osd.Close();
            _panel.Close();
            ExitThread();
        }

        private static string FriendlyActionName(HotkeyAction action)
        {
            switch (action)
            {
                case HotkeyAction.VolumeUp: return AppText.Get("Action.IncreaseVolume");
                case HotkeyAction.VolumeDown: return AppText.Get("Action.DecreaseVolume");
                case HotkeyAction.BrightnessUp: return AppText.Get("Action.IncreaseBrightness");
                case HotkeyAction.BrightnessDown: return AppText.Get("Action.DecreaseBrightness");
                case HotkeyAction.ToggleMute: return AppText.Get("Action.ToggleMute");
                default: return AppText.Get("Action.TogglePanel");
            }
        }

        private static int Clamp(int value)
        {
            return Math.Max(0, Math.Min(100, value));
        }

        private bool HasPendingBrightness
        {
            get { return _pendingBrightness.HasValue || _pendingBrightnessDelta != 0; }
        }
    }
}
