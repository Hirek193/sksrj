namespace Sksrj.Core.Dictionaries;

public sealed record CodeEntry(string Code, string Description)
{
    public override string ToString() => Code;
    public string Display => $"{Code} – {Description}";
}

public sealed record TrainKindRow(string Group, string Kind, string? Steam, string? ElLoco, string? ElEmu, string? DieselLoco, string? DieselDmu);

/// <summary>Słowniki znaków i skrótów wg Załącznika 4 do instrukcji Ir-11 (strony stałe karty rozkładu jazdy).</summary>
public static class Ir11
{
    /// <summary>Skrót rodzaju pociągu w kolumnie 6 obok numeru pociągu (strony 6–7).</summary>
    public static readonly IReadOnlyList<TrainKindRow> TrainKinds = new TrainKindRow[]
    {
        new("A. Pociągi pasażerskie", "EuroCity – ekspresowy międzynarodowy", null, "ECE", "ECJ", "ECS", "ECM"),
        new("A. Pociągi pasażerskie", "EuroNight – nocny ekspresowy międzynarodowy", null, "ENE", null, "ENS", null),
        new("A. Pociągi pasażerskie", "Ekspresowy krajowy (w tym InterCity)", null, "EIE", "EIJ", "EIS", "EIM"),
        new("A. Pociągi pasażerskie", "Międzynarodowy pospieszny", null, "MME", "MMJ", "MMS", "MMM"),
        new("A. Pociągi pasażerskie", "Międzywojewódzki pospieszny", null, "MPE", "MPJ", "MPS", "MPM"),
        new("A. Pociągi pasażerskie", "Międzywojewódzki pospieszny nocny lub typu hotelowego", null, "MHE", "MHJ", "MHS", null),
        new("A. Pociągi pasażerskie", "Międzywojewódzki osobowy", null, "MOE", "MOJ", "MOS", "MOM"),
        new("A. Pociągi pasażerskie", "Wojewódzki osobowy międzynarodowy", "RMP", "RME", "RMJ", "RMS", "RMM"),
        new("A. Pociągi pasażerskie", "Wojewódzki pospieszny", "RPP", "RPE", "RPJ", "RPS", "RPM"),
        new("A. Pociągi pasażerskie", "Wojewódzki osobowy", "ROP", "ROE", "ROJ", "ROS", "ROM"),
        new("A. Pociągi pasażerskie", "Wojewódzki osobowy w komunikacji aglomeracyjnej", null, "RAE", "RAJ", "RAS", "RAM"),
        new("A. Pociągi pasażerskie", "Próżny skład pasażerski – od/do pociągów pasażerskich", "PWP", "PWE", "PWJ", "PWS", "PWM"),
        new("A. Pociągi pasażerskie", "Próżny skład pasażerski (pociągi próbne, pojazdy trakcyjne i pomocnicze w stanie nieczynnym, skład lokomotyw)", "PXP", "PXE", "PXJ", "PXS", "PXM"),
        new("B.1 Pociągi towarowe w ruchu międzynarodowym", "Do międzynarodowych przewozów jednostek transportu intermodalnego", null, "TCE", null, "TCS", null),
        new("B.1 Pociągi towarowe w ruchu międzynarodowym", "Do międzynarodowych przewozów masowych", null, "TGE", null, "TGS", null),
        new("B.1 Pociągi towarowe w ruchu międzynarodowym", "Do międzynarodowych przewozów niemasowych", null, "TRE", null, "TRS", null),
        new("B.2 Pociągi towarowe w ruchu krajowym", "Do krajowych przewozów jednostek transportu intermodalnego", null, "TDE", null, "TDS", null),
        new("B.2 Pociągi towarowe w ruchu krajowym", "Do przewozów masowych", null, "TME", null, "TMS", null),
        new("B.2 Pociągi towarowe w ruchu krajowym", "Do przewozów niemasowych", null, "TNE", null, "TNS", null),
        new("B.2 Pociągi towarowe w ruchu krajowym", "Do obsługi stacji i bocznic", "TKP", "TKE", null, "TKS", null),
        new("B.2 Pociągi towarowe w ruchu krajowym", "Próżne wagony z/do naprawy, pociągi próbne oraz pozostałe pociągi", null, "TSE", "TSJ", "TSS", "TSM"),
        new("B.2 Pociągi towarowe w ruchu krajowym", "Skład lokomotyw – powyżej 3 lokomotyw", null, "THE", null, "THS", null),
        new("C. Pojazdy kolejowe luzem", "Lokomotywa do i od pociągów pasażerskich", "LPP", "LPE", null, "LPS", null),
        new("C. Pojazdy kolejowe luzem", "Lokomotywa do i od pociągów towarowych", "LTP", "LTE", null, "LTS", null),
        new("C. Pojazdy kolejowe luzem", "Lokomotywa luzem do i od pociągów utrzymaniowo-naprawczych, pojazd pomocniczy", null, "LZE", null, "LZS", null),
        new("C. Pojazdy kolejowe luzem", "Lokomotywa od/do prac manewrowych, pozostałe lokomotywy luzem, pojazd pomocniczy", null, "LSE", null, "LSS", null),
        new("D. Pociągi utrzymaniowo-naprawcze", "Ratunkowy", null, "ZGE", "ZGJ", "ZGS", "ZGM"),
        new("D. Pociągi utrzymaniowo-naprawcze", "Inspekcyjny, diagnostyczny", null, "ZNE", "ZNJ", "ZNS", "ZNM"),
        new("D. Pociągi utrzymaniowo-naprawcze", "Inne utrzymaniowe", null, "ZUE", "ZUJ", "ZUS", "ZUM"),
    };

    public static readonly IReadOnlyList<CodeEntry> TrainCategoryCodes = BuildCategoryCodes();

    private static IReadOnlyList<CodeEntry> BuildCategoryCodes()
    {
        var list = new List<CodeEntry>();
        foreach (var r in TrainKinds)
        {
            void Add(string? c, string traction) { if (c != null) list.Add(new CodeEntry(c, $"{r.Kind} – {traction}")); }
            Add(r.Steam, "trakcja parowa");
            Add(r.ElLoco, "trakcja elektryczna, lokomotywy");
            Add(r.ElEmu, "trakcja elektryczna, zespoły i wagony trakcyjne");
            Add(r.DieselLoco, "trakcja spalinowa, lokomotywy");
            Add(r.DieselDmu, "trakcja spalinowa, zespoły i wagony trakcyjne");
        }
        return list;
    }

    /// <summary>Skróty rodzaju posterunku dopisywane do nazwy (strona 2).</summary>
    public static readonly IReadOnlyList<CodeEntry> PointTypes = new CodeEntry[]
    {
        new("b", "miejsce dojazdu do bocznicy, punktu ładunkowego"),
        new("gt", "grupa torów wydzielona w obrębie stacji"),
        new("m", "mijanka"),
        new("opm", "obsługa przejazdu przez drużynę pociągu"),
        new("pe", "posterunek odstępowy"),
        new("pb", "posterunek bocznicowy"),
        new("pgr", "punkt graniczny"),
        new("po", "przystanek osobowy publiczny"),
        new("podg", "posterunek odgałęźny"),
        new("pp", "punkt przeładunkowy"),
        new("ps", "przystanek służbowy"),
        new("pst", "obiekt kolei obcych"),
        new("pprj", "punkt pomocniczy do konstrukcji rozkładu jazdy"),
        new("pzs", "miejsce złączenia linii kolejowych"),
        new("stth", "stacja techniczna"),
        new("zz", "miejsce zmiany systemu zasilania"),
    };

    /// <summary>Znaki i skróty w kolumnie 5 (strony 2–4) – do pola „skróty” posterunku.</summary>
    public static readonly IReadOnlyList<CodeEntry> Abbreviations = new CodeEntry[]
    {
        new("Z", "posterunek ruchu zamknięty"),
        new("K", "zgłoszenie o wjeździe pociągu z sygnałami końca pociągu należy do obowiązków drużyny pociągowej"),
        new("NZ", "postój „na żądanie” podróżnego"),
        new("W24", "zezwolenie na wyjazd na tor szlakowy w kierunku przeciwnym sygnałem zezwalającym lub Sz ze wskaźnikiem W24"),
        new("T", "trudny podjazd do stacji dla pociągu towarowego"),
        new("Rd1", "zezwolenie na wyjazd pociągu pasażerskiego wymaga dodatkowo sygnału „Nakaz jazdy”"),
        new("APO", "automatyczny posterunek odstępowy, którego semaforem dysponuje zdalnie nastawnia"),
        new("UZS", "posterunek posiadający urządzenia do zdalnego sterowania lub dysponujący semaforem APO"),
        new("ZS", "posterunek ruchu zdalnie sterowany"),
        new("ZSM", "posterunek ruchu zdalnie sterowany, z możliwością miejscowego nastawiania"),
        new("R1", "radiotelefon pracujący na kanale 1"),
        new("R2", "radiotelefon pracujący na kanale 2"),
        new("R3", "radiotelefon pracujący na kanale 3"),
        new("R4", "radiotelefon pracujący na kanale 4"),
        new("R5", "radiotelefon pracujący na kanale 5"),
        new("R6", "radiotelefon pracujący na kanale 6"),
        new("R7", "radiotelefon pracujący na kanale 7"),
        new("GSM-R", "cyfrowy naziemny system łączności radiowej"),
        new("H", "zainstalowane są urządzenia SHP"),
        new("R307", "kierujący nie może odjechać bez rozkazu pisemnego „O” na dalszy odcinek jazdy"),
        new("RT", "rozmowy w sieci radiołączności pociągowej są rejestrowane"),
        new("SL", "posterunek ruchu nieobsadzony, sterowany przez pociąg"),
        new("PP", "blokada jednoodstępowa dla jazdy po torze w kierunku przeciwnym"),
        new("S", "blokada samoczynna (jednotorowa lub jednokierunkowa w kierunku zasadniczym)"),
        new("SS", "blokada samoczynna dwukierunkowa na szlaku dwutorowym"),
        new("SP", "samoczynna w kierunku zasadniczym, półsamoczynna w przeciwnym"),
        new("PS", "półsamoczynna w kierunku zasadniczym, samoczynna w przeciwnym"),
        new("L1", "przytorowe urządzenia ERTMS/ETCS poziom 1"),
        new("L2", "przytorowe urządzenia ERTMS/ETCS poziom 2"),
        new("L1LS", "przytorowe urządzenia ERTMS/ETCS poziom 1LS"),
    };

    /// <summary>Rodzaje postoju obok nazwy (strona 5).</summary>
    public static readonly IReadOnlyList<CodeEntry> StopKinds = new CodeEntry[]
    {
        new("ph", "postój handlowy – obsługa pasażera"),
        new("pn", "postój niehandlowy – tylko dla potrzeb przewoźnika"),
        new("zp", "zatrzymuje się w razie potrzeby po uprzednim zgłoszeniu kierownikowi pociągu"),
        new("pm", "praca manewrowa na stacji z wymianą grup wagonów lub lokomotywy"),
        new("pt", "postój tylko dla potrzeb technicznych"),
        new("zd", "zmiana drużyny"),
    };

    /// <summary>Symbole terminów kursowania (strona 8).</summary>
    public static readonly IReadOnlyList<CodeEntry> RunningDaySymbols = new CodeEntry[]
    {
        new("(+)", "kursuje w dni świąteczne (niedziele i święta)"),
        new("(1)", "kursuje w poniedziałki"),
        new("(2)", "kursuje we wtorki"),
        new("(3)", "kursuje w środy"),
        new("(4)", "kursuje w czwartki"),
        new("(5)", "kursuje w piątki"),
        new("(6)", "kursuje w soboty"),
        new("(7)", "kursuje w niedziele"),
        new("(1)-(7)", "kursuje od poniedziałku do niedzieli"),
        new("(A)", "kursuje od poniedziałku do piątku"),
        new("(B)", "kursuje codziennie oprócz soboty"),
        new("(C)", "kursuje w soboty, niedziele i święta"),
        new("(D)", "kursuje od poniedziałku do piątku oprócz dni świątecznych"),
        new("(E)", "kursuje od poniedziałku do soboty oprócz dni świątecznych"),
    };

    /// <summary>Rodzaje hamulca (kolumna 9).</summary>
    public static readonly IReadOnlyList<CodeEntry> BrakeTypes = new CodeEntry[]
    {
        new("P", "hamulec szybko działający"),
        new("R", "hamulec szybko działający"),
        new("R+MG", "hamulec szybko działający z hamulcem szynowym"),
        new("G", "hamulec wolno działający"),
    };

    /// <summary>Rodzaje rozkładu drukowane na stronie tytułowej.</summary>
    public static readonly IReadOnlyList<string> TimetableKindPresets = new[]
    {
        "ROCZNY ROZKŁAD JAZDY",
        "RRJ - KOREKTA PARAMETRÓW LINII",
        "KOREKTA ROCZNEGO ROZKŁADU JAZDY",
        "ZMIANA PARAMETRÓW ROCZNEGO ROZKŁADU JAZDY",
        "ZASTĘPCZY ROZKŁAD JAZDY",
        "INDYWIDUALNY ROZKŁAD JAZDY",
        "ZMIANA PARAMETRÓW INDYWIDUALNEGO ROZKŁADU JAZDY",
        "PRZEJŚCIOWY ROZKŁAD JAZDY",
        "STUDIUM ROZKŁADU JAZDY",
    };

    /// <summary>Sygnatury zarządzeń rozkładu jazdy wg § 35 ust. 3.</summary>
    public static readonly IReadOnlyList<OrderSignatureType> OrderTypes = new OrderSignatureType[]
    {
        new("RRJ", "Roczny Rozkład Jazdy", true),
        new("KRJ", "Korekta Rocznego Rozkładu Jazdy", true),
        new("ZPRRJ", "Zmiana Parametrów Rocznego Rozkładu Jazdy", false),
        new("ZRJP", "Zastępczy Rozkład Jazdy pociągów Pasażerskich", true),
        new("ZRJT", "Zastępczy Rozkład Jazdy pociągów Towarowych", true),
        new("IRJ", "Indywidualny Rozkład Jazdy", false),
        new("ZPIRJ", "Zmiana Parametrów Indywidualnego Rozkładu Jazdy", false),
        new("SRJ", "Studium Rozkładu Jazdy", false),
    };
}

public sealed record OrderSignatureType(string Prefix, string Name, bool HasSuffix)
{
    public override string ToString() => $"{Prefix} – {Name}";

    /// <summary>Np. RRJ_2020_452_15, ZRJP_2020_458965_6-1, IRJ_2020_112233.</summary>
    public string Build(string year, string applicationId, string suffix)
    {
        var s = $"{Prefix}_{year}_{applicationId}";
        if (HasSuffix && !string.IsNullOrWhiteSpace(suffix)) s += "_" + suffix.Trim();
        return s;
    }
}
