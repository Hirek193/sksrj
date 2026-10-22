using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Sksrj.App.Services;

public interface IDialogService
{
    Task<string?> OpenProjectAsync();
    Task<string?> SaveProjectAsync(string suggestedName);
    Task<string?> SavePdfAsync(string suggestedName);
    Task<bool> ConfirmAsync(string title, string message, string ok = "OK", string cancel = "Anuluj");
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

    public Task<bool> ConfirmAsync(string title, string message, string ok = "OK", string cancel = "Anuluj") =>
        Views.ConfirmDialog.ShowAsync(_owner, title, message, ok, cancel);
}
