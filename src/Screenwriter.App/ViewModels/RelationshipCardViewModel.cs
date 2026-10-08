using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Screenwriter.Core.Story;

namespace Screenwriter.App.ViewModels;

public sealed record RelationshipDirectionOption(RelationshipDirection Value, string Label);

public partial class RelationshipCardViewModel : ObservableObject
{
    public static IReadOnlyList<RelationshipDirectionOption> DirectionOptions { get; } =
    [
        new(RelationshipDirection.Mutual, "Mutual"),
        new(RelationshipDirection.FromFirstToSecond, "First → second"),
        new(RelationshipDirection.FromSecondToFirst, "Second → first")
    ];

    private readonly CharacterRelationship _data;
    private readonly Action _markChanged;
    private readonly ICommand _removeCommand;

    public RelationshipCardViewModel(
        CharacterRelationship data,
        IReadOnlyList<CharacterCardViewModel> characters,
        Action markChanged,
        Action<RelationshipCardViewModel> remove)
    {
        _data = data;
        _markChanged = markChanged;
        CharacterChoices = new ObservableCollection<CharacterCardViewModel>(characters);
        _removeCommand = new RelayCommand(() => remove(this));
        Label = data.Label;
        Notes = data.Notes;
        SelectedDirection = DirectionOptions.First(option => option.Value == data.Direction);
        foreach (var character in CharacterChoices)
            character.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(CharacterCardViewModel.Name))
                    OnPropertyChanged(nameof(RelationshipSummary));
                if (args.PropertyName is nameof(CharacterCardViewModel.MapX) or nameof(CharacterCardViewModel.MapY))
                    NotifyMapGeometry();
            };
    }

    public ObservableCollection<CharacterCardViewModel> CharacterChoices { get; }
    public IReadOnlyList<RelationshipDirectionOption> Directions => DirectionOptions;
    public ICommand RemoveCommand => _removeCommand;
    public CharacterRelationship Data => _data;
    public double LineX1 => (FirstCharacter?.MapX ?? 0) + 86;
    public double LineY1 => (FirstCharacter?.MapY ?? 0) + 50;
    public double LineX2 => (SecondCharacter?.MapX ?? 0) + 86;
    public double LineY2 => (SecondCharacter?.MapY ?? 0) + 50;
    public Point LineStart => new(LineX1, LineY1);
    public Point LineEnd => new(LineX2, LineY2);
    public double LabelX => (LineX1 + LineX2) / 2 - 30;
    public double LabelY => (LineY1 + LineY2) / 2 - 10;
    public string MapLabel => $"{(SelectedDirection.Value == RelationshipDirection.Mutual ? "↔" : SelectedDirection.Value == RelationshipDirection.FromFirstToSecond ? "→" : "←")} {Label}";

    public CharacterCardViewModel? FirstCharacter
    {
        get => CharacterChoices.FirstOrDefault(character => character.Data.Id == _data.FirstCharacterId);
        set
        {
            if (value is null || _data.FirstCharacterId == value.Data.Id)
                return;
            _data.FirstCharacterId = value.Data.Id;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RelationshipSummary));
            NotifyMapGeometry();
            _markChanged();
        }
    }

    public CharacterCardViewModel? SecondCharacter
    {
        get => CharacterChoices.FirstOrDefault(character => character.Data.Id == _data.SecondCharacterId);
        set
        {
            if (value is null || _data.SecondCharacterId == value.Data.Id)
                return;
            _data.SecondCharacterId = value.Data.Id;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RelationshipSummary));
            NotifyMapGeometry();
            _markChanged();
        }
    }

    [ObservableProperty]
    public partial string Label { get; set; }

    [ObservableProperty]
    public partial string Notes { get; set; }

    [ObservableProperty]
    public partial RelationshipDirectionOption SelectedDirection { get; set; }

    public string RelationshipSummary
    {
        get
        {
            var first = FirstCharacter?.Name ?? "Character";
            var second = SecondCharacter?.Name ?? "Character";
            var arrow = SelectedDirection.Value switch
            {
                RelationshipDirection.FromFirstToSecond => "→",
                RelationshipDirection.FromSecondToFirst => "←",
                _ => "↔"
            };
            return $"{first}  {arrow}  {second}";
        }
    }

    partial void OnLabelChanged(string value)
    {
        _data.Label = value;
        OnPropertyChanged(nameof(RelationshipSummary));
        OnPropertyChanged(nameof(MapLabel));
        _markChanged();
    }

    partial void OnNotesChanged(string value)
    {
        _data.Notes = value;
        _markChanged();
    }

    partial void OnSelectedDirectionChanged(RelationshipDirectionOption value)
    {
        _data.Direction = value.Value;
        OnPropertyChanged(nameof(RelationshipSummary));
        OnPropertyChanged(nameof(MapLabel));
        _markChanged();
    }

    private void NotifyMapGeometry()
    {
        OnPropertyChanged(nameof(LineX1));
        OnPropertyChanged(nameof(LineY1));
        OnPropertyChanged(nameof(LineX2));
        OnPropertyChanged(nameof(LineY2));
        OnPropertyChanged(nameof(LineStart));
        OnPropertyChanged(nameof(LineEnd));
        OnPropertyChanged(nameof(LabelX));
        OnPropertyChanged(nameof(LabelY));
    }
}
