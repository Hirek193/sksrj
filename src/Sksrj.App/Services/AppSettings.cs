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


using System.Text.Json;

namespace Sksrj.App.Services;

/// <summary>Ustawienia programu zapisywane w profilu użytkownika (motyw aplikacji, motyw schematu).</summary>
public sealed class AppSettings
{
    public bool IsDark { get; set; }
    public bool SchemeDark { get; set; }

    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SKSRJ", "settings.json");

    /// <summary>Wyłączone w testach i trybie bez interfejsu.</summary>
    public static bool Enabled { get; set; } = true;

    public static AppSettings Load()
    {
        if (!Enabled) return new AppSettings();
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new() : new(); }
        catch { return new AppSettings(); }
    }

    public void Save()
    {
        if (!Enabled) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* ustawienia są opcjonalne */ }
    }
}
