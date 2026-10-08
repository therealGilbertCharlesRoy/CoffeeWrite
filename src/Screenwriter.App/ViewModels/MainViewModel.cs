using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Screenwriter.Core.Projects;
using Screenwriter.Core.Documents;
using Screenwriter.Core.Formatting;
using Screenwriter.Core.Story;
using Screenwriter.App.Configuration;
using Screenwriter.Editor;
using Screenwriter.Infrastructure.Projects;
using Screenwriter.Infrastructure.Settings;
using Screenwriter.Infrastructure.GoogleDrive;
using Screenwriter.Infrastructure.Analytics;
using WeCantSpell.Hunspell;

namespace Screenwriter.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public IReadOnlyList<PageSizeOption> PageSizes { get; } =
    [
        new("USLetter", "US Letter"),
        new("A4", "A4"),
        new("USLegal", "US Legal")
    ];
    private readonly LocalProjectStore _projectStore = new();
    private readonly RecoveryStore _recoveryStore = new();
    private readonly LocalPreferenceStore _preferenceStore = new();
    private readonly GoogleDriveOAuthConfiguration _driveOAuthConfiguration = GoogleDriveOAuthConfiguration.Load();
    private readonly GoogleDriveOAuthClient _driveOAuthClient = new();
    private readonly GoogleDriveApiClient _driveApiClient = new();
    private readonly WindowsCredentialStore _driveCredentialStore = new();
    private readonly PostHogAnalyticsClient _analyticsClient = new();
    private ScreenplayProject? _project;
    private string? _projectPath;
    private Guid? _activeEpisodeId;
    private CancellationTokenSource? _recoveryDebounce;
    private WordList? _spellDictionary;
    private readonly HashSet<string> _ignoredSpellingWords = new(StringComparer.OrdinalIgnoreCase);
    private bool _scriptPagesDirty = true;
    private bool _isUpdatingAudioVisualText;
    private DriveFileMetadata? _driveConflictMetadata;
    private bool _analyticsChoiceMade;
    private bool _isAnalyticsEnabled;
    private int _previewPageIndex;
    private int _lastCaretOffset;
    private PageSizeOption _selectedPageSize = new("USLetter", "US Letter");
    private decimal _leftMarginInches = 1.5m;
    private decimal _rightMarginInches = 1m;
    private decimal _topMarginInches = 1m;
    private decimal _bottomMarginInches = 1m;
    private bool _showPageNumbers = true;
    private bool _isRelationshipMapVisible = true;
    private double _relationshipMapZoom = 1;
    private double _relationshipMapPanX;
    private double _relationshipMapPanY;

    public ObservableCollection<SeasonNavigationItem> Seasons { get; } = [];
    public IReadOnlyList<ScriptFormatProfile> FormatProfiles => ScriptFormatProfiles.All;
    public ObservableCollection<ElementTypeOption> ElementOptions { get; } = [];
    public ObservableCollection<ScriptSuggestion> Suggestions { get; } = [];
    public ObservableCollection<SpellingIssue> SpellingIssues { get; } = [];
    public ObservableCollection<ScriptPageViewModel> ScriptPages { get; } = [];
    public ObservableCollection<ScriptPageViewModel> CurrentPreviewPageItems { get; } = [];
    public ObservableCollection<CharacterCardViewModel> Characters { get; } = [];
    public ObservableCollection<CharacterCardViewModel> FilteredCharacters { get; } = [];
    public ObservableCollection<RelationshipCardViewModel> Relationships { get; } = [];
    public ObservableCollection<BeatGroupViewModel> BeatGroups { get; } = [];
    public ObservableCollection<SceneLinkOption> SceneOptions { get; } = [];
    public ObservableCollection<BeatGroupChoice> BeatGroupChoices { get; } = [];
    public event Action<int>? NavigateToSceneRequested;
    public ObservableCollection<AudioVisualWritingRowViewModel> AudioVisualWritingRows { get; } = [];

    private ScriptFormatProfile _selectedFormatProfile = ScriptFormatProfiles.Get("movie");
    public ScriptFormatProfile SelectedFormatProfile
    {
        get => _selectedFormatProfile;
        set => SetFormatProfile(value.Id, markUnsaved: true);
    }

    public PageSizeOption SelectedPageSize
    {
        get => _selectedPageSize;
        set => SetPageSize(value.Id, markUnsaved: true);
    }

    public decimal LeftMarginInches { get => _leftMarginInches; set => SetMargin(ref _leftMarginInches, value, nameof(LeftMarginInches)); }
    public decimal RightMarginInches { get => _rightMarginInches; set => SetMargin(ref _rightMarginInches, value, nameof(RightMarginInches)); }
    public decimal TopMarginInches { get => _topMarginInches; set => SetMargin(ref _topMarginInches, value, nameof(TopMarginInches)); }
    public decimal BottomMarginInches { get => _bottomMarginInches; set => SetMargin(ref _bottomMarginInches, value, nameof(BottomMarginInches)); }
    public bool ShowPageNumbers
    {
        get => _showPageNumbers;
        set
        {
            if (SetProperty(ref _showPageNumbers, value))
                PersistPageSettings();
        }
    }

    [ObservableProperty]
    public partial bool IsPageSetupVisible { get; set; }

    [RelayCommand]
    private void TogglePageSetup() => IsPageSetupVisible = !IsPageSetupVisible;

    private void SetMargin(ref decimal field, decimal value, string propertyName)
    {
        if (value < 0.25m || value > 3m || field == value)
            return;
        field = value;
        OnPropertyChanged(propertyName);
        PersistPageSettings();
    }

    private void PersistPageSettings()
    {
        if (_project is not null)
        {
            var settings = GetOrCreateActiveScript(_project, _activeEpisodeId).PageSettings;
            ApplyCurrentPageSettings(settings);
        }
        InvalidateScriptPages();
        MarkUnsaved("Page setup changed · unsaved changes");
    }

    private void LoadPageSettings(ScriptPageSettings settings)
    {
        _leftMarginInches = (decimal)Math.Clamp(settings.LeftMarginInches, 0.25, 3);
        _rightMarginInches = (decimal)Math.Clamp(settings.RightMarginInches, 0.25, 3);
        _topMarginInches = (decimal)Math.Clamp(settings.TopMarginInches, 0.25, 3);
        _bottomMarginInches = (decimal)Math.Clamp(settings.BottomMarginInches, 0.25, 3);
        _showPageNumbers = settings.ShowPageNumbers;
        OnPropertyChanged(nameof(LeftMarginInches));
        OnPropertyChanged(nameof(RightMarginInches));
        OnPropertyChanged(nameof(TopMarginInches));
        OnPropertyChanged(nameof(BottomMarginInches));
        OnPropertyChanged(nameof(ShowPageNumbers));
    }

    private void ApplyCurrentPageSettings(ScriptPageSettings settings)
    {
        settings.PaperSize = _selectedPageSize.Id;
        settings.LeftMarginInches = (double)_leftMarginInches;
        settings.RightMarginInches = (double)_rightMarginInches;
        settings.TopMarginInches = (double)_topMarginInches;
        settings.BottomMarginInches = (double)_bottomMarginInches;
        settings.ShowPageNumbers = _showPageNumbers;
    }

    public void SetPageSize(string paperSize, bool markUnsaved)
    {
        var option = PageSizes.FirstOrDefault(item => item.Id.Equals(paperSize, StringComparison.OrdinalIgnoreCase)) ?? PageSizes[0];
        if (_selectedPageSize != option)
        {
            _selectedPageSize = option;
            OnPropertyChanged(nameof(SelectedPageSize));
        }

        if (_project is not null)
            GetOrCreateActiveScript(_project, _activeEpisodeId).PageSettings.PaperSize = option.Id;
        InvalidateScriptPages();
        if (markUnsaved)
            MarkUnsaved("Page size changed · unsaved changes");
    }

    partial void OnIsPageSetupVisibleChanged(bool value)
    {
        if (value)
        {
            IsFindReplaceVisible = false;
            IsSpellcheckVisible = false;
        }
    }

    private ElementTypeOption? _selectedElementOption;
    public ElementTypeOption? SelectedElementOption
    {
        get => _selectedElementOption;
        set
        {
            if (SetProperty(ref _selectedElementOption, value))
                RefreshSuggestions();
        }
    }

    private ScriptSuggestion? _selectedSuggestion;
    public ScriptSuggestion? SelectedSuggestion
    {
        get => _selectedSuggestion;
        set => SetProperty(ref _selectedSuggestion, value);
    }

    public MainViewModel()
    {
        RefreshElementOptions();
        var analyticsConsent = _preferenceStore.LoadAnalyticsConsent();
        _analyticsChoiceMade = analyticsConsent.ChoiceMade;
        _isAnalyticsEnabled = analyticsConsent.ChoiceMade && analyticsConsent.Enabled;
        _analyticsClient.SetConsent(_isAnalyticsEnabled);
        IsPrivacySettingsVisible = !analyticsConsent.ChoiceMade;
        _analyticsClient.Track(ProductAnalyticsEvent.AppLaunched);
        TrackAnalyticsFeature(AnalyticsFeature.Editor);
        TrackAnalyticsFeature(AnalyticsFeature.AiPlaceholder);
        IsDarkTheme = _preferenceStore.LoadDarkTheme();
        if (Application.Current is not null)
            Application.Current.RequestedThemeVariant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        RefreshScriptPages();
    }

    [ObservableProperty]
    public partial string ProjectTitle { get; set; } = "Untitled screenplay";

    [ObservableProperty]
    public partial string SaveStateText { get; set; } = "New project · not saved";

    [ObservableProperty]
    public partial bool HasUnsavedChanges { get; set; }

    public string? ProjectPath => _projectPath;

    [ObservableProperty]
    public partial string ScriptText { get; set; } = """
EXT. RIVERTON MAIN STREET — DUSK

The last light slips behind the rooftops. A hand-painted sign swings above the empty street: THE ORPHEUM.

MARA VOSS, 32, crosses the street with a cardboard box tucked under one arm. She stops beneath the sign.

                              MARA
            You couldn't have picked a better day to haunt me.

A warm rectangle of light appears in the Orpheum's upper window.

INT. ORPHEUM THEATRE — PROJECTION BOOTH — CONTINUOUS

Dust turns in the projector beam. The reels begin to spin.
""";

    [ObservableProperty]
    public partial bool IsBeatBoardVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsBeatBoardExpanded { get; set; }

    [ObservableProperty]
    public partial string ActiveTool { get; set; } = "Research";

    [ObservableProperty]
    public partial bool IsDarkTheme { get; set; }

    [ObservableProperty]
    public partial bool IsPrivacySettingsVisible { get; set; }

    [ObservableProperty]
    public partial string PrivacySettingsMessage { get; set; } = string.Empty;

    public bool IsAnalyticsEnabled => _isAnalyticsEnabled;
    public bool CanClosePrivacySettings => _analyticsChoiceMade;
    public string AnalyticsConsentStatus => _isAnalyticsEnabled
        ? _analyticsClient.IsConfigured ? "Optional analytics is on. You can turn it off at any time." : "Your preference is saved. PostHog is not configured, so no usage data is sent."
        : "Analytics is off. No usage data is sent.";
    public string AnalyticsInformationText => _analyticsClient.ConfigurationStatus;

    [ObservableProperty]
    public partial bool IsFindReplaceVisible { get; set; }

    [ObservableProperty]
    public partial bool IsSpellcheckVisible { get; set; }

    [ObservableProperty]
    public partial bool IsPagePreviewVisible { get; set; }

    [ObservableProperty]
    public partial SpellingIssue? SelectedSpellingIssue { get; set; }

    [ObservableProperty]
    public partial string? SelectedSpellingSuggestion { get; set; }

    [ObservableProperty]
    public partial string SpellcheckStatus { get; set; } = "Spellcheck runs on this device.";

    [ObservableProperty]
    public partial string CurrentPageIndicator { get; set; } = "Page 1 of 1";

    [ObservableProperty]
    public partial string FindQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ReplaceText { get; set; } = string.Empty;

    public GridLength BeatBoardWidth => IsBeatBoardVisible
        ? new GridLength(IsBeatBoardExpanded ? 5 : 4, GridUnitType.Star)
        : new GridLength(0);

    public GridLength EditorWidth => new(IsBeatBoardVisible && IsBeatBoardExpanded ? 6 : 5, GridUnitType.Star);

    public GridLength ToolsWidth => new(3, GridUnitType.Star);
    public string WritingSurfaceModeText => IsPagePreviewVisible ? "Write" : "Pages";
    public bool IsYouTubeFormat => SelectedFormatProfile.Layout == ScriptLayoutKind.TwoColumnAudioVisual;
    public bool IsTelevisionFormat => SelectedFormatProfile.Layout == ScriptLayoutKind.Television;
    public bool IsWritingSurfaceVisible => !IsPagePreviewVisible && !IsYouTubeFormat;
    public bool IsAudioVisualWritingVisible => !IsPagePreviewVisible && IsYouTubeFormat;

    public bool ShowResearch => ActiveTool == "Research";
    public bool ShowCharacters => ActiveTool == "Characters";
    public bool ShowRelationships => ActiveTool == "Relationships";
    public bool ShowDrive => ActiveTool == "Drive";
    public bool IsDriveSyncSelected
    {
        get => _project?.DriveSync.SelectedForSync ?? false;
        set => SetDriveSyncSelected(value);
    }
    public bool IsDriveAutoSyncEnabled
    {
        get => _project?.DriveSync.AutoSyncEnabled ?? false;
        set => SetDriveAutoSyncEnabled(value);
    }
    public string DriveSyncStatusText => IsDriveSyncSelected
        ? _project?.DriveSync.PendingSync == true
            ? "Saved locally. Drive sync is queued and will retry when connected."
            : _project?.DriveSync.LastSyncedAt is { } lastSynced
            ? $"Synced with Google Drive {lastSynced.ToLocalTime():g}."
            : IsDriveConnected ? "Selected for Google Drive sync. Use Sync now to upload this project." : "Selected for Drive sync. Connect Drive to start transferring this project."
        : "This project is local only.";
    public string DriveOAuthStatusText => _driveOAuthConfiguration.IsConfigured
        ? !OperatingSystem.IsWindows()
            ? "Google Drive connection is available in the Windows release first."
            : IsDriveConnected ? "Google Drive is connected. Your projects stay local until selected for sync." : "Connect a Google account to enable selected-project sync."
        : "Google Drive sign-in is not configured yet. Add your desktop OAuth client ID before release.";

    [ObservableProperty]
    public partial bool IsDriveConnected { get; set; }

    [ObservableProperty]
    public partial bool IsDriveConnecting { get; set; }

    [ObservableProperty]
    public partial string DriveConnectionMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsDriveSyncing { get; set; }

    [ObservableProperty]
    public partial string? DriveConflictCopyPath { get; set; }

    public bool HasDriveConflict => !string.IsNullOrWhiteSpace(DriveConflictCopyPath) && _driveConflictMetadata is not null;
    public string DriveConflictCopyName => string.IsNullOrWhiteSpace(DriveConflictCopyPath) ? "" : Path.GetFileName(DriveConflictCopyPath);

    public bool CanConnectDrive => _driveOAuthConfiguration.IsConfigured && OperatingSystem.IsWindows() && !IsDriveConnected && !IsDriveConnecting;
    public bool CanDisconnectDrive => IsDriveConnected && !IsDriveConnecting;
    public bool CanSyncDrive => IsDriveConnected && IsDriveSyncSelected && !IsDriveSyncing && !HasDriveConflict && !string.IsNullOrWhiteSpace(_projectPath);
    public bool CanResolveDriveConflict => HasDriveConflict && IsDriveConnected && IsDriveSyncSelected && !IsDriveSyncing && !string.IsNullOrWhiteSpace(_projectPath);
    public bool IsRelationshipMapVisible
    {
        get => _isRelationshipMapVisible;
        set
        {
            if (SetProperty(ref _isRelationshipMapVisible, value))
                OnPropertyChanged(nameof(RelationshipViewButtonText));
                OnPropertyChanged(nameof(IsRelationshipDetailsVisible));
        }
    }
    public bool IsRelationshipDetailsVisible => !IsRelationshipMapVisible;
    public string RelationshipViewButtonText => IsRelationshipMapVisible ? "Details" : "Map";
    public double RelationshipMapZoom => _relationshipMapZoom;
    public double RelationshipMapPanX => _relationshipMapPanX;
    public double RelationshipMapPanY => _relationshipMapPanY;
    public bool CanAddRelationship => Characters.Count >= 2;
    public bool NeedsMoreCharacters => !CanAddRelationship;
    public bool CharacterSearchHasNoResults => !string.IsNullOrWhiteSpace(CharacterSearchText) && FilteredCharacters.Count == 0;

    [ObservableProperty]
    public partial string CharacterSearchText { get; set; } = string.Empty;

    partial void OnCharacterSearchTextChanged(string value) => ApplyCharacterFilter();
    public bool HasRelationships => Relationships.Count > 0;
    public bool IsSeriesProject => _project?.Kind == ProjectKind.TelevisionSeries;
    public bool CanRecoverSession { get; private set; }
    public string RecoveryMessage { get; private set; } = string.Empty;
    public bool ShowBeatBoardContent => LeftPanelMode == "Beats";
    public bool ShowEpisodeContent => LeftPanelMode == "Episodes" && IsSeriesProject;
    public string StoryPanelTitle => ShowEpisodeContent ? "Series & episodes" : "Beat board";

    public void SetMapPan(double x, double y, bool commit)
    {
        _relationshipMapPanX = x;
        _relationshipMapPanY = y;
        OnPropertyChanged(nameof(RelationshipMapPanX));
        OnPropertyChanged(nameof(RelationshipMapPanY));
        if (_project is not null)
        {
            _project.Story.RelationshipMap.PanX = x;
            _project.Story.RelationshipMap.PanY = y;
        }
        if (commit)
            MarkUnsaved("Relationship map moved · unsaved changes");
    }

    private void SetMapZoom(double value)
    {
        _relationshipMapZoom = Math.Clamp(value, 0.6, 1.6);
        OnPropertyChanged(nameof(RelationshipMapZoom));
        if (_project is not null)
            _project.Story.RelationshipMap.Zoom = _relationshipMapZoom;
        MarkUnsaved("Relationship map zoom changed · unsaved changes");
    }

    private void LoadMapState()
    {
        var state = _project?.Story.RelationshipMap ?? new RelationshipMapState();
        _relationshipMapZoom = Math.Clamp(state.Zoom, 0.6, 1.6);
        _relationshipMapPanX = state.PanX;
        _relationshipMapPanY = state.PanY;
        OnPropertyChanged(nameof(RelationshipMapZoom));
        OnPropertyChanged(nameof(RelationshipMapPanX));
        OnPropertyChanged(nameof(RelationshipMapPanY));
    }

    [RelayCommand]
    private void ZoomMapIn() => SetMapZoom(_relationshipMapZoom + 0.1);

    [RelayCommand]
    private void ZoomMapOut() => SetMapZoom(_relationshipMapZoom - 0.1);

    [RelayCommand]
    private void ResetMapView()
    {
        SetMapPan(0, 0, commit: false);
        SetMapZoom(1);
    }

    [RelayCommand]
    private void ToggleRelationshipView() => IsRelationshipMapVisible = !IsRelationshipMapVisible;

    [ObservableProperty]
    public partial string LeftPanelMode { get; set; } = "Beats";

    partial void OnIsBeatBoardVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(BeatBoardWidth));
        OnPropertyChanged(nameof(EditorWidth));
    }

    partial void OnIsBeatBoardExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(BeatBoardWidth));
        OnPropertyChanged(nameof(EditorWidth));
    }

    partial void OnActiveToolChanged(string value)
    {
        OnPropertyChanged(nameof(ShowResearch));
        OnPropertyChanged(nameof(ShowCharacters));
        OnPropertyChanged(nameof(ShowRelationships));
        OnPropertyChanged(nameof(ShowDrive));
    }

    partial void OnLeftPanelModeChanged(string value)
    {
        OnPropertyChanged(nameof(ShowBeatBoardContent));
        OnPropertyChanged(nameof(ShowEpisodeContent));
        OnPropertyChanged(nameof(StoryPanelTitle));
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        if (Application.Current is not null)
            Application.Current.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;

        try
        {
            _preferenceStore.SaveDarkTheme(value);
        }
        catch (IOException)
        {
            SaveStateText = "Theme changed · preference could not be saved";
        }
    }

    [RelayCommand]
    private void ToggleBeatBoard()
    {
        IsBeatBoardVisible = !IsBeatBoardVisible;
        TrackAnalyticsFeature(AnalyticsFeature.BeatBoard);
    }

    [RelayCommand]
    private void ToggleBeatBoardSize() => IsBeatBoardExpanded = !IsBeatBoardExpanded;

    [RelayCommand]
    private void ShowTool(string tool)
    {
        ActiveTool = tool;
        switch (tool)
        {
            case "Research": TrackAnalyticsFeature(AnalyticsFeature.AiPlaceholder); break;
            case "Characters": TrackAnalyticsFeature(AnalyticsFeature.CharacterTracker); break;
            case "Relationships": TrackAnalyticsFeature(AnalyticsFeature.RelationshipTracker); break;
            case "Drive": TrackAnalyticsFeature(AnalyticsFeature.GoogleDrive); break;
        }
    }

    public void SetDriveSyncSelected(bool selected)
    {
        if (_project is null || _project.DriveSync.SelectedForSync == selected)
            return;
        _project.DriveSync.SelectedForSync = selected;
        if (!selected)
        {
            _project.DriveSync.AutoSyncEnabled = false;
            _project.DriveSync.PendingSync = false;
        }
        OnPropertyChanged(nameof(IsDriveSyncSelected));
        OnPropertyChanged(nameof(IsDriveAutoSyncEnabled));
        OnPropertyChanged(nameof(DriveSyncStatusText));
        OnPropertyChanged(nameof(CanSyncDrive));
        OnPropertyChanged(nameof(CanResolveDriveConflict));
        MarkUnsaved("Drive sync preference changed · unsaved changes");
    }

    public void SetDriveAutoSyncEnabled(bool enabled)
    {
        if (_project is null || !_project.DriveSync.SelectedForSync || _project.DriveSync.AutoSyncEnabled == enabled)
            return;
        _project.DriveSync.AutoSyncEnabled = enabled;
        if (!enabled)
            _project.DriveSync.PendingSync = false;
        OnPropertyChanged(nameof(IsDriveAutoSyncEnabled));
        OnPropertyChanged(nameof(DriveSyncStatusText));
        OnPropertyChanged(nameof(CanSyncDrive));
        MarkUnsaved("Drive auto-sync preference changed · unsaved changes");
    }

    public async Task InitializeDriveConnectionAsync()
    {
        if (!_driveOAuthConfiguration.IsConfigured || !OperatingSystem.IsWindows())
            return;
        try
        {
            IsDriveConnected = !string.IsNullOrWhiteSpace(_driveCredentialStore.ReadRefreshToken());
            OnPropertyChanged(nameof(CanConnectDrive));
            OnPropertyChanged(nameof(CanDisconnectDrive));
            OnPropertyChanged(nameof(DriveOAuthStatusText));
            OnPropertyChanged(nameof(CanSyncDrive));
            OnPropertyChanged(nameof(CanResolveDriveConflict));
            await TrySyncPendingDriveAsync();
        }
        catch (Exception exception)
        {
            DriveConnectionMessage = exception.Message;
        }
    }

    [RelayCommand]
    private async Task ConnectDriveAsync()
    {
        if (!CanConnectDrive)
            return;
        IsDriveConnecting = true;
        OnPropertyChanged(nameof(CanConnectDrive));
        OnPropertyChanged(nameof(CanDisconnectDrive));
        DriveConnectionMessage = "Waiting for Google authorization in your browser…";
        try
        {
            var tokens = await _driveOAuthClient.AuthorizeAsync(_driveOAuthConfiguration.ClientId, uri =>
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                return Task.CompletedTask;
            });
            _driveCredentialStore.SaveRefreshToken(tokens.RefreshToken);
            IsDriveConnected = true;
            DriveConnectionMessage = "Google Drive is connected. No project has been uploaded.";
            _analyticsClient.Track(ProductAnalyticsEvent.DriveConnected);
            await TrySyncPendingDriveAsync();
        }
        catch (Exception exception)
        {
            DriveConnectionMessage = exception.Message;
            _analyticsClient.Track(ProductAnalyticsEvent.DriveConnectionFailed);
        }
        finally
        {
            IsDriveConnecting = false;
            OnPropertyChanged(nameof(CanConnectDrive));
            OnPropertyChanged(nameof(CanDisconnectDrive));
            OnPropertyChanged(nameof(DriveOAuthStatusText));
            OnPropertyChanged(nameof(CanSyncDrive));
            OnPropertyChanged(nameof(CanResolveDriveConflict));
        }
    }

    [RelayCommand]
    private async Task DisconnectDriveAsync()
    {
        IsDriveConnecting = true;
        OnPropertyChanged(nameof(CanConnectDrive));
        OnPropertyChanged(nameof(CanDisconnectDrive));
        try
        {
            var refreshToken = _driveCredentialStore.ReadRefreshToken();
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                IsDriveConnected = false;
                DriveConnectionMessage = "No saved Google Drive connection was found.";
                return;
            }
            await _driveOAuthClient.RevokeAsync(refreshToken);
            _driveCredentialStore.DeleteRefreshToken();
            IsDriveConnected = false;
            DriveConnectionMessage = "Google Drive access was revoked. Local projects and Drive copies are unchanged.";
            _analyticsClient.Track(ProductAnalyticsEvent.DriveDisconnected);
        }
        catch (Exception exception)
        {
            DriveConnectionMessage = exception.Message;
        }
        finally
        {
            IsDriveConnecting = false;
            OnPropertyChanged(nameof(CanConnectDrive));
            OnPropertyChanged(nameof(CanDisconnectDrive));
            OnPropertyChanged(nameof(DriveOAuthStatusText));
            OnPropertyChanged(nameof(CanSyncDrive));
            OnPropertyChanged(nameof(CanResolveDriveConflict));
        }
    }

    private void RefreshDriveSyncProperties()
    {
        OnPropertyChanged(nameof(IsDriveSyncSelected));
        OnPropertyChanged(nameof(IsDriveAutoSyncEnabled));
        OnPropertyChanged(nameof(DriveSyncStatusText));
        OnPropertyChanged(nameof(CanSyncDrive));
        OnPropertyChanged(nameof(CanResolveDriveConflict));
    }

    [RelayCommand]
    private Task SyncDriveAsync() => SyncDriveCoreAsync();

    private async Task SyncDriveCoreAsync()
    {
        if (!CanSyncDrive || _project is null || string.IsNullOrWhiteSpace(_projectPath))
            return;

        IsDriveSyncing = true;
        OnPropertyChanged(nameof(CanSyncDrive));
        DriveConnectionMessage = "Saving locally, then syncing the selected project…";
        try
        {
            await SaveProjectAsync(_projectPath);
            var refreshToken = _driveCredentialStore.ReadRefreshToken();
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new InvalidOperationException("Google Drive is disconnected. Connect again to sync this project.");

            var tokens = await _driveOAuthClient.RefreshAccessTokenAsync(_driveOAuthConfiguration.ClientId, refreshToken);
            var accessToken = tokens.AccessToken;
            var sync = _project.DriveSync;
            var folderId = await _driveApiClient.EnsureDraftroomFolderAsync(accessToken, sync.DriveFolderId ?? _preferenceStore.LoadDriveFolderId());
            sync.DriveFolderId = folderId;
            _preferenceStore.SaveDriveFolderId(folderId);

            if (string.IsNullOrWhiteSpace(sync.DriveFileId))
            {
                var uploaded = await _driveApiClient.UploadProjectAsync(accessToken, _projectPath, folderId);
                ApplyDriveMetadata(sync, uploaded);
                sync.PendingSync = false;
                await _projectStore.SaveAsync(_projectPath, _project);
                _analyticsClient.Track(ProductAnalyticsEvent.DriveProjectUploaded);
                DriveConnectionMessage = "Project uploaded to Google Drive. Your local copy remains available.";
            }
            else
            {
                var current = await _driveApiClient.GetFileAsync(accessToken, sync.DriveFileId);
                if (current.Trashed || current.MimeType != "application/vnd.draftroom.project")
                    throw new InvalidOperationException("The linked Google Drive item is unavailable or has a different file type. The local project was kept unchanged.");

                if (RevisionChanged(sync, current))
                {
                    var conflictPath = await SaveDriveConflictCopyAsync(accessToken, current.Id, _projectPath);
                    SetDriveConflict(conflictPath, current);
                    sync.PendingSync = false;
                    await _projectStore.SaveAsync(_projectPath, _project);
                    _analyticsClient.Track(ProductAnalyticsEvent.DriveSyncConflict);
                    DriveConnectionMessage = $"Drive has a newer copy. Your local work is safe; the Drive copy was downloaded as ‘{Path.GetFileName(conflictPath)}’. Review both before syncing again.";
                    return;
                }

                try
                {
                    var updated = await _driveApiClient.UpdateProjectAsync(accessToken, current.Id, current.ETag ?? sync.RemoteETag ?? string.Empty, _projectPath);
                    ApplyDriveMetadata(sync, updated);
                    sync.PendingSync = false;
                    await _projectStore.SaveAsync(_projectPath, _project);
                    _analyticsClient.Track(ProductAnalyticsEvent.DriveSyncCompleted);
                    DriveConnectionMessage = "Google Drive is up to date. Your local copy remains available.";
                }
                catch (GoogleDriveConflictException)
                {
                    var conflictPath = await SaveDriveConflictCopyAsync(accessToken, current.Id, _projectPath);
                    SetDriveConflict(conflictPath, current);
                    sync.PendingSync = false;
                    await _projectStore.SaveAsync(_projectPath, _project);
                    _analyticsClient.Track(ProductAnalyticsEvent.DriveSyncConflict);
                    DriveConnectionMessage = $"Drive changed during sync. Your local work is safe; the Drive copy was downloaded as ‘{Path.GetFileName(conflictPath)}’. Review both before syncing again.";
                }
            }
            OnPropertyChanged(nameof(DriveSyncStatusText));
        }
        catch (Exception exception)
        {
            _analyticsClient.Track(ProductAnalyticsEvent.DriveSyncFailed);
            DriveConnectionMessage = $"Saved locally. Drive sync could not finish: {exception.Message}";
        }
        finally
        {
            IsDriveSyncing = false;
            OnPropertyChanged(nameof(CanSyncDrive));
            OnPropertyChanged(nameof(DriveSyncStatusText));
            OnPropertyChanged(nameof(CanResolveDriveConflict));
        }
    }

    private void SetDriveConflict(string conflictPath, DriveFileMetadata remote)
    {
        DriveConflictCopyPath = conflictPath;
        _driveConflictMetadata = remote;
        OnPropertyChanged(nameof(HasDriveConflict));
        OnPropertyChanged(nameof(DriveConflictCopyName));
        OnPropertyChanged(nameof(CanSyncDrive));
        OnPropertyChanged(nameof(CanResolveDriveConflict));
    }

    [RelayCommand]
    private async Task OpenDriveConflictCopyAsync()
    {
        if (string.IsNullOrWhiteSpace(DriveConflictCopyPath) || !File.Exists(DriveConflictCopyPath))
            return;
        await OpenProjectAsync(DriveConflictCopyPath);
        DriveConnectionMessage = "Opened the preserved Drive copy. Your previous local project remains saved separately.";
        DriveConflictCopyPath = null;
        _driveConflictMetadata = null;
        OnPropertyChanged(nameof(HasDriveConflict));
        OnPropertyChanged(nameof(DriveConflictCopyName));
        OnPropertyChanged(nameof(CanSyncDrive));
        OnPropertyChanged(nameof(CanResolveDriveConflict));
    }

    [RelayCommand]
    private async Task KeepLocalAndUpdateDriveAsync()
    {
        var conflictMetadata = _driveConflictMetadata;
        if (!CanResolveDriveConflict || _project is null || conflictMetadata is null || string.IsNullOrWhiteSpace(_projectPath))
            return;

        IsDriveSyncing = true;
        OnPropertyChanged(nameof(CanResolveDriveConflict));
        try
        {
            var refreshToken = _driveCredentialStore.ReadRefreshToken();
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new InvalidOperationException("Google Drive is disconnected. Connect again to resolve this conflict.");
            var tokens = await _driveOAuthClient.RefreshAccessTokenAsync(_driveOAuthConfiguration.ClientId, refreshToken);
            try
            {
                var updated = await _driveApiClient.UpdateProjectAsync(tokens.AccessToken, conflictMetadata.Id, conflictMetadata.ETag ?? string.Empty, _projectPath);
                ApplyDriveMetadata(_project.DriveSync, updated);
                _project.DriveSync.PendingSync = false;
                await _projectStore.SaveAsync(_projectPath, _project);
                _analyticsClient.Track(ProductAnalyticsEvent.DriveSyncCompleted);
                DriveConflictCopyPath = null;
                _driveConflictMetadata = null;
                OnPropertyChanged(nameof(HasDriveConflict));
                OnPropertyChanged(nameof(DriveConflictCopyName));
                OnPropertyChanged(nameof(CanSyncDrive));
                DriveConnectionMessage = "Your local copy is now the Google Drive version. The previous Drive copy remains preserved as a local conflict file.";
            }
            catch (GoogleDriveConflictException)
            {
                var latest = await _driveApiClient.GetFileAsync(tokens.AccessToken, conflictMetadata.Id);
                var latestCopy = await SaveDriveConflictCopyAsync(tokens.AccessToken, latest.Id, _projectPath);
                SetDriveConflict(latestCopy, latest);
                DriveConnectionMessage = "Drive changed again. The newer version was saved as another conflict copy; review it before choosing again.";
            }
        }
        catch (Exception exception)
        {
            DriveConnectionMessage = $"Both copies remain safe. Conflict resolution could not finish: {exception.Message}";
        }
        finally
        {
            IsDriveSyncing = false;
            OnPropertyChanged(nameof(CanSyncDrive));
            OnPropertyChanged(nameof(CanResolveDriveConflict));
            OnPropertyChanged(nameof(DriveSyncStatusText));
        }
    }

    private async Task TrySyncPendingDriveAsync()
    {
        if (IsDriveConnected && _project?.DriveSync is { SelectedForSync: true, AutoSyncEnabled: true, PendingSync: true }
            && !string.IsNullOrWhiteSpace(_projectPath) && !IsDriveSyncing)
            await SyncDriveCoreAsync();
    }

    public Task RetryPendingDriveSyncAsync() => TrySyncPendingDriveAsync();

    private static bool RevisionChanged(DriveSyncSettings sync, DriveFileMetadata current) =>
        (!string.IsNullOrWhiteSpace(sync.RemoteRevisionId) && !string.Equals(sync.RemoteRevisionId, current.HeadRevisionId, StringComparison.Ordinal)) ||
        (string.IsNullOrWhiteSpace(current.HeadRevisionId) && !string.IsNullOrWhiteSpace(sync.RemoteETag) && !string.Equals(sync.RemoteETag, current.ETag, StringComparison.Ordinal));

    private static void ApplyDriveMetadata(DriveSyncSettings sync, DriveFileMetadata metadata)
    {
        sync.DriveFileId = metadata.Id;
        sync.RemoteRevisionId = metadata.HeadRevisionId;
        sync.RemoteETag = metadata.ETag;
        sync.LastSyncedAt = DateTimeOffset.UtcNow;
    }

    private async Task<string> SaveDriveConflictCopyAsync(string accessToken, string fileId, string localPath)
    {
        var directory = Path.GetDirectoryName(localPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(localPath);
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var conflictPath = Path.Combine(directory, $"{name} (Drive conflict {stamp}).draftroom");
        for (var suffix = 2; File.Exists(conflictPath); suffix++)
            conflictPath = Path.Combine(directory, $"{name} (Drive conflict {stamp} {suffix}).draftroom");

        var temporaryPath = Path.Combine(directory, $".{Guid.NewGuid():N}.drive-conflict.tmp");
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous))
                await _driveApiClient.DownloadFileAsync(accessToken, fileId, stream);
            await _projectStore.OpenAsync(temporaryPath);
            File.Move(temporaryPath, conflictPath);
            return conflictPath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    [RelayCommand]
    private void ShowLeftPanel(string mode) => LeftPanelMode = mode;

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        TrackAnalyticsFeature(AnalyticsFeature.ThemeSelection);
    }

    [RelayCommand]
    private void OpenPrivacySettings() => IsPrivacySettingsVisible = true;

    [RelayCommand]
    private void AllowAnalytics()
    {
        SaveAnalyticsConsent(enabled: true);
    }

    [RelayCommand]
    private void KeepAnalyticsOff()
    {
        SaveAnalyticsConsent(enabled: false);
    }

    [RelayCommand]
    private void ClosePrivacySettings()
    {
        if (_analyticsChoiceMade)
            IsPrivacySettingsVisible = false;
    }

    private void SaveAnalyticsConsent(bool enabled)
    {
        try
        {
            _preferenceStore.SaveAnalyticsConsent(choiceMade: true, enabled: enabled);
            _analyticsClient.SetConsent(enabled);
            _analyticsChoiceMade = true;
            _isAnalyticsEnabled = enabled;
            PrivacySettingsMessage = string.Empty;
            OnPropertyChanged(nameof(IsAnalyticsEnabled));
            OnPropertyChanged(nameof(AnalyticsConsentStatus));
            OnPropertyChanged(nameof(AnalyticsInformationText));
            OnPropertyChanged(nameof(CanClosePrivacySettings));
            IsPrivacySettingsVisible = false;
        }
        catch (IOException)
        {
            PrivacySettingsMessage = "Your choice could not be saved. Analytics remains inactive in this build.";
        }
        catch (UnauthorizedAccessException)
        {
            PrivacySettingsMessage = "Your choice could not be saved. Analytics remains inactive in this build.";
        }
    }

    public void TrackAnalyticsFeature(AnalyticsFeature feature) =>
        _analyticsClient.Track(ProductAnalyticsEvent.FeatureUsed, feature);

    public Task FlushPendingAnalyticsAsync() => _analyticsClient.FlushPendingAsync();

    [RelayCommand]
    private void CheckSpelling()
    {
        IsSpellcheckVisible = true;
        SpellingIssues.Clear();
        try
        {
            _spellDictionary ??= LoadSpellDictionary();
            foreach (Match match in Regex.Matches(ScriptText, @"[\p{L}\p{M}]+(?:['’][\p{L}\p{M}]+)?", RegexOptions.CultureInvariant))
            {
                var word = match.Value;
                if (_ignoredSpellingWords.Contains(word) || word.All(char.IsUpper) || _spellDictionary.Check(word))
                    continue;

                SpellingIssues.Add(new SpellingIssue(word, match.Index, _spellDictionary.Suggest(word).Take(4).ToArray()));
            }

            SpellcheckStatus = SpellingIssues.Count == 0
                ? "No spelling issues found."
                : $"{SpellingIssues.Count} possible spelling issue{(SpellingIssues.Count == 1 ? string.Empty : "s")}.";
            SelectedSpellingIssue = SpellingIssues.FirstOrDefault();
            SelectedSpellingSuggestion = SelectedSpellingIssue?.Suggestions.FirstOrDefault();
        }
        catch (Exception exception)
        {
            SpellcheckStatus = $"Spellcheck unavailable: {exception.Message}";
        }
    }

    public void IgnoreSpellingWord(string word)
    {
        _ignoredSpellingWords.Add(word);
        CheckSpelling();
    }

    partial void OnSelectedSpellingIssueChanged(SpellingIssue? value)
    {
        OnPropertyChanged(nameof(SelectedSpellingSuggestions));
        SelectedSpellingSuggestion = value?.Suggestions.FirstOrDefault();
    }

    public IReadOnlyList<string> SelectedSpellingSuggestions => SelectedSpellingIssue?.Suggestions ?? [];

    private static WordList LoadSpellDictionary()
    {
        var assembly = typeof(MainViewModel).Assembly;
        using var dictionaryStream = assembly.GetManifestResourceStream("Screenwriter.App.Assets.Spellcheck.en_US.dic")
            ?? throw new FileNotFoundException("The bundled English dictionary is missing.");
        using var affixStream = assembly.GetManifestResourceStream("Screenwriter.App.Assets.Spellcheck.en_US.aff")
            ?? throw new FileNotFoundException("The bundled English spelling rules are missing.");
        return WordList.CreateFromStreams(dictionaryStream, affixStream);
    }

    partial void OnScriptTextChanged(string value)
    {
        if (_project is not null)
        {
            var script = GetOrCreateActiveScript(_project, _activeEpisodeId);
            ScriptEditingController.SyncElements(script, value);
            RefreshSceneOptions(script);
            RefreshSuggestions();
        }
        if (IsYouTubeFormat && !_isUpdatingAudioVisualText)
            RefreshAudioVisualWritingRows();
        HasUnsavedChanges = true;
        SaveStateText = _projectPath is null ? "New project · unsaved changes" : "Unsaved changes";
        ScheduleRecovery();
        InvalidateScriptPages();
    }

    partial void OnIsPagePreviewVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(WritingSurfaceModeText));
        OnPropertyChanged(nameof(IsWritingSurfaceVisible));
        OnPropertyChanged(nameof(IsAudioVisualWritingVisible));
        if (value && _scriptPagesDirty)
            RefreshScriptPages();
        else if (value)
            SelectPreviewPageForOffset(_lastCaretOffset);
        else
            UpdateCurrentPageIndicator(_lastCaretOffset);
    }

    [RelayCommand]
    private void TogglePagePreview() => IsPagePreviewVisible = !IsPagePreviewVisible;

    private void RefreshScriptPages()
    {
        var document = _project is null
            ? new ScriptDocument { Text = ScriptText, FormatProfile = SelectedFormatProfile.Id, PageSettings = new ScriptPageSettings { PaperSize = _selectedPageSize.Id } }
            : FindActiveScript(_project, _activeEpisodeId) ?? new ScriptDocument { Text = ScriptText, FormatProfile = SelectedFormatProfile.Id };
        ScriptEditingController.SyncElements(document, ScriptText);
        ApplyCurrentPageSettings(document.PageSettings);
        var layouts = ScriptPaginationService.Paginate(document, SelectedFormatProfile);
        var previewScale = GetPreviewScale(document.PageSettings.PaperSize);

        ScriptPages.Clear();
        foreach (var layout in layouts)
        {
            if (layout.IsTitlePage)
            {
                var titlePage = document.TitlePage;
                ScriptPages.Add(new ScriptPageViewModel(layout.Index, layout.StartOffset, 0, true, false, false,
                    ProjectTitle, titlePage.Subtitle, titlePage.Author, titlePage.DraftDate, titlePage.Contact, false, [],
                    GetPreviewPageHeight(document.PageSettings.PaperSize), GetPreviewPagePadding(document.PageSettings, previewScale)));
                continue;
            }

            var lines = layout.Content.Split('\n');
            var rows = new List<ScriptPreviewRowViewModel>(lines.Length);
            var firstLine = layout.Content.Length == 0 ? 0 : ScriptText[..layout.StartOffset].Count(character => character == '\n');
            var isTwoColumn = SelectedFormatProfile.Layout == ScriptLayoutKind.TwoColumnAudioVisual;
            for (var index = 0; index < lines.Length; index++)
            {
                var typeIndex = firstLine + index;
                var type = typeIndex < document.Elements.Count ? document.Elements[typeIndex].Type : ScriptElementType.Action;
                var rule = SelectedFormatProfile.FindRule(type);
                var text = lines[index].TrimEnd('\r');
                if (rule?.Uppercase == true)
                    text = text.ToUpperInvariant();

                if (isTwoColumn && type is ScriptElementType.Audio or ScriptElementType.Visual)
                {
                    var leftText = type == ScriptElementType.Audio ? text : string.Empty;
                    var rightText = type == ScriptElementType.Visual ? text : string.Empty;
                    var topSpace = rule?.SpaceBeforePoints ?? 0;
                    var bottomSpace = rule?.SpaceAfterPoints ?? 0;
                    if (index + 1 < lines.Length && firstLine + index + 1 < document.Elements.Count)
                    {
                        var nextType = document.Elements[firstLine + index + 1].Type;
                        if ((nextType is ScriptElementType.Audio or ScriptElementType.Visual) && nextType != type)
                        {
                            var nextRule = SelectedFormatProfile.FindRule(nextType);
                            var nextText = lines[index + 1].TrimEnd('\r');
                            if (nextRule?.Uppercase == true)
                                nextText = nextText.ToUpperInvariant();
                            if (nextType == ScriptElementType.Audio)
                                leftText = nextText;
                            else
                                rightText = nextText;
                            topSpace = Math.Max(topSpace, nextRule?.SpaceBeforePoints ?? 0);
                            bottomSpace = Math.Max(bottomSpace, nextRule?.SpaceAfterPoints ?? 0);
                            index++;
                        }
                    }

                    var leftLines = ScriptPaginationService.WrapLine(leftText, SelectedFormatProfile.FindRule(ScriptElementType.Audio), document.PageSettings, SelectedFormatProfile.Layout, columnWidth: true);
                    var rightLines = ScriptPaginationService.WrapLine(rightText, SelectedFormatProfile.FindRule(ScriptElementType.Visual), document.PageSettings, SelectedFormatProfile.Layout, columnWidth: true);
                    var rowCount = Math.Max(leftLines.Count, rightLines.Count);
                    for (var row = 0; row < rowCount; row++)
                    {
                        rows.Add(new ScriptPreviewRowViewModel(
                            row < leftLines.Count ? leftLines[row] : string.Empty,
                            row < rightLines.Count ? rightLines[row] : string.Empty,
                            string.Empty,
                            false,
                            new Avalonia.Thickness(0, row == 0 ? topSpace * 96 / 72 * previewScale : 0, 0,
                                row == rowCount - 1 ? bottomSpace * 96 / 72 * previewScale : 0)));
                    }
                    continue;
                }

                var wrappedLines = ScriptPaginationService.WrapLine(text, rule, document.PageSettings, SelectedFormatProfile.Layout);
                for (var row = 0; row < wrappedLines.Count; row++)
                {
                    var margin = new Avalonia.Thickness(
                        (rule?.LeftIndentInches ?? 0) * 96 * previewScale,
                        row == 0 ? (rule?.SpaceBeforePoints ?? 0) * 96 / 72 * previewScale : 0,
                        (rule?.RightIndentInches ?? 0) * 96 * previewScale,
                        row == wrappedLines.Count - 1 ? (rule?.SpaceAfterPoints ?? 0) * 96 / 72 * previewScale : 0);
                    rows.Add(new ScriptPreviewRowViewModel(string.Empty, string.Empty, wrappedLines[row], true, margin));
                }
            }

            ScriptPages.Add(new ScriptPageViewModel(layout.Index, layout.StartOffset, layout.PageNumber, false, true,
                document.PageSettings.ShowPageNumbers, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, isTwoColumn, rows,
                GetPreviewPageHeight(document.PageSettings.PaperSize), GetPreviewPagePadding(document.PageSettings, previewScale)));
        }

        _scriptPagesDirty = false;
        UpdateCurrentPageIndicator(_lastCaretOffset);
    }

    private static double GetPreviewScale(string paperSize) => 670 / (GetPaperWidthInches(paperSize) * 96);

    private static double GetPreviewPageHeight(string paperSize) => 670 * GetPaperHeightInches(paperSize) / GetPaperWidthInches(paperSize);

    private static Thickness GetPreviewPagePadding(ScriptPageSettings settings, double scale) => new(
        settings.LeftMarginInches * 96 * scale,
        settings.TopMarginInches * 96 * scale,
        settings.RightMarginInches * 96 * scale,
        settings.BottomMarginInches * 96 * scale);

    private static double GetPaperWidthInches(string paperSize) => paperSize switch
    {
        "A4" => 8.27,
        _ => 8.5
    };

    private static double GetPaperHeightInches(string paperSize) => paperSize switch
    {
        "A4" => 11.69,
        "USLegal" => 14,
        _ => 11
    };

    private void RefreshAudioVisualWritingRows()
    {
        AudioVisualWritingRows.Clear();
        if (!IsYouTubeFormat)
            return;

        var document = _project is null
            ? new ScriptDocument { Text = ScriptText, FormatProfile = SelectedFormatProfile.Id }
            : FindActiveScript(_project, _activeEpisodeId) ?? new ScriptDocument { Text = ScriptText, FormatProfile = SelectedFormatProfile.Id };
        ScriptEditingController.SyncElements(document, ScriptText);
        var lines = ScriptText.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var type = i < document.Elements.Count ? document.Elements[i].Type : ScriptElementType.Action;
            var text = lines[i].TrimEnd('\r');
            if (type is ScriptElementType.Audio or ScriptElementType.Visual)
            {
                var leftIndex = type == ScriptElementType.Audio ? i : -1;
                var rightIndex = type == ScriptElementType.Visual ? i : -1;
                var leftText = type == ScriptElementType.Audio ? text : string.Empty;
                var rightText = type == ScriptElementType.Visual ? text : string.Empty;
                if (i + 1 < lines.Length && i + 1 < document.Elements.Count)
                {
                    var nextType = document.Elements[i + 1].Type;
                    if ((nextType is ScriptElementType.Audio or ScriptElementType.Visual) && nextType != type)
                    {
                        var nextText = lines[i + 1].TrimEnd('\r');
                        if (nextType == ScriptElementType.Audio)
                        {
                            leftIndex = i + 1;
                            leftText = nextText;
                        }
                        else
                        {
                            rightIndex = i + 1;
                            rightText = nextText;
                        }
                        i++;
                    }
                }

                AudioVisualWritingRows.Add(new AudioVisualWritingRowViewModel(leftIndex, rightIndex, -1, false, leftText, rightText, string.Empty));
            }
            else
            {
                AudioVisualWritingRows.Add(new AudioVisualWritingRowViewModel(-1, -1, i, true, string.Empty, string.Empty, text));
            }
        }
    }

    [RelayCommand]
    private void AddAudioVisualRow()
    {
        if (!IsYouTubeFormat)
            return;

        var script = EnsureActiveScript();
        var lineCount = ScriptText.Split('\n').Length;
        int audioLine;
        int visualLine;
        if (ScriptText.Length == 0)
        {
            ScriptText = "\n";
            audioLine = 0;
            visualLine = 1;
        }
        else if (ScriptText.EndsWith('\n'))
        {
            ScriptText += "\n";
            audioLine = lineCount - 1;
            visualLine = lineCount;
        }
        else
        {
            ScriptText += "\n\n";
            audioLine = lineCount;
            visualLine = lineCount + 1;
        }

        ScriptEditingController.SetElementTypeAtLine(script, ScriptText, audioLine, ScriptElementType.Audio);
        ScriptEditingController.SetElementTypeAtLine(script, ScriptText, visualLine, ScriptElementType.Visual);
        RefreshAudioVisualWritingRows();
        InvalidateScriptPages();
    }

    public void UpdateAudioVisualWritingText(AudioVisualWritingRowViewModel row, bool rightColumn, string text)
    {
        var lineIndex = row.IsFullWidth ? row.FullLineIndex : rightColumn ? row.RightLineIndex : row.LeftLineIndex;
        if (lineIndex < 0)
            return;

        var lines = ScriptText.Split('\n');
        if (lineIndex >= lines.Length)
            return;
        var current = lines[lineIndex].TrimEnd('\r');
        if (current == text)
            return;

        lines[lineIndex] = text;
        if (row.IsFullWidth)
            row.FullText = text;
        else if (rightColumn)
            row.RightText = text;
        else
            row.LeftText = text;
        _isUpdatingAudioVisualText = true;
        try
        {
            ScriptText = string.Join('\n', lines);
        }
        finally
        {
            _isUpdatingAudioVisualText = false;
        }
    }

    public void UpdateCurrentPageIndicator(int offset)
    {
        _lastCaretOffset = offset;
        if (_scriptPagesDirty)
            RefreshScriptPages();

        var scriptPages = ScriptPages.Where(page => page.IsScriptPage).ToArray();
        if (scriptPages.Length == 0)
        {
            CurrentPageIndicator = "Page 1 of 1";
            return;
        }

        var index = Array.FindLastIndex(scriptPages, page => page.StartOffset <= offset);
        if (index < 0)
            index = 0;
        var previewIndex = Array.IndexOf(ScriptPages.ToArray(), scriptPages[index]);
        if (IsPagePreviewVisible)
            ShowPreviewPage(previewIndex);
        else
            CurrentPageIndicator = $"Page {index + 1} of {scriptPages.Length}";
    }

    public void NavigatePreviewPage(int direction)
    {
        if (_scriptPagesDirty)
            RefreshScriptPages();
        if (ScriptPages.Count > 0)
            ShowPreviewPage(Math.Clamp(_previewPageIndex + direction, 0, ScriptPages.Count - 1));
    }

    private void SelectPreviewPageForOffset(int offset)
    {
        var scriptPages = ScriptPages.Where(page => page.IsScriptPage).ToArray();
        if (scriptPages.Length == 0)
        {
            ShowPreviewPage(ScriptPages.Count > 0 ? 0 : -1);
            CurrentPageIndicator = "Page 1 of 1";
            return;
        }

        var scriptIndex = Array.FindLastIndex(scriptPages, page => page.StartOffset <= offset);
        if (scriptIndex < 0)
            scriptIndex = 0;
        ShowPreviewPage(Array.IndexOf(ScriptPages.ToArray(), scriptPages[scriptIndex]));
    }

    private void ShowPreviewPage(int index)
    {
        if (index < 0 || index >= ScriptPages.Count)
        {
            CurrentPreviewPageItems.Clear();
            return;
        }

        _previewPageIndex = index;
        CurrentPreviewPageItems.Clear();
        CurrentPreviewPageItems.Add(ScriptPages[index]);
        if (ScriptPages[index].IsTitlePage)
        {
            CurrentPageIndicator = "Title page";
            return;
        }

        var scriptPageIndex = ScriptPages.Take(index + 1).Count(page => page.IsScriptPage);
        var total = ScriptPages.Count(page => page.IsScriptPage);
        CurrentPageIndicator = $"Page {scriptPageIndex} of {total}";
    }

    public int GetAdjacentScriptPageOffset(int currentOffset, int direction)
    {
        if (_scriptPagesDirty)
            RefreshScriptPages();

        var scriptPages = ScriptPages.Where(page => page.IsScriptPage).ToArray();
        if (scriptPages.Length == 0)
            return 0;

        var currentIndex = Array.FindLastIndex(scriptPages, page => page.StartOffset <= currentOffset);
        var targetIndex = Math.Clamp((currentIndex < 0 ? 0 : currentIndex) + direction, 0, scriptPages.Length - 1);
        return scriptPages[targetIndex].StartOffset;
    }

    private void InvalidateScriptPages()
    {
        _scriptPagesDirty = true;
        if (IsPagePreviewVisible)
            RefreshScriptPages();
    }

    partial void OnProjectTitleChanged(string value)
    {
        HasUnsavedChanges = true;
        SaveStateText = _projectPath is null ? "New project · unsaved changes" : "Unsaved changes";
        ScheduleRecovery();
        InvalidateScriptPages();
    }

    public async Task SaveProjectAsync(string path)
    {
        _project ??= new ScreenplayProject();
        _project.DriveSync.PendingSync = _project.DriveSync.SelectedForSync && _project.DriveSync.AutoSyncEnabled;
        _project.Title = ProjectTitle;
        if (_project.Kind == ProjectKind.TelevisionSeries && _project.Series is not null)
            _project.Series.Title = ProjectTitle;
        var script = GetOrCreateActiveScript(_project, _activeEpisodeId);
        script.FormatProfile = SelectedFormatProfile.Id;
        ApplyCurrentPageSettings(script.PageSettings);
        if (_project.Kind == ProjectKind.StandaloneScript)
            script.Title = ProjectTitle;
        script.Text = ScriptText;
        ScriptEditingController.SyncElements(script, ScriptText);

        await _projectStore.SaveAsync(path, _project);
        _projectPath = Path.GetFullPath(path);
        OnPropertyChanged(nameof(ProjectPath));
        HasUnsavedChanges = false;
        _recoveryDebounce?.Cancel();
        SaveStateText = "All changes saved locally";
        _recoveryStore.Clear();
        CanRecoverSession = false;
        OnPropertyChanged(nameof(CanRecoverSession));
        OnPropertyChanged(nameof(CanSyncDrive));
        OnPropertyChanged(nameof(DriveSyncStatusText));
        if (_project.DriveSync.SelectedForSync && _project.DriveSync.AutoSyncEnabled && IsDriveConnected && !IsDriveSyncing)
            await SyncDriveCoreAsync();
        else if (_project.DriveSync.PendingSync)
            DriveConnectionMessage = "Saved locally. Drive sync is queued and will retry when connected.";
    }

    public ScriptDocument GetDocumentForExport()
    {
        var document = _project is null
            ? new ScriptDocument { Title = ProjectTitle, FormatProfile = SelectedFormatProfile.Id, Text = ScriptText, PageSettings = new ScriptPageSettings { PaperSize = _selectedPageSize.Id } }
            : GetOrCreateActiveScript(_project, _activeEpisodeId);
        if (_project?.Kind == ProjectKind.StandaloneScript)
            document.Title = ProjectTitle;
        document.FormatProfile = SelectedFormatProfile.Id;
        document.PageSettings.PaperSize = _selectedPageSize.Id;
        ApplyCurrentPageSettings(document.PageSettings);
        document.Text = ScriptText;
        ScriptEditingController.SyncElements(document, ScriptText);
        return document;
    }

    public async Task OpenProjectAsync(string path)
    {
        var project = await _projectStore.OpenAsync(path);
        _project = project;
        RefreshDriveSyncProperties();
        RebuildBeatGroups();
        RebuildCharacterCards();
        _projectPath = Path.GetFullPath(path);
        ProjectTitle = project.Kind == ProjectKind.TelevisionSeries
            ? project.Series?.Title ?? project.Title
            : project.Title;
        RebuildSeasonNavigation();
        var firstEpisode = Seasons.SelectMany(season => season.Episodes).FirstOrDefault();
        _activeEpisodeId = firstEpisode?.Id;
        SetActiveEpisode(_activeEpisodeId);
        var script = FindActiveScript(project, _activeEpisodeId);
        LoadPageSettings(script?.PageSettings ?? new ScriptPageSettings());
        SetPageSize(script?.PageSettings.PaperSize ?? "USLetter", markUnsaved: false);
        ScriptText = script?.Text ?? string.Empty;
        if (script is not null)
            SetFormatProfile(script.FormatProfile, markUnsaved: false);
        LeftPanelMode = "Beats";
        OnPropertyChanged(nameof(ProjectPath));
        OnPropertyChanged(nameof(CanSyncDrive));
        OnPropertyChanged(nameof(IsSeriesProject));
        OnPropertyChanged(nameof(ShowEpisodeContent));
        HasUnsavedChanges = false;
        _recoveryDebounce?.Cancel();
        SaveStateText = "Project opened locally";
        _recoveryStore.Clear();
        CanRecoverSession = false;
        OnPropertyChanged(nameof(CanRecoverSession));
        if (IsDriveConnected)
            await TrySyncPendingDriveAsync();
    }

    public void StartNewProject()
    {
        _project = new ScreenplayProject();
        RefreshDriveSyncProperties();
        RebuildBeatGroups();
        RebuildCharacterCards();
        _projectPath = null;
        _activeEpisodeId = null;
        Seasons.Clear();
        ProjectTitle = "Untitled screenplay";
        LoadPageSettings(new ScriptPageSettings());
        SetPageSize("USLetter", markUnsaved: false);
        SetFormatProfile("movie", markUnsaved: false);
        ScriptText = string.Empty;
        LeftPanelMode = "Beats";
        OnPropertyChanged(nameof(ProjectPath));
        OnPropertyChanged(nameof(CanSyncDrive));
        OnPropertyChanged(nameof(IsSeriesProject));
        OnPropertyChanged(nameof(ShowEpisodeContent));
        HasUnsavedChanges = false;
        _recoveryDebounce?.Cancel();
        SaveStateText = "New project · not saved";
        _recoveryStore.Clear();
        CanRecoverSession = false;
        OnPropertyChanged(nameof(CanRecoverSession));
    }

    public void StartNewSeries()
    {
        var episode = new SeriesEpisode
        {
            Number = 1,
            Title = "Episode 1",
            Script = new ScriptDocument { Title = "Episode 1", FormatProfile = "tv" }
        };
        var season = new SeriesSeason { Episodes = [episode] };
        _project = new ScreenplayProject
        {
            Title = "Untitled series",
            Kind = ProjectKind.TelevisionSeries,
            StandaloneScript = null,
            Series = new SeriesOutline { Title = "Untitled series", Seasons = [season] }
        };
        RefreshDriveSyncProperties();
        RebuildBeatGroups();
        RebuildCharacterCards();
        _projectPath = null;
        ProjectTitle = "Untitled series";
        LoadPageSettings(new ScriptPageSettings());
        SetPageSize("USLetter", markUnsaved: false);
        SetFormatProfile("tv", markUnsaved: false);
        _activeEpisodeId = episode.Id;
        ScriptText = string.Empty;
        RebuildSeasonNavigation();
        SetActiveEpisode(_activeEpisodeId);
        LeftPanelMode = "Episodes";
        OnPropertyChanged(nameof(ProjectPath));
        OnPropertyChanged(nameof(CanSyncDrive));
        OnPropertyChanged(nameof(IsSeriesProject));
        OnPropertyChanged(nameof(ShowEpisodeContent));
        HasUnsavedChanges = true;
        SaveStateText = "New series · not saved";
        ScheduleRecovery();
    }

    public void MarkUnsaved(string message = "Unsaved changes")
    {
        HasUnsavedChanges = true;
        SaveStateText = message;
        ScheduleRecovery();
    }

    [RelayCommand]
    public void AddBeatGroup()
    {
        EnsureActiveScript();
        var data = new BeatGroup { Title = $"Section {_project!.Story.BeatGroups.Count + 1}", Order = _project.Story.BeatGroups.Count };
        _project.Story.BeatGroups.Add(data);
        BeatGroups.Add(CreateBeatGroup(data));
        RefreshBeatGroupChoices();
        MarkUnsaved("Beat section added · unsaved changes");
    }

    [RelayCommand]
    public void AddBeat()
    {
        EnsureActiveScript();
        if (_project!.Story.BeatGroups.Count == 0)
        {
            var group = new BeatGroup { Title = "Story beats", Order = 0 };
            _project.Story.BeatGroups.Add(group);
            BeatGroups.Add(CreateBeatGroup(group));
            RefreshBeatGroupChoices();
        }

        var targetGroup = _project.Story.BeatGroups.OrderBy(group => group.Order).Last();
        var data = new BeatCard
        {
            GroupId = targetGroup.Id,
            Order = _project.Story.Beats.Count(beat => beat.GroupId == targetGroup.Id),
            Title = "New beat"
        };
        _project.Story.Beats.Add(data);
        BeatGroups.First(group => group.Data.Id == targetGroup.Id).Beats.Add(CreateBeatCard(data));
        MarkUnsaved("Beat added · unsaved changes");
    }

    private BeatGroupViewModel CreateBeatGroup(BeatGroup data) => new(data, () =>
    {
        RefreshBeatGroupChoices();
        MarkUnsaved("Beat section changed · unsaved changes");
    }, MoveBeatGroup);

    private BeatCardViewModel CreateBeatCard(BeatCard data) => new(data, SceneOptions, BeatGroupChoices, () => MarkUnsaved("Beat changed · unsaved changes"), RemoveBeatCard, NavigateToScene, MoveBeat, MoveBeatToGroup);

    private void RefreshBeatGroupChoices()
    {
        if (_project is null)
        {
            BeatGroupChoices.Clear();
            return;
        }
        var orderedGroups = _project.Story.BeatGroups.OrderBy(group => group.Order).ToList();
        for (var index = BeatGroupChoices.Count - 1; index >= 0; index--)
            if (orderedGroups.All(group => group.Id != BeatGroupChoices[index].Id))
                BeatGroupChoices.RemoveAt(index);
        for (var index = 0; index < orderedGroups.Count; index++)
        {
            var group = orderedGroups[index];
            var choice = BeatGroupChoices.FirstOrDefault(item => item.Id == group.Id);
            if (choice is null)
            {
                BeatGroupChoices.Insert(Math.Min(index, BeatGroupChoices.Count), new BeatGroupChoice(group.Id, group.Title));
                continue;
            }
            choice.Title = group.Title;
            var currentIndex = BeatGroupChoices.IndexOf(choice);
            if (currentIndex != index)
                BeatGroupChoices.Move(currentIndex, index);
        }
    }

    private void MoveBeatToGroup(BeatCardViewModel beat, Guid? groupId)
    {
        if (_project is null)
            return;
        var source = BeatGroups.FirstOrDefault(group => group.Beats.Contains(beat));
        if (source is null || source.Data.Id == groupId)
            return;
        var destination = groupId is { } id
            ? BeatGroups.FirstOrDefault(group => group.Data.Id == id)
            : BeatGroups.FirstOrDefault(group => !_project.Story.BeatGroups.Any(item => item.Id == group.Data.Id));
        if (groupId is not null && destination is null)
            return;
        if (destination is null)
        {
            destination = CreateBeatGroup(new BeatGroup { Title = "Unassigned", Order = int.MaxValue });
            BeatGroups.Add(destination);
        }
        source.Beats.Remove(beat);
        beat.Data.GroupId = groupId;
        beat.Data.Order = destination.Beats.Count;
        destination.Beats.Add(beat);
        for (var order = 0; order < source.Beats.Count; order++)
            source.Beats[order].Data.Order = order;
        if (source.Beats.Count == 0 && !_project.Story.BeatGroups.Any(item => item.Id == source.Data.Id))
            BeatGroups.Remove(source);
        beat.RefreshSelectedGroup();
        MarkUnsaved("Beat moved to another section · unsaved changes");
    }

    private void MoveBeat(BeatCardViewModel beat, int delta)
    {
        foreach (var group in BeatGroups)
        {
            var index = group.Beats.IndexOf(beat);
            var target = index + delta;
            if (index < 0 || target < 0 || target >= group.Beats.Count)
                continue;
            group.Beats.Move(index, target);
            for (var order = 0; order < group.Beats.Count; order++)
                group.Beats[order].Data.Order = order;
            if (_project is null)
                return;
            MarkUnsaved("Beat order changed · unsaved changes");
            return;
        }
    }

    private void MoveBeatGroup(BeatGroupViewModel group, int delta)
    {
        var index = BeatGroups.IndexOf(group);
        var target = index + delta;
        if (_project is null || !_project.Story.BeatGroups.Any(data => data.Id == group.Data.Id)
            || index < 0 || target < 0 || target >= BeatGroups.Count)
            return;
        BeatGroups.Move(index, target);
        for (var order = 0; order < BeatGroups.Count; order++)
            BeatGroups[order].Data.Order = order;
        _project.Story.BeatGroups = BeatGroups.Where(item => _project.Story.BeatGroups.Any(data => data.Id == item.Data.Id))
            .Select(item => item.Data).ToList();
        RefreshBeatGroupChoices();
        MarkUnsaved("Beat section order changed · unsaved changes");
    }

    public void NavigateToScene(SceneReference? sceneLink)
    {
        if (_project is null || sceneLink is null)
            return;

        ScriptDocument? targetScript = null;
        Guid? targetEpisodeId = null;
        if (_project.StandaloneScript?.Id == sceneLink.ScriptDocumentId)
            targetScript = _project.StandaloneScript;
        else if (_project.Series is not null)
        {
            foreach (var episode in _project.Series.Seasons.SelectMany(season => season.Episodes))
            {
                if (episode.Script.Id != sceneLink.ScriptDocumentId)
                    continue;
                targetScript = episode.Script;
                targetEpisodeId = episode.Id;
                break;
            }
        }

        if (targetScript is null || !targetScript.Elements.Any(element => element.Id == sceneLink.SceneId))
            return;
        if (targetEpisodeId is { } episodeId && episodeId != _activeEpisodeId)
            SelectEpisode(episodeId);

        var elementIndex = targetScript.Elements.FindIndex(element => element.Id == sceneLink.SceneId);
        var lines = ScriptText.Split('\n');
        if (elementIndex < 0 || elementIndex >= lines.Length)
            return;
        var offset = lines.Take(elementIndex).Sum(line => line.Length + 1);
        NavigateToSceneRequested?.Invoke(Math.Clamp(offset, 0, ScriptText.Length));
    }

    private void RefreshSceneOptions(ScriptDocument? script = null)
    {
        if (script is null && _project is not null)
            script = GetOrCreateActiveScript(_project, _activeEpisodeId);
        SceneOptions.Clear();
        if (script is not null)
        {
            ScriptEditingController.SyncElements(script, ScriptText);
            foreach (var scene in script.Elements.Where(element => element.Type == ScriptElementType.SceneHeading))
            {
                var title = string.IsNullOrWhiteSpace(scene.Text) ? "Untitled scene" : scene.Text.Trim();
                SceneOptions.Add(new SceneLinkOption(script.Id, scene.Id, title));
            }
        }
        foreach (var beat in BeatGroups.SelectMany(group => group.Beats))
            beat.RefreshSelectedScene();
    }

    private void RebuildBeatGroups()
    {
        BeatGroups.Clear();
        if (_project is null)
            return;

        var story = _project.Story;
        foreach (var groupData in story.BeatGroups.OrderBy(group => group.Order))
        {
            var group = CreateBeatGroup(groupData);
            foreach (var beat in story.Beats.Where(beat => beat.GroupId == groupData.Id).OrderBy(beat => beat.Order))
                group.Beats.Add(CreateBeatCard(beat));
            BeatGroups.Add(group);
        }

        var ungrouped = story.Beats.Where(beat => beat.GroupId is null || story.BeatGroups.All(group => group.Id != beat.GroupId)).OrderBy(beat => beat.Order).ToList();
        if (ungrouped.Count > 0)
        {
            var groupData = new BeatGroup { Title = "Unassigned", Order = int.MaxValue };
            var group = CreateBeatGroup(groupData);
            foreach (var beat in ungrouped)
                group.Beats.Add(CreateBeatCard(beat));
            BeatGroups.Add(group);
        }
        RefreshBeatGroupChoices();
        RefreshSceneOptions();
    }

    private void RemoveBeatCard(BeatCardViewModel beat)
    {
        if (_project is null)
            return;
        _project.Story.Beats.Remove(beat.Data);
        foreach (var group in BeatGroups)
            group.Beats.Remove(beat);
        MarkUnsaved("Beat removed · unsaved changes");
    }

    [RelayCommand]
    public void AddCharacter()
    {
        EnsureActiveScript();
        var index = _project!.Story.Characters.Count;
        var data = new CharacterProfile
        {
            Name = $"Character {index + 1}",
            MapX = 24 + (index % 2) * 220,
            MapY = 24 + (index / 2) * 140
        };
        _project.Story.Characters.Add(data);
        Characters.Add(CreateCharacterCard(data));
        ApplyCharacterFilter();
        OnPropertyChanged(nameof(CanAddRelationship));
        OnPropertyChanged(nameof(NeedsMoreCharacters));
        RebuildRelationshipCards();
        MarkUnsaved("Character added · unsaved changes");
    }

    private CharacterCardViewModel CreateCharacterCard(CharacterProfile data) =>
        new(data, OnCharacterDetailsChanged, RemoveCharacterCard);

    private void OnCharacterDetailsChanged()
    {
        ApplyCharacterFilter();
        MarkUnsaved("Character details changed · unsaved changes");
    }

    private void ApplyCharacterFilter()
    {
        var query = CharacterSearchText.Trim();
        var matches = Characters.Where(character => string.IsNullOrEmpty(query)
            || character.Data.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || character.Data.Attributes.Any(attribute => attribute.Key.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || attribute.Value.Contains(query, StringComparison.CurrentCultureIgnoreCase))).ToList();
        FilteredCharacters.Clear();
        foreach (var character in matches)
            FilteredCharacters.Add(character);
        OnPropertyChanged(nameof(CharacterSearchHasNoResults));
    }

    private void RebuildCharacterCards()
    {
        Characters.Clear();
        FilteredCharacters.Clear();
        if (_project is not null)
        {
            var characters = _project.Story.Characters;
            if (characters.Count > 0 && characters.All(character => character.MapX == 0 && character.MapY == 0))
            {
                for (var index = 0; index < characters.Count; index++)
                {
                    characters[index].MapX = 24 + (index % 2) * 220;
                    characters[index].MapY = 24 + (index / 2) * 140;
                }
            }
            foreach (var character in _project.Story.Characters)
                Characters.Add(CreateCharacterCard(character));
        }
        ApplyCharacterFilter();
        OnPropertyChanged(nameof(CanAddRelationship));
        OnPropertyChanged(nameof(NeedsMoreCharacters));
        LoadMapState();
        RebuildRelationshipCards();
    }

    private void RemoveCharacterCard(CharacterCardViewModel character)
    {
        if (_project is null)
            return;
        _project.Story.Characters.Remove(character.Data);
        _project.Story.Relationships.RemoveAll(relationship =>
            relationship.FirstCharacterId == character.Data.Id || relationship.SecondCharacterId == character.Data.Id);
        Characters.Remove(character);
        ApplyCharacterFilter();
        OnPropertyChanged(nameof(CanAddRelationship));
        OnPropertyChanged(nameof(NeedsMoreCharacters));
        RebuildRelationshipCards();
        MarkUnsaved("Character removed · unsaved changes");
    }

    [RelayCommand]
    public void AddRelationship()
    {
        if (!CanAddRelationship)
            return;
        EnsureActiveScript();
        var relationship = new CharacterRelationship
        {
            FirstCharacterId = Characters[0].Data.Id,
            SecondCharacterId = Characters[1].Data.Id,
            Label = "knows"
        };
        _project!.Story.Relationships.Add(relationship);
        Relationships.Add(CreateRelationshipCard(relationship));
        OnPropertyChanged(nameof(HasRelationships));
        MarkUnsaved("Relationship added · unsaved changes");
    }

    private RelationshipCardViewModel CreateRelationshipCard(CharacterRelationship data) =>
        new(data, Characters.ToArray(), () => MarkUnsaved("Relationship changed · unsaved changes"), RemoveRelationshipCard);

    private void RebuildRelationshipCards()
    {
        Relationships.Clear();
        if (_project is not null)
            foreach (var relationship in _project.Story.Relationships)
                Relationships.Add(CreateRelationshipCard(relationship));
        OnPropertyChanged(nameof(HasRelationships));
    }

    private void RemoveRelationshipCard(RelationshipCardViewModel relationship)
    {
        if (_project is null)
            return;
        _project.Story.Relationships.Remove(relationship.Data);
        Relationships.Remove(relationship);
        OnPropertyChanged(nameof(HasRelationships));
        MarkUnsaved("Relationship removed · unsaved changes");
    }

    public void SetFormatProfile(string profileId, bool markUnsaved)
    {
        var profile = ScriptFormatProfiles.Get(profileId);
        _selectedFormatProfile = profile;
        OnPropertyChanged(nameof(SelectedFormatProfile));
        RefreshElementOptions();
        InvalidateScriptPages();

        if (_project is not null)
            GetOrCreateActiveScript(_project, _activeEpisodeId).FormatProfile = profile.Id;

        OnPropertyChanged(nameof(IsYouTubeFormat));
        OnPropertyChanged(nameof(IsTelevisionFormat));
        OnPropertyChanged(nameof(IsWritingSurfaceVisible));
        OnPropertyChanged(nameof(IsAudioVisualWritingVisible));
        RefreshAudioVisualWritingRows();

        if (markUnsaved)
            MarkUnsaved("Script format changed · unsaved changes");
    }

    public void ApplyElementTypeToLine(int lineIndex, ScriptElementType type)
    {
        if (!SelectedFormatProfile.Elements.Any(rule => rule.Type == type))
            return;

        var script = EnsureActiveScript();
        ScriptEditingController.SetElementTypeAtLine(script, ScriptText, lineIndex, type);
        SelectedElementOption = ElementOptions.FirstOrDefault(option => option.Type == type);
        RefreshAudioVisualWritingRows();
        InvalidateScriptPages();
        MarkUnsaved("Element format changed · unsaved changes");
    }

    private ScriptDocument EnsureActiveScript()
    {
        _project ??= new ScreenplayProject { Title = ProjectTitle };
        var script = GetOrCreateActiveScript(_project, _activeEpisodeId);
        script.FormatProfile = SelectedFormatProfile.Id;
        ScriptEditingController.SyncElements(script, ScriptText);
        return script;
    }

    public ScriptElementType GetElementTypeAtLine(int lineIndex)
    {
        if (_project is null)
            return ScriptElementType.Action;
        var script = GetOrCreateActiveScript(_project, _activeEpisodeId);
        ScriptEditingController.SyncElements(script, ScriptText);
        return ScriptEditingController.GetElementTypeAtLine(script, lineIndex);
    }

    public int GetElementCount(ScriptElementType type)
    {
        if (_project is null)
            return 0;
        var script = GetOrCreateActiveScript(_project, _activeEpisodeId);
        ScriptEditingController.SyncElements(script, ScriptText);
        return script.Elements.Count(element => element.Type == type);
    }

    public void InsertSuggestion(int lineIndex, string suggestion)
    {
        var lines = ScriptText.Split('\n');
        if (lineIndex < 0 || lineIndex >= lines.Length || _project is null)
            return;
        var hadCarriageReturn = lines[lineIndex].EndsWith('\r');
        lines[lineIndex] = suggestion + (hadCarriageReturn ? "\r" : string.Empty);
        ScriptText = string.Join('\n', lines);
        ApplyElementTypeToLine(lineIndex, SelectedElementOption?.Type ?? ScriptElementType.Action);
    }

    private void RefreshElementOptions()
    {
        ElementOptions.Clear();
        foreach (var rule in _selectedFormatProfile.Elements)
            ElementOptions.Add(new ElementTypeOption(rule.Type, rule.Label));
        SelectedElementOption = ElementOptions.FirstOrDefault();
    }

    private void RefreshSuggestions()
    {
        Suggestions.Clear();
        if (_project is null || SelectedElementOption is null)
            return;

        var script = GetOrCreateActiveScript(_project, _activeEpisodeId);
        ScriptEditingController.SyncElements(script, ScriptText);
        IEnumerable<string> candidates = SelectedElementOption.Type switch
        {
            ScriptElementType.Character => _project.Story.Characters.Select(character => character.Name)
                .Concat(script.Elements.Where(element => element.Type == ScriptElementType.Character).Select(element => element.Text.Trim())),
            ScriptElementType.SceneHeading => script.Elements.Where(element => element.Type == ScriptElementType.SceneHeading).Select(element => element.Text.Trim()),
            _ => []
        };

        foreach (var candidate in candidates.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Take(20))
            Suggestions.Add(new ScriptSuggestion(candidate));
        SelectedSuggestion = Suggestions.FirstOrDefault();
    }

    public async Task CheckForRecoveryAsync()
    {
        try
        {
            var snapshot = await _recoveryStore.LoadAsync();
            CanRecoverSession = snapshot is not null;
            RecoveryMessage = snapshot is null
                ? string.Empty
                : $"Unsaved work from {snapshot.SavedAt.ToLocalTime():g} is available to restore.";
            OnPropertyChanged(nameof(CanRecoverSession));
            OnPropertyChanged(nameof(RecoveryMessage));
        }
        catch (Exception exception)
        {
            SaveStateText = $"Could not check recovery file: {exception.Message}";
        }
    }

    public async Task RecoverSessionAsync()
    {
        var snapshot = await _recoveryStore.LoadAsync();
        if (snapshot is null)
            return;

        _project = snapshot.Project;
        RefreshDriveSyncProperties();
        RebuildBeatGroups();
        RebuildCharacterCards();
        _projectPath = snapshot.SourcePath;
        _activeEpisodeId = snapshot.ActiveEpisodeId;
        ProjectTitle = _project.Kind == ProjectKind.TelevisionSeries
            ? _project.Series?.Title ?? _project.Title
            : _project.Title;
        RebuildSeasonNavigation();
        var script = FindActiveScript(_project, _activeEpisodeId);
        LoadPageSettings(script?.PageSettings ?? new ScriptPageSettings());
        SetPageSize(script?.PageSettings.PaperSize ?? "USLetter", markUnsaved: false);
        ScriptText = script?.Text ?? string.Empty;
        SetFormatProfile(script?.FormatProfile ?? "movie", markUnsaved: false);
        SetActiveEpisode(_activeEpisodeId);
        OnPropertyChanged(nameof(ProjectPath));
        OnPropertyChanged(nameof(IsSeriesProject));
        OnPropertyChanged(nameof(ShowEpisodeContent));
        HasUnsavedChanges = true;
        CanRecoverSession = false;
        LeftPanelMode = IsSeriesProject ? "Episodes" : "Beats";
        OnPropertyChanged(nameof(CanRecoverSession));
        OnPropertyChanged(nameof(IsSeriesProject));
        OnPropertyChanged(nameof(ShowEpisodeContent));
        SaveStateText = "Recovered unsaved work · save when ready";
        ScheduleRecovery();
        if (IsDriveConnected)
            await TrySyncPendingDriveAsync();
    }

    private void ScheduleRecovery()
    {
        if (_project is null)
            return;

        _recoveryDebounce?.Cancel();
        _recoveryDebounce?.Dispose();
        var cancellation = new CancellationTokenSource();
        _recoveryDebounce = cancellation;
        _ = SaveRecoveryAfterDelayAsync(cancellation.Token);
    }

    private async Task SaveRecoveryAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            if (_project is not null && HasUnsavedChanges)
                await _recoveryStore.SaveAsync(_project, _projectPath, _activeEpisodeId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            SaveStateText = $"Changes remain in memory · recovery could not be written: {exception.Message}";
        }
    }

    public void SelectEpisode(Guid episodeId)
    {
        if (_activeEpisodeId == episodeId)
            return;

        var script = FindActiveScript(_project, episodeId);
        if (script is null)
            return;

        _activeEpisodeId = episodeId;
        ScriptText = script.Text;
        LoadPageSettings(script.PageSettings);
        SetPageSize(script.PageSettings.PaperSize, markUnsaved: false);
        SetFormatProfile(script.FormatProfile, markUnsaved: false);
        SetActiveEpisode(episodeId);
        HasUnsavedChanges = true;
        SaveStateText = "Episode selected · unsaved changes";
    }

    public void AddSeason()
    {
        if (_project?.Series is null)
            return;

        var number = _project.Series.Seasons.Count == 0 ? 1 : _project.Series.Seasons.Max(season => season.Number) + 1;
        var season = new SeriesSeason { Number = number, Title = $"Season {number}" };
        _project.Series.Seasons.Add(season);
        RebuildSeasonNavigation();
        LeftPanelMode = "Episodes";
        HasUnsavedChanges = true;
        SaveStateText = "New season · unsaved changes";
        ScheduleRecovery();
    }

    public void AddEpisode(Guid seasonId)
    {
        var season = _project?.Series?.Seasons.FirstOrDefault(item => item.Id == seasonId);
        if (season is null)
            return;

        var number = season.Episodes.Count == 0 ? 1 : season.Episodes.Max(episode => episode.Number) + 1;
        var episode = new SeriesEpisode
        {
            Number = number,
            Title = $"Episode {number}",
            Script = new ScriptDocument { Title = $"Episode {number}", FormatProfile = "tv" }
        };
        season.Episodes.Add(episode);
        RebuildSeasonNavigation();
        _activeEpisodeId = episode.Id;
        ApplyCurrentPageSettings(episode.Script.PageSettings);
        ScriptText = string.Empty;
        SetFormatProfile("tv", markUnsaved: false);
        SetPageSize(_selectedPageSize.Id, markUnsaved: false);
        SetActiveEpisode(episode.Id);
        HasUnsavedChanges = true;
        SaveStateText = "New episode · unsaved changes";
        ScheduleRecovery();
    }

    public void MoveEpisode(Guid episodeId, int direction)
    {
        var season = _project?.Series?.Seasons.FirstOrDefault(item => item.Episodes.Any(episode => episode.Id == episodeId));
        if (season is null)
            return;

        var ordered = season.Episodes.OrderBy(episode => episode.Number).ToList();
        var index = ordered.FindIndex(episode => episode.Id == episodeId);
        var target = index + direction;
        if (index < 0 || target < 0 || target >= ordered.Count)
            return;

        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        for (var i = 0; i < ordered.Count; i++)
            ordered[i].Number = i + 1;
        season.Episodes = ordered;
        RebuildSeasonNavigation();
        SetActiveEpisode(_activeEpisodeId);
        HasUnsavedChanges = true;
        ScheduleRecovery();
    }

    public void MoveSeason(Guid seasonId, int direction)
    {
        if (_project?.Series is null)
            return;

        var ordered = _project.Series.Seasons.OrderBy(season => season.Number).ToList();
        var index = ordered.FindIndex(season => season.Id == seasonId);
        var target = index + direction;
        if (index < 0 || target < 0 || target >= ordered.Count)
            return;

        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        for (var i = 0; i < ordered.Count; i++)
            ordered[i].Number = i + 1;
        _project.Series.Seasons = ordered;
        RebuildSeasonNavigation();
        SetActiveEpisode(_activeEpisodeId);
        HasUnsavedChanges = true;
        ScheduleRecovery();
    }

    private void RebuildSeasonNavigation()
    {
        Seasons.Clear();
        if (_project?.Series is null)
            return;

        foreach (var season in _project.Series.Seasons.OrderBy(item => item.Number))
        {
            var navigationItem = new SeasonNavigationItem(season, () => MarkUnsaved("Unsaved series changes"));
            navigationItem.Changed += ScheduleRecovery;
            foreach (var episode in navigationItem.Episodes)
                episode.Changed += ScheduleRecovery;
            Seasons.Add(navigationItem);
        }
    }

    private void SetActiveEpisode(Guid? episodeId)
    {
        foreach (var episode in Seasons.SelectMany(season => season.Episodes))
            episode.IsActive = episode.Id == episodeId;
    }

    private static ScriptDocument GetOrCreateActiveScript(ScreenplayProject project, Guid? activeEpisodeId)
    {
        if (project.Kind == ProjectKind.StandaloneScript)
            return project.StandaloneScript ??= new ScriptDocument();

        project.Series ??= new SeriesOutline();
        if (project.Series.Seasons.Count == 0)
            project.Series.Seasons.Add(new SeriesSeason());

        var season = project.Series.Seasons
            .FirstOrDefault(item => item.Episodes.Any(episode => episode.Id == activeEpisodeId))
            ?? project.Series.Seasons.OrderBy(item => item.Number).First();
        if (season.Episodes.Count == 0)
            season.Episodes.Add(new SeriesEpisode());

        return season.Episodes.FirstOrDefault(item => item.Id == activeEpisodeId)?.Script
            ?? season.Episodes.OrderBy(item => item.Number).First().Script;
    }

    private static ScriptDocument? FindActiveScript(ScreenplayProject? project, Guid? episodeId)
    {
        if (project is null)
            return null;
        if (project.Kind == ProjectKind.StandaloneScript)
            return project.StandaloneScript;

        var episodes = project.Series?.Seasons.OrderBy(season => season.Number)
            .SelectMany(season => season.Episodes.OrderBy(item => item.Number));
        var episode = episodes?.FirstOrDefault(item => item.Id == episodeId)
            ?? episodes?.FirstOrDefault();

        return episode?.Script;
    }
}
