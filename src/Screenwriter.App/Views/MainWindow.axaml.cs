using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit;
using Screenwriter.Core.Documents;
using Screenwriter.Editor;
using Screenwriter.App.ViewModels;
using Screenwriter.Export;

namespace Screenwriter.App.Views;

public partial class MainWindow : Window
{
    private TextEditor? _attachedEditor;
    private MainViewModel? _attachedViewModel;
    private AudioVisualWritingRowViewModel? _activeAudioVisualRow;
    private TextBox? _activeAudioVisualTextBox;
    private bool _activeAudioVisualRightColumn;
    private CharacterCardViewModel? _draggingMapCharacter;
    private Point _mapDragStart;
    private double _mapCharacterStartX;
    private double _mapCharacterStartY;
    private bool _isPanningRelationshipMap;
    private Point _mapPanStart;
    private double _mapPanStartX;
    private double _mapPanStartY;
    private readonly DispatcherTimer _pageIndicatorTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };

    public MainWindow()
    {
        InitializeComponent();
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        Closed += (_, _) => NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        _pageIndicatorTimer.Tick += (_, _) =>
        {
            _pageIndicatorTimer.Stop();
            if (_attachedEditor is { } activeEditor && _attachedViewModel is { } activeViewModel)
                activeViewModel.UpdateCurrentPageIndicator(activeEditor.CaretOffset);
        };
        DataContextChanged += (_, _) => AttachEditor();
        Opened += async (_, _) =>
        {
            if (DataContext is MainViewModel viewModel)
            {
                await viewModel.CheckForRecoveryAsync();
                await viewModel.InitializeDriveConnectionAsync();
                await viewModel.FlushPendingAnalyticsAsync();
            }
        };
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs args)
    {
        if (!args.IsAvailable)
            return;
        Dispatcher.UIThread.Post(async () =>
        {
            if (DataContext is MainViewModel viewModel)
            {
                await viewModel.FlushPendingAnalyticsAsync();
                await viewModel.RetryPendingDriveSyncAsync();
            }
        });
    }

    private void AttachEditor()
    {
        var editor = this.FindControl<TextEditor>("ScriptEditor");
        if (editor is not null && DataContext is MainViewModel viewModel
            && (!ReferenceEquals(editor, _attachedEditor) || !ReferenceEquals(viewModel, _attachedViewModel)))
        {
            _attachedEditor = editor;
            _attachedViewModel = viewModel;
            viewModel.AudioVisualWritingRows.CollectionChanged += (_, _) =>
            {
                _activeAudioVisualRow = null;
                _activeAudioVisualTextBox = null;
            };
            editor.Text = viewModel.ScriptText;
            editor.TextChanged += (_, _) => viewModel.ScriptText = editor.Text;
            editor.AddHandler(InputElement.KeyDownEvent, Editor_KeyDown, RoutingStrategies.Tunnel);
            editor.TextArea.Caret.PositionChanged += (_, _) =>
            {
                _pageIndicatorTimer.Stop();
                _pageIndicatorTimer.Start();
            };
            viewModel.UpdateCurrentPageIndicator(editor.CaretOffset);
            viewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(viewModel.ScriptText) && editor.Text != viewModel.ScriptText)
                    editor.Text = viewModel.ScriptText;
            };
            viewModel.NavigateToSceneRequested += offset => Dispatcher.UIThread.Post(() =>
            {
                if (editor.Document is null)
                    return;
                editor.CaretOffset = Math.Clamp(offset, 0, editor.Document.TextLength);
                var line = editor.Document.GetLineByOffset(editor.CaretOffset);
                editor.ScrollToLine(line.LineNumber);
                editor.Focus();
                viewModel.UpdateCurrentPageIndicator(editor.CaretOffset);
            });
        }
    }

    private void ApplyElementType_Click(object? sender, RoutedEventArgs args)
    {
        if (_attachedViewModel is not { SelectedElementOption: { } option } viewModel)
            return;

        if (viewModel.IsYouTubeFormat && _activeAudioVisualRow is { } audioVisualRow)
        {
            var lineIndex = audioVisualRow.IsFullWidth
                ? audioVisualRow.FullLineIndex
                : _activeAudioVisualRightColumn ? audioVisualRow.RightLineIndex : audioVisualRow.LeftLineIndex;
            if (lineIndex >= 0)
                viewModel.ApplyElementTypeToLine(lineIndex, option.Type);
            _activeAudioVisualRow = null;
            return;
        }

        if (_attachedEditor is not { } editor)
            return;
        var line = editor.Document.GetLineByOffset(editor.CaretOffset);
        viewModel.ApplyElementTypeToLine(line.LineNumber - 1, option.Type);
        editor.Focus();
    }

    private void InsertActBreak_Click(object? sender, RoutedEventArgs args)
    {
        if (_attachedEditor is not { } editor || _attachedViewModel is not { IsTelevisionFormat: true } viewModel)
            return;

        var actCount = viewModel.GetElementCount(ScriptElementType.ActHeading) + 1;
        var heading = actCount switch
        {
            1 => "ACT ONE",
            2 => "ACT TWO",
            3 => "ACT THREE",
            4 => "ACT FOUR",
            5 => "ACT FIVE",
            _ => $"ACT {actCount}"
        };

        var currentLine = editor.Document.GetLineByOffset(editor.CaretOffset);
        var insertOffset = currentLine.Offset;
        editor.Document.Insert(insertOffset, $"{heading}{Environment.NewLine}");
        editor.CaretOffset = insertOffset + heading.Length;
        viewModel.ApplyElementTypeToLine(currentLine.LineNumber - 1, ScriptElementType.ActHeading);
        editor.Focus();
    }

    private void AudioVisualInput_GotFocus(object? sender, RoutedEventArgs args)
    {
        if (sender is not TextBox { DataContext: AudioVisualWritingRowViewModel row } input)
            return;

        _activeAudioVisualTextBox = input;
        _activeAudioVisualRow = row;
        _activeAudioVisualRightColumn = string.Equals(input.Tag as string, "visual", StringComparison.Ordinal);
    }

    private void AudioVisualInput_TextChanged(object? sender, TextChangedEventArgs args)
    {
        if (sender is not TextBox { DataContext: AudioVisualWritingRowViewModel row } input || _attachedViewModel is not { } viewModel)
            return;

        var isVisual = string.Equals(input.Tag as string, "visual", StringComparison.Ordinal);
        viewModel.UpdateAudioVisualWritingText(row, isVisual, input.Text ?? string.Empty);
    }

    private void MapCharacter_PointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (sender is not Border { DataContext: CharacterCardViewModel character } card
            || this.FindControl<Canvas>("RelationshipMapCanvas") is not { } canvas)
            return;
        _draggingMapCharacter = character;
        _mapDragStart = args.GetPosition(canvas);
        _mapCharacterStartX = character.MapX;
        _mapCharacterStartY = character.MapY;
        args.Pointer.Capture(card);
        args.Handled = true;
    }

    private void MapCharacter_PointerMoved(object? sender, PointerEventArgs args)
    {
        if (_draggingMapCharacter is not { } character
            || this.FindControl<Canvas>("RelationshipMapCanvas") is not { } canvas)
            return;
        var current = args.GetPosition(canvas);
        character.SetMapPosition(_mapCharacterStartX + current.X - _mapDragStart.X,
            _mapCharacterStartY + current.Y - _mapDragStart.Y, commit: false);
        args.Handled = true;
    }

    private void MapCharacter_PointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (_draggingMapCharacter is not { } character)
            return;
        character.CommitMapPosition();
        _draggingMapCharacter = null;
        args.Pointer.Capture(null);
        args.Handled = true;
    }

    private void RelationshipMapCanvas_PointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (sender is not Canvas canvas || !ReferenceEquals(args.Source, canvas)
            || !args.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed
            || DataContext is not MainViewModel viewModel)
            return;
        _isPanningRelationshipMap = true;
        _mapPanStart = args.GetPosition(canvas);
        _mapPanStartX = viewModel.RelationshipMapPanX;
        _mapPanStartY = viewModel.RelationshipMapPanY;
        args.Pointer.Capture(canvas);
        args.Handled = true;
    }

    private void RelationshipMapCanvas_PointerMoved(object? sender, PointerEventArgs args)
    {
        if (!_isPanningRelationshipMap || sender is not Canvas canvas || DataContext is not MainViewModel viewModel)
            return;
        var current = args.GetPosition(canvas);
        viewModel.SetMapPan(_mapPanStartX + current.X - _mapPanStart.X,
            _mapPanStartY + current.Y - _mapPanStart.Y, commit: false);
        args.Handled = true;
    }

    private void RelationshipMapCanvas_PointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (!_isPanningRelationshipMap || DataContext is not MainViewModel viewModel)
            return;
        _isPanningRelationshipMap = false;
        viewModel.SetMapPan(viewModel.RelationshipMapPanX, viewModel.RelationshipMapPanY, commit: true);
        args.Pointer.Capture(null);
        args.Handled = true;
    }

    private void InsertSuggestion_Click(object? sender, RoutedEventArgs args)
    {
        if (_attachedEditor is not { } editor || _attachedViewModel is not { SelectedSuggestion: { } suggestion } viewModel)
            return;
        var line = editor.Document.GetLineByOffset(editor.CaretOffset);
        editor.Document.Replace(line.Offset, line.Length, suggestion.Text);
        editor.CaretOffset = line.Offset + suggestion.Text.Length;
        viewModel.ApplyElementTypeToLine(line.LineNumber - 1, viewModel.SelectedElementOption?.Type ?? ScriptElementType.Action);
        editor.Focus();
    }

    private void Editor_KeyDown(object? sender, KeyEventArgs args)
    {
        if (_attachedEditor is not { } editor || _attachedViewModel is not { } viewModel)
            return;

        if (args.Key == Key.Tab)
        {
            var line = editor.Document.GetLineByOffset(editor.CaretOffset);
            var lineIndex = line.LineNumber - 1;
            var current = viewModel.GetElementTypeAtLine(lineIndex);
            var next = ScriptEditingController.GetNextElementType(viewModel.SelectedFormatProfile, current, args.KeyModifiers.HasFlag(KeyModifiers.Shift));
            viewModel.ApplyElementTypeToLine(lineIndex, next);
            viewModel.SelectedElementOption = viewModel.ElementOptions.FirstOrDefault(option => option.Type == next);
            args.Handled = true;
            return;
        }

        if (args.Key == Key.Enter && editor.SelectionLength == 0)
        {
            var offset = editor.CaretOffset;
            var line = editor.Document.GetLineByOffset(offset);
            var lineIndex = line.LineNumber - 1;
            var current = viewModel.GetElementTypeAtLine(lineIndex);
            editor.Document.Insert(offset, "\n");
            editor.CaretOffset = offset + 1;
            var next = ScriptEditingController.GetElementAfterEnter(current);
            viewModel.ApplyElementTypeToLine(lineIndex + 1, next);
            viewModel.SelectedElementOption = viewModel.ElementOptions.FirstOrDefault(option => option.Type == next);
            args.Handled = true;
        }
    }

    private void Undo_Click(object? sender, RoutedEventArgs args)
    {
        if (_attachedViewModel is { IsAudioVisualWritingVisible: true } && _activeAudioVisualTextBox?.CanUndo == true)
            _activeAudioVisualTextBox.Undo();
        else if (_attachedEditor?.Document.UndoStack.CanUndo == true)
            _attachedEditor.Document.UndoStack.Undo();
    }

    private void Redo_Click(object? sender, RoutedEventArgs args)
    {
        if (_attachedViewModel is { IsAudioVisualWritingVisible: true } && _activeAudioVisualTextBox?.CanRedo == true)
            _activeAudioVisualTextBox.Redo();
        else if (_attachedEditor?.Document.UndoStack.CanRedo == true)
            _attachedEditor.Document.UndoStack.Redo();
    }

    private void ToggleFindReplace_Click(object? sender, RoutedEventArgs args)
    {
        if (_attachedViewModel is { } viewModel)
        {
            viewModel.IsFindReplaceVisible = !viewModel.IsFindReplaceVisible;
            if (viewModel.IsFindReplaceVisible)
                this.FindControl<TextBox>("FindInput")?.Focus();
        }
    }

    private void EditPreviewPage_Click(object? sender, RoutedEventArgs args)
    {
        if (_attachedEditor is not { } editor || _attachedViewModel is not { } viewModel
            || sender is not Button { Tag: int startOffset })
            return;

        viewModel.IsPagePreviewVisible = false;
        Dispatcher.UIThread.Post(() =>
        {
            var offset = Math.Clamp(startOffset, 0, editor.Document.TextLength);
            editor.CaretOffset = offset;
            editor.ScrollToLine(editor.Document.GetLineByOffset(offset).LineNumber);
            editor.Focus();
        });
    }

    private void PreviousPage_Click(object? sender, RoutedEventArgs args) => NavigateScriptPage(-1);

    private void NextPage_Click(object? sender, RoutedEventArgs args) => NavigateScriptPage(1);

    private void NavigateScriptPage(int direction)
    {
        if (_attachedViewModel is not { } viewModel)
            return;

        if (viewModel.IsPagePreviewVisible)
        {
            viewModel.NavigatePreviewPage(direction);
            return;
        }

        if (_attachedEditor is not { } editor)
            return;

        var offset = viewModel.GetAdjacentScriptPageOffset(editor.CaretOffset, direction);
        editor.CaretOffset = Math.Clamp(offset, 0, editor.Document.TextLength);
        editor.ScrollToLine(editor.Document.GetLineByOffset(editor.CaretOffset).LineNumber);
        editor.Focus();
        viewModel.UpdateCurrentPageIndicator(editor.CaretOffset);
    }

    private void FindNext_Click(object? sender, RoutedEventArgs args) => FindNext();

    private void ReplaceSpelling_Click(object? sender, RoutedEventArgs args)
    {
        if (_attachedEditor is not { } editor
            || _attachedViewModel is not { SelectedSpellingIssue: { } issue, SelectedSpellingSuggestion: { } suggestion } viewModel)
            return;

        editor.Document.Replace(issue.Offset, issue.Word.Length, suggestion);
        viewModel.CheckSpellingCommand.Execute(null);
        editor.Focus();
    }

    private void SpellingIssue_SelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (sender is not ComboBox { SelectedItem: SpellingIssue issue } || _attachedEditor is not { } editor)
            return;

        editor.Select(issue.Offset, issue.Word.Length);
        editor.ScrollToLine(editor.Document.GetLineByOffset(issue.Offset).LineNumber);
        editor.Focus();
    }

    private void IgnoreSpelling_Click(object? sender, RoutedEventArgs args)
    {
        if (_attachedViewModel is { SelectedSpellingIssue: { } issue } viewModel)
            viewModel.IgnoreSpellingWord(issue.Word);
    }

    private void Replace_Click(object? sender, RoutedEventArgs args)
    {
        if (_attachedEditor is not { } editor || _attachedViewModel is not { } viewModel || string.IsNullOrEmpty(viewModel.FindQuery))
            return;

        if (editor.SelectionLength > 0 && editor.SelectedText.Equals(viewModel.FindQuery, StringComparison.CurrentCultureIgnoreCase))
            editor.Document.Replace(editor.SelectionStart, editor.SelectionLength, viewModel.ReplaceText);
        FindNext();
    }

    private void FindNext()
    {
        if (_attachedEditor is not { } editor || _attachedViewModel is not { } viewModel || string.IsNullOrEmpty(viewModel.FindQuery))
            return;

        var start = editor.SelectionStart + editor.SelectionLength;
        var index = editor.Text.IndexOf(viewModel.FindQuery, start, StringComparison.CurrentCultureIgnoreCase);
        if (index < 0 && start > 0)
            index = editor.Text.IndexOf(viewModel.FindQuery, StringComparison.CurrentCultureIgnoreCase);
        if (index < 0)
        {
            viewModel.SaveStateText = "No further matches found.";
            return;
        }

        editor.Select(index, viewModel.FindQuery.Length);
        editor.ScrollToLine(editor.Document.GetLineByOffset(index).LineNumber);
        editor.Focus();
    }

    private async void SaveProject_Click(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        try
        {
            var path = viewModel.ProjectPath;
            if (path is null)
                path = await ChooseProjectPathAsync(viewModel);

            if (path is not null)
                await viewModel.SaveProjectAsync(path);
        }
        catch (Exception exception)
        {
            viewModel.SaveStateText = $"Could not save project: {exception.Message}";
        }
    }

    private async void SaveAsProject_Click(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        try
        {
            var path = await ChooseProjectPathAsync(viewModel);
            if (path is not null)
                await viewModel.SaveProjectAsync(path);
        }
        catch (Exception exception)
        {
            viewModel.SaveStateText = $"Could not save project: {exception.Message}";
        }
    }

    private async Task<string?> ChooseProjectPathAsync(MainViewModel viewModel)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Draftroom project",
            SuggestedFileName = $"{viewModel.ProjectTitle}.draftroom",
            DefaultExtension = "draftroom",
            FileTypeChoices = [new FilePickerFileType("Draftroom project") { Patterns = ["*.draftroom"] }]
        });

        if (file is null)
            return null;

        var path = StorageProviderExtensions.TryGetLocalPath(file);
        if (path is null)
            viewModel.SaveStateText = "Choose a local folder for this project.";
        return path;
    }

    private async void ExportPdf_Click(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        try
        {
            var suggestedName = string.Concat(viewModel.ProjectTitle.Split(Path.GetInvalidFileNameChars())).Trim();
            if (string.IsNullOrWhiteSpace(suggestedName))
                suggestedName = "Screenplay";
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export screenplay as PDF",
                SuggestedFileName = $"{suggestedName}.pdf",
                DefaultExtension = "pdf",
                FileTypeChoices = [new FilePickerFileType("PDF document") { Patterns = ["*.pdf"] }]
            });
            if (file is null)
                return;

            var path = StorageProviderExtensions.TryGetLocalPath(file);
            if (path is null)
            {
                viewModel.SaveStateText = "Choose a local folder for the PDF.";
                return;
            }

            await PdfScriptExporter.ExportAsync(viewModel.GetDocumentForExport(), viewModel.SelectedFormatProfile, path);
            viewModel.TrackAnalyticsFeature(Screenwriter.Infrastructure.Analytics.AnalyticsFeature.PdfExport);
            viewModel.SaveStateText = $"PDF exported · {Path.GetFileName(path)}";
        }
        catch (Exception exception)
        {
            viewModel.SaveStateText = $"Could not export PDF: {exception.Message}";
        }
    }

    private void NewProject_Click(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        if (viewModel.HasUnsavedChanges)
        {
            viewModel.SaveStateText = "Save current work before starting a new project.";
            return;
        }

        viewModel.StartNewProject();
    }

    private async void RecoverSession_Click(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        try
        {
            await viewModel.RecoverSessionAsync();
        }
        catch (Exception exception)
        {
            viewModel.SaveStateText = $"Could not recover work: {exception.Message}";
        }
    }

    private void NewSeries_Click(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        if (viewModel.HasUnsavedChanges)
        {
            viewModel.SaveStateText = "Save current work before starting a new series.";
            return;
        }

        viewModel.StartNewSeries();
    }

    private void AddSeason_Click(object? sender, RoutedEventArgs args)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.AddSeason();
    }

    private void AddEpisode_Click(object? sender, RoutedEventArgs args)
    {
        if (DataContext is MainViewModel viewModel && GetTagId(sender) is { } seasonId)
            viewModel.AddEpisode(seasonId);
    }

    private void OpenEpisode_Click(object? sender, RoutedEventArgs args)
    {
        if (DataContext is MainViewModel viewModel && GetTagId(sender) is { } episodeId)
            viewModel.SelectEpisode(episodeId);
    }

    private void MoveEpisodeUp_Click(object? sender, RoutedEventArgs args) => MoveEpisode(sender, -1);
    private void MoveEpisodeDown_Click(object? sender, RoutedEventArgs args) => MoveEpisode(sender, 1);

    private void MoveEpisode(object? sender, int direction)
    {
        if (DataContext is MainViewModel viewModel && GetTagId(sender) is { } episodeId)
            viewModel.MoveEpisode(episodeId, direction);
    }

    private void MoveSeasonUp_Click(object? sender, RoutedEventArgs args) => MoveSeason(sender, -1);
    private void MoveSeasonDown_Click(object? sender, RoutedEventArgs args) => MoveSeason(sender, 1);

    private void MoveSeason(object? sender, int direction)
    {
        if (DataContext is MainViewModel viewModel && GetTagId(sender) is { } seasonId)
            viewModel.MoveSeason(seasonId, direction);
    }

    private static Guid? GetTagId(object? sender) => sender is Button { Tag: Guid id } ? id : null;

    private async void OpenProject_Click(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        if (viewModel.HasUnsavedChanges)
        {
            viewModel.SaveStateText = "Save current work before opening another project.";
            return;
        }

        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Draftroom project",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Draftroom project") { Patterns = ["*.draftroom"] }]
            });

            if (files.Count > 0 && StorageProviderExtensions.TryGetLocalPath(files[0]) is { } path)
                await viewModel.OpenProjectAsync(path);
        }
        catch (Exception exception)
        {
            viewModel.SaveStateText = $"Could not open project: {exception.Message}";
        }
    }
}
