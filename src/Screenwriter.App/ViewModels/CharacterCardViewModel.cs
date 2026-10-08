using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Screenwriter.Core.Story;

namespace Screenwriter.App.ViewModels;

public partial class CharacterCardViewModel : ObservableObject
{
    private readonly Action _markChanged;
    private readonly ICommand _addAttributeCommand;
    public CharacterCardViewModel(CharacterProfile data, Action markChanged, Action<CharacterCardViewModel> remove)
    {
        Data = data;
        _markChanged = markChanged;
        Name = data.Name;
        RemoveCommand = new RelayCommand(() => remove(this));
        _addAttributeCommand = new RelayCommand(AddAttribute);
        MapX = data.MapX;
        MapY = data.MapY;
        Attributes = [];
        foreach (var pair in data.Attributes.OrderBy(pair => pair.Key))
            AddAttributeRow(pair.Key, pair.Value);
    }

    public CharacterProfile Data { get; }
    public ObservableCollection<CharacterAttributeViewModel> Attributes { get; }
    public ICommand RemoveCommand { get; }
    public ICommand AddAttributeCommand => _addAttributeCommand;

    public double MapX { get; private set; }
    public double MapY { get; private set; }

    [ObservableProperty]
    public partial string Name { get; set; }

    partial void OnNameChanged(string value)
    {
        Data.Name = value;
        _markChanged();
    }

    private void AddAttribute()
    {
        var number = 1;
        var key = "Attribute";
        while (Data.Attributes.ContainsKey(key))
            key = $"Attribute {++number}";
        Data.Attributes[key] = string.Empty;
        AddAttributeRow(key, string.Empty);
        _markChanged();
    }

    private void AddAttributeRow(string key, string value)
    {
        var row = new CharacterAttributeViewModel(Data, key, value, _markChanged);
        row.Removed += removed => Attributes.Remove(removed);
        Attributes.Add(row);
    }

    public void SetMapPosition(double x, double y, bool commit)
    {
        x = Math.Clamp(x, 0, 620 - 172);
        y = Math.Clamp(y, 0, 500 - 100);
        var changed = MapX != x || MapY != y;
        if (!changed)
            return;
        MapX = x;
        MapY = y;
        Data.MapX = x;
        Data.MapY = y;
        OnPropertyChanged(nameof(MapX));
        OnPropertyChanged(nameof(MapY));
        if (commit)
            _markChanged();
    }

    public void CommitMapPosition() => _markChanged();
}

public partial class CharacterAttributeViewModel : ObservableObject
{
    private readonly CharacterProfile _character;
    private readonly Action _markChanged;
    private string _key;
    private string _value;

    public CharacterAttributeViewModel(CharacterProfile character, string key, string value, Action markChanged)
    {
        _character = character;
        _key = key;
        _value = value;
        _markChanged = markChanged;
        RemoveCommand = new RelayCommand(Remove);
    }

    public ICommand RemoveCommand { get; }

    public string Key
    {
        get => _key;
        set
        {
            var next = value.Trim();
            if (string.IsNullOrWhiteSpace(next) || next.Equals(_key, StringComparison.OrdinalIgnoreCase)
                || _character.Attributes.ContainsKey(next))
                return;
            _character.Attributes.Remove(_key);
            _key = next;
            _character.Attributes[_key] = _value;
            OnPropertyChanged();
            _markChanged();
        }
    }

    public string Value
    {
        get => _value;
        set
        {
            if (!SetProperty(ref _value, value))
                return;
            _character.Attributes[_key] = value;
            _markChanged();
        }
    }

    private void Remove()
    {
        _character.Attributes.Remove(_key);
        Removed?.Invoke(this);
        _markChanged();
    }

    public event Action<CharacterAttributeViewModel>? Removed;
}
