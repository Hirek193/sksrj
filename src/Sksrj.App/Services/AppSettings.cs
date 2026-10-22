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
    /// <summary>Bank nagłówków wydawcy (wiersze rozdzielone znakiem nowej linii); null – jeszcze nie utworzony.</summary>
    public List<string>? Issuers { get; set; }
    /// <summary>Bank przewoźników; null – jeszcze nie utworzony.</summary>
    public List<string>? Carriers { get; set; }

    public static readonly string[] DefaultIssuers =
    {
        "FLK Fikcyjne Linie Kolejowe S.F.",
        "PKP POLSKIE LINIE KOLEJOWE S.A.\nCENTRUM ZARZĄDZANIA RUCHEM KOLEJOWYM\nW WARSZAWIE",
    };

    public static readonly string[] DefaultCarriers =
    {
        "Koleje Fikcyjne Spółka z o.o.",
        "PKP INTERCITY Spółka Akcyjna",
        "POLREGIO S.A.",
        "Koleje Mazowieckie – KM sp. z o.o.",
        "Koleje Śląskie sp. z o.o.",
        "Koleje Dolnośląskie S.A.",
        "Koleje Wielkopolskie sp. z o.o.",
        "Łódzka Kolej Aglomeracyjna sp. z o.o.",
        "PKP Szybka Kolej Miejska w Trójmieście sp. z o.o.",
        "Arriva RP sp. z o.o.",
        "PKP CARGO S.A.",
        "DB Cargo Polska S.A.",
    };

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
