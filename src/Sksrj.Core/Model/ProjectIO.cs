using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sksrj.Core.Model;

public static class ProjectSerializer
{
    public const string Extension = ".sksrj";

    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string Serialize(TimetableProject p) => JsonSerializer.Serialize(p, Options);

    public static TimetableProject Deserialize(string json) =>
        JsonSerializer.Deserialize<TimetableProject>(json, Options) ?? new TimetableProject();

    public static void Save(TimetableProject p, string path) => File.WriteAllText(path, Serialize(p));

    public static TimetableProject Load(string path) => Deserialize(File.ReadAllText(path));

    public static T Clone<T>(T obj) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj, Options), Options)!;
}

/// <summary>Śledzi zmiany w całym drzewie obiektów (właściwości i kolekcje) – do odświeżania podglądu i flagi „niezapisane”.</summary>
public sealed class DeepChangeTracker : IDisposable
{
    private readonly HashSet<object> _tracked = new(ReferenceEqualityComparer.Instance);
    private readonly object _root;
    public event EventHandler? Changed;

    public DeepChangeTracker(object root)
    {
        _root = root;
        Attach(root);
    }

    private void Attach(object? o)
    {
        if (o == null || o is string || !_tracked.Add(o)) return;
        if (o is INotifyPropertyChanged npc) npc.PropertyChanged += OnPropertyChanged;
        if (o is INotifyCollectionChanged ncc) ncc.CollectionChanged += OnCollectionChanged;
        if (o is IEnumerable en and not string)
            foreach (var item in en) Attach(item);
        foreach (var child in Children(o)) Attach(child);
    }

    private void Detach(object? o)
    {
        if (o == null || o is string || !_tracked.Remove(o)) return;
        if (o is INotifyPropertyChanged npc) npc.PropertyChanged -= OnPropertyChanged;
        if (o is INotifyCollectionChanged ncc) ncc.CollectionChanged -= OnCollectionChanged;
        if (o is IEnumerable en and not string)
            foreach (var item in en) Detach(item);
        foreach (var child in Children(o)) Detach(child);
    }

    private static IEnumerable<object> Children(object o)
    {
        var t = o.GetType();
        if (t.Namespace == null || !t.Namespace.StartsWith("Sksrj", StringComparison.Ordinal)) yield break;
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.GetIndexParameters().Length > 0 || p.PropertyType.IsValueType || p.PropertyType == typeof(string)) continue;
            if (p.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;
            object? v;
            try { v = p.GetValue(o); } catch { continue; }
            if (v != null) yield return v;
        }
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // podmiana obiektu podrzędnego – przepnij śledzenie
        if (sender != null && e.PropertyName != null)
        {
            var prop = sender.GetType().GetProperty(e.PropertyName);
            if (prop != null && !prop.PropertyType.IsValueType && prop.PropertyType != typeof(string))
                Attach(prop.GetValue(sender));
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null) foreach (var o in e.OldItems) Detach(o);
        if (e.NewItems != null) foreach (var o in e.NewItems) Attach(o);
        if (e.Action == NotifyCollectionChangedAction.Reset && sender is IEnumerable en)
            foreach (var o in en) Attach(o);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        foreach (var o in _tracked.ToArray()) Detach(o);
        _ = _root;
    }
}
