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

using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Sksrj.App.Services;

public interface IDialogService
{
    Task<string?> OpenProjectAsync();
    Task<string?> SaveProjectAsync(string suggestedName);
    Task<string?> SavePdfAsync(string suggestedName);
    Task<bool> ConfirmAsync(string title, string message, string ok = "OK", string cancel = "Anuluj");
    Task ShowAboutAsync();
}

public sealed class DialogService : IDialogService
{
    private readonly Window _owner;
    public DialogService(Window owner) => _owner = owner;

    private static readonly FilePickerFileType ProjectType = new("Projekt SKSRJ") { Patterns = new[] { "*.sksrj" } };
    private static readonly FilePickerFileType JsonType = new("JSON") { Patterns = new[] { "*.json" } };
    private static readonly FilePickerFileType PdfType = new("Dokument PDF") { Patterns = new[] { "*.pdf" }, MimeTypes = new[] { "application/pdf" } };

    public async Task<string?> OpenProjectAsync()
    {
        var res = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Otwórz projekt rozkładu",
            AllowMultiple = false,
            FileTypeFilter = new[] { ProjectType, JsonType, FilePickerFileTypes.All },
        });
        return res.Count > 0 ? res[0].TryGetLocalPath() : null;
    }

    public async Task<string?> SaveProjectAsync(string suggestedName)
    {
        var res = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Zapisz projekt rozkładu",
            SuggestedFileName = suggestedName,
            DefaultExtension = "sksrj",
            FileTypeChoices = new[] { ProjectType },
            ShowOverwritePrompt = true,
        });
        return res?.TryGetLocalPath();
    }

    public async Task<string?> SavePdfAsync(string suggestedName)
    {
        var res = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Eksport karty rozkładu jazdy do PDF",
            SuggestedFileName = suggestedName,
            DefaultExtension = "pdf",
            FileTypeChoices = new[] { PdfType },
            ShowOverwritePrompt = true,
        });
        return res?.TryGetLocalPath();
    }

    public Task ShowAboutAsync() => new Views.AboutWindow().ShowDialog(_owner);

    public Task<bool> ConfirmAsync(string title, string message, string ok = "OK", string cancel = "Anuluj") =>
        Views.ConfirmDialog.ShowAsync(_owner, title, message, ok, cancel);
}
