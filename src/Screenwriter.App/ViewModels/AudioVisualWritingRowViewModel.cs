using CommunityToolkit.Mvvm.ComponentModel;

namespace Screenwriter.App.ViewModels;

public partial class AudioVisualWritingRowViewModel : ObservableObject
{
    public AudioVisualWritingRowViewModel(int leftLineIndex, int rightLineIndex, int fullLineIndex, bool isFullWidth, string leftText, string rightText, string fullText)
    {
        LeftLineIndex = leftLineIndex;
        RightLineIndex = rightLineIndex;
        FullLineIndex = fullLineIndex;
        IsFullWidth = isFullWidth;
        LeftText = leftText;
        RightText = rightText;
        FullText = fullText;
    }

    public int LeftLineIndex { get; }
    public int RightLineIndex { get; }
    public int FullLineIndex { get; }
    public bool IsFullWidth { get; }
    public bool HasLeftLine => IsFullWidth || LeftLineIndex >= 0;
    public bool HasRightLine => RightLineIndex >= 0;
    public bool HasRightColumn => !IsFullWidth;
    public int LeftColumnSpan => IsFullWidth ? 2 : 1;
    public string LeftPlaceholder => IsFullWidth ? "Direction or section heading" : "Audio";
    public string RightPlaceholder => "Visual";

    private string _leftText = string.Empty;
    public string LeftText
    {
        get => _leftText;
        set
        {
            if (SetProperty(ref _leftText, value))
                OnPropertyChanged(nameof(LeftEditorText));
        }
    }

    private string _fullText = string.Empty;
    public string FullText
    {
        get => _fullText;
        set
        {
            if (SetProperty(ref _fullText, value))
                OnPropertyChanged(nameof(LeftEditorText));
        }
    }

    private string _rightText = string.Empty;
    public string RightText
    {
        get => _rightText;
        set => SetProperty(ref _rightText, value);
    }

    public string LeftEditorText => IsFullWidth ? FullText : LeftText;
}
