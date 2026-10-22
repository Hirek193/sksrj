// SKSRJ Timetable generator
// Copyright (C) 2026 Oskar Jagodziński
//
// This program is free software: you can redistribute it and/or modify it under the terms of
// the GNU General Public License as published by the Free Software Foundation, either version 3
// of the License, or (at your option) any later version, together with the additional terms
// under Section 7 of that License (see the files LICENSE and ADDITIONAL_TERMS).
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
//
// Based on SKSRJ Timetable generator originally created by Oskar Jagodziński.
// Original source: https://github.com/Hirek193/sksrj


using CommunityToolkit.Mvvm.ComponentModel;
using Sksrj.Core.Import;

namespace Sksrj.App.ViewModels;

/// <summary>Opcje importu rozkładu jazdy MaSzyny.</summary>
public partial class MaSzynaImportViewModel : ObservableObject
{
    public MaSzynaTimetable Data { get; }
    public string FileName { get; }
    [ObservableProperty] private string _lineNumber;
    [ObservableProperty] private string _lineName;
    [ObservableProperty] private bool _mergeByName = true;
    [ObservableProperty] private bool _createTimetable = true;

    public string Summary =>
        $"{(Data.TrainId.Length > 0 ? Data.TrainId : "pociąg")} · {Data.From} – {Data.To} · {Data.Stations.Count} posterunków · {Data.LengthKm:0.00} km" +
        (Data.Loco.Length > 0 ? $" · {Data.Loco}" : "");

    public string StationsText => string.Join("  ·  ", Data.Stations.Select(s => s.Name + (s.Suffix.Length > 0 ? " " + s.Suffix : "") + $" ({s.KmText})"));
    public string WarningsText => string.Join("\n", Data.Warnings);
    public bool HasWarnings => Data.Warnings.Count > 0;

    public MaSzynaImportViewModel(MaSzynaTimetable data, string fileName, string suggestedLine)
    {
        Data = data; FileName = fileName;
        _lineNumber = suggestedLine;
        _lineName = $"{data.From} – {data.To}";
    }

    public MaSzynaImportOptions Options => new() { LineNumber = LineNumber, LineName = LineName, MergeByName = MergeByName, CreateTimetable = CreateTimetable };
}
