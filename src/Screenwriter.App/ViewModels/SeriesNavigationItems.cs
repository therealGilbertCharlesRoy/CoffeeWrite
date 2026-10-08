using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Screenwriter.Core.Documents;
using Screenwriter.Core.Projects;

namespace Screenwriter.App.ViewModels;

public sealed record ElementTypeOption(ScriptElementType Type, string Label);
public sealed record ScriptSuggestion(string Text);

public partial class SeasonNavigationItem : ObservableObject
{
    private readonly Action _markChanged;

    public SeasonNavigationItem(SeriesSeason data, Action markChanged)
    {
        Data = data;
        _markChanged = markChanged;
        Title = data.Title;
        Episodes = new ObservableCollection<EpisodeNavigationItem>(
            data.Episodes.OrderBy(episode => episode.Number).Select(episode => new EpisodeNavigationItem(episode, markChanged)));
    }

    public SeriesSeason Data { get; }
    public Guid Id => Data.Id;
    public int Number => Data.Number;
    public ObservableCollection<EpisodeNavigationItem> Episodes { get; }
    public string Heading => $"SEASON {Number:00}";
    public event Action? Changed;

    [ObservableProperty]
    public partial string Title { get; set; }

    partial void OnTitleChanged(string value)
    {
        Data.Title = value;
        _markChanged();
        Changed?.Invoke();
    }
}

public partial class EpisodeNavigationItem : ObservableObject
{
    private readonly Action _markChanged;

    public EpisodeNavigationItem(SeriesEpisode data, Action markChanged)
    {
        Data = data;
        _markChanged = markChanged;
        Title = data.Title;
    }

    public SeriesEpisode Data { get; }
    public Guid Id => Data.Id;
    public int Number => Data.Number;
    public string DisplayTitle => $"E{Number:00}  {Title}";
    public event Action? Changed;

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    partial void OnTitleChanged(string value)
    {
        Data.Title = value;
        Data.Script.Title = value;
        OnPropertyChanged(nameof(DisplayTitle));
        _markChanged();
        Changed?.Invoke();
    }

    partial void OnIsActiveChanged(bool value) => OnPropertyChanged(nameof(DisplayTitle));
}
