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
    /// <summary>Zmiana właściwości konkretnego obiektu (nadawca, nazwa właściwości) lub kolekcji (kolekcja, null).</summary>
    public event Action<object, string?>? ItemChanged;

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

    /// <summary>Właściwości pomocnicze (wyliczane przez program) – ich zmiana nie oznacza zmiany projektu.</summary>
    private static readonly HashSet<string> Derived = new() { nameof(RoutePoint.HiddenInCard) };

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != null && Derived.Contains(e.PropertyName)) return;
        // podmiana obiektu podrzędnego – przepnij śledzenie
        if (sender != null && e.PropertyName != null)
        {
            var prop = sender.GetType().GetProperty(e.PropertyName);
            if (prop != null && !prop.PropertyType.IsValueType && prop.PropertyType != typeof(string))
                Attach(prop.GetValue(sender));
        }
        if (sender != null) ItemChanged?.Invoke(sender, e.PropertyName);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null) foreach (var o in e.OldItems) Detach(o);
        if (e.NewItems != null) foreach (var o in e.NewItems) Attach(o);
        if (e.Action == NotifyCollectionChangedAction.Reset && sender is IEnumerable en)
            foreach (var o in en) Attach(o);
        if (sender != null) ItemChanged?.Invoke(sender, null);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        foreach (var o in _tracked.ToArray()) Detach(o);
        _ = _root;
    }
}
