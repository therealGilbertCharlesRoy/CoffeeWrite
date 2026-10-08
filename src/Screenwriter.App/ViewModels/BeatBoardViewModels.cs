using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Screenwriter.Core.Story;

namespace Screenwriter.App.ViewModels;

public partial class BeatGroupViewModel : ObservableObject
{
    private readonly Action _changed;
    private readonly Action<BeatGroupViewModel, int> _move;

    public BeatGroupViewModel(BeatGroup data, Action changed, Action<BeatGroupViewModel, int> move)
    {
        Data = data;
        _changed = changed;
        _move = move;
        Title = data.Title;
        MoveUpCommand = new RelayCommand(() => _move(this, -1));
        MoveDownCommand = new RelayCommand(() => _move(this, 1));
    }

    public BeatGroup Data { get; }
    public ObservableCollection<BeatCardViewModel> Beats { get; } = [];
    public IRelayCommand MoveUpCommand { get; }
    public IRelayCommand MoveDownCommand { get; }

    [ObservableProperty]
    public partial string Title { get; set; }

    partial void OnTitleChanged(string value)
    {
        Data.Title = value;
        _changed();
    }
}

public partial class BeatCardViewModel : ObservableObject
{
    private readonly Action _changed;
    private readonly ObservableCollection<SceneLinkOption> _sceneOptions;
    private readonly Action<SceneReference> _navigate;
    private readonly Action<BeatCardViewModel, int> _move;
    private readonly ObservableCollection<BeatGroupChoice> _groupChoices;
    private readonly Action<BeatCardViewModel, Guid?> _moveToGroup;
    private BeatGroupChoice? _selectedGroup;
    private BeatColorChoice? _selectedColor;
    private SceneLinkOption? _selectedScene;
    private readonly RelayCommand _navigateCommand;

    public BeatCardViewModel(BeatCard data, ObservableCollection<SceneLinkOption> sceneOptions, ObservableCollection<BeatGroupChoice> groupChoices, Action changed, Action<BeatCardViewModel> remove, Action<SceneReference> navigate, Action<BeatCardViewModel, int> move, Action<BeatCardViewModel, Guid?> moveToGroup)
    {
        Data = data;
        _sceneOptions = sceneOptions;
        _navigate = navigate;
        _move = move;
        _groupChoices = groupChoices;
        _moveToGroup = moveToGroup;
        _changed = changed;
        Title = data.Title;
        Notes = data.Notes;
        Label = data.Label ?? string.Empty;
        RemoveCommand = new RelayCommand(() => remove(this));
        _navigateCommand = new RelayCommand(() => { if (Data.SceneLink is { } link) _navigate(link); }, () => Data.SceneLink is not null);
        MoveUpCommand = new RelayCommand(() => _move(this, -1));
        MoveDownCommand = new RelayCommand(() => _move(this, 1));
        ColorChoices = [new("Sage", "#B9CCBA"), new("Blue", "#B8C8D8"), new("Amber", "#E4D1A6"), new("Rose", "#E0C1BD"), new("Lavender", "#CDC4DD")];
        _selectedColor = ColorChoices.FirstOrDefault(choice => choice.Hex == data.Color);
        RefreshSelectedScene();
    }

    public BeatCard Data { get; }
    public IRelayCommand RemoveCommand { get; }
    public IRelayCommand NavigateCommand => _navigateCommand;
    public IRelayCommand MoveUpCommand { get; }
    public IRelayCommand MoveDownCommand { get; }
    public bool CanNavigateToScene => Data.SceneLink is not null;
    public ObservableCollection<SceneLinkOption> SceneOptions => _sceneOptions;
    public ObservableCollection<BeatGroupChoice> GroupChoices => _groupChoices;
    public IReadOnlyList<BeatColorChoice> ColorChoices { get; }
    public BeatGroupChoice? SelectedGroup
    {
        get => _groupChoices.FirstOrDefault(option => option.Id == Data.GroupId);
        set
        {
        if (ReferenceEquals(_selectedGroup, value) || _selectedGroup?.Id == value?.Id)
            return;
        if (value?.Id == Data.GroupId)
        {
            _selectedGroup = value;
            return;
        }
            _selectedGroup = value;
            _moveToGroup(this, value?.Id);
            OnPropertyChanged();
        }
    }
    public void RefreshSelectedGroup() => OnPropertyChanged(nameof(SelectedGroup));
    public BeatColorChoice? SelectedColor
    {
        get => _selectedColor;
        set
        {
            if (_selectedColor == value)
                return;
            _selectedColor = value;
            Data.Color = value?.Hex;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ColorBrush));
            _changed();
        }
    }
    public IBrush ColorBrush => _selectedColor is null ? Brushes.Transparent : new SolidColorBrush(Color.Parse(_selectedColor.Hex));

    public SceneLinkOption? SelectedScene
    {
        get => _selectedScene;
        set
        {
            if (ReferenceEquals(_selectedScene, value))
                return;
            _selectedScene = value;
            Data.SceneLink = value is null ? null : new SceneReference { ScriptDocumentId = value.DocumentId, SceneId = value.SceneId, AnchorText = value.Title };
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanNavigateToScene));
            _navigateCommand.NotifyCanExecuteChanged();
            _changed();
        }
    }

    public void RefreshSelectedScene()
    {
        var link = Data.SceneLink;
        var selected = link is null ? null : _sceneOptions.FirstOrDefault(option => option.DocumentId == link.ScriptDocumentId && option.SceneId == link.SceneId);
        if (ReferenceEquals(_selectedScene, selected))
            return;
        _selectedScene = selected;
        OnPropertyChanged(nameof(SelectedScene));
        OnPropertyChanged(nameof(CanNavigateToScene));
        _navigateCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string Notes { get; set; }

    [ObservableProperty]
    public partial string Label { get; set; }

    partial void OnTitleChanged(string value) { Data.Title = value; _changed(); }
    partial void OnNotesChanged(string value) { Data.Notes = value; _changed(); }
    partial void OnLabelChanged(string value) { Data.Label = string.IsNullOrWhiteSpace(value) ? null : value; _changed(); }
}

public sealed record SceneLinkOption(Guid DocumentId, Guid SceneId, string Title);
public sealed class BeatGroupChoice(Guid id, string title) : ObservableObject
{
    public Guid Id { get; } = id;
    private string _title = title;
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
}
public sealed record BeatColorChoice(string Title, string Hex);
