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

namespace Sksrj.Core;

/// <summary>Nazwa programu, autor i wymagana atrybucja (ADDITIONAL_TERMS, pkt 1).</summary>
public static class AppInfo
{
    public const string Name = "SKSRJ Timetable generator";
    public const string ShortName = "SKSRJ";
    public const string Tagline = "Generator symulacyjnych kart rozkładu jazdy";
    public const string Author = "Oskar Jagodziński";
    public const string Year = "2026";
    public const string SourceUrl = "https://github.com/Hirek193/sksrj";

    public static string Version =>
        typeof(AppInfo).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "1.0.0";

    public const string Copyright = "Copyright © " + Year + " " + Author;

    /// <summary>Atrybucja, którą zgodnie z ADDITIONAL_TERMS należy zachować w kodzie i w „Appropriate Legal Notices”.</summary>
    public const string AttributionLine1 = "Based on SKSRJ Timetable generator originally created by Oskar Jagodziński.";
    public const string AttributionLine2 = "Original source: " + SourceUrl;

    public const string LicenseShort = "Licencja GNU GPL v3 lub nowsza, z warunkami dodatkowymi (ADDITIONAL_TERMS). Program jest rozpowszechniany BEZ JAKIEJKOLWIEK GWARANCJI.";

    public const string Disclaimer = "Program symulacyjny – nie jest powiązany z PKP Polskie Linie Kolejowe S.A. ani nie jest przez nią autoryzowany. Wygląd kart wzorowany na instrukcji Ir-11.";
}
