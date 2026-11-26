using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ClosedXML.Excel;
using ExcelDataReader;

namespace DernekTakip.Servisler;

public static class UyeExcelServisi
{
    static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    static UyeExcelServisi()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static byte[] OrnekExcelOlustur(string dernekAd, IReadOnlyList<GrupKayit> gruplar)
    {
        using var wb = new XLWorkbook();

        // 1. Sekme: Üye Listesi
        var ws = wb.Worksheets.Add("Üye Listesi");
        var basliklar = new[]
        {
            "Ad Soyad",
            "Grup",
            "T.C. Kimlik No",
            "Telefon",
            "E-posta",
            "Adres",
            "Durum",
            "Üye Giriş Tarihi"
        };

        for (var c = 0; c < basliklar.Length; c++)
            ws.Cell(1, c + 1).Value = basliklar[c];

        var baslikStil = ws.Range(1, 1, 1, basliklar.Length);
        baslikStil.Style.Font.Bold = true;
        baslikStil.Style.Font.FontColor = XLColor.White;
        baslikStil.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F2248");
        baslikStil.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Row(1).Height = 25;

        // T.C., Telefon ve Tarih sütunlarını metin formatına al (baştaki 0 silinmesin)
        ws.Column(3).Style.NumberFormat.Format = "@";
        ws.Column(4).Style.NumberFormat.Format = "@";
        ws.Column(8).Style.NumberFormat.Format = "@";

        var bugun = DateTime.Today.ToString("dd.MM.yyyy");
        var ilkGrup = gruplar.FirstOrDefault()?.Ad ?? "Üyeler";
        var ikinciGrup = gruplar.Skip(1).FirstOrDefault()?.Ad ?? ilkGrup;

        var ornekler = new (string Ad, string Grup, string Tc, string Tel, string Mail, string Adres, string Durum, string Tarih)[]
        {
            ("Örnek Üye 1 (Silinebilir)", ilkGrup, "10000000146", "0532 000 00 01", "ornek1@dernek.org.tr", "Merkez Mah. Atatürk Cad. No:1", "Aktif", bugun),
            ("Örnek Üye 2 (Silinebilir)", ikinciGrup, "10000000210", "0533 000 00 02", "ornek2@dernek.org.tr", "Cumhuriyet Mah. İstiklal Sok. No:5", "Aktif", bugun)
        };

        for (var i = 0; i < ornekler.Length; i++)
        {
            var r = i + 2;
            var o = ornekler[i];
            ws.Cell(r, 1).SetValue(o.Ad);
            ws.Cell(r, 2).SetValue(o.Grup);
            ws.Cell(r, 3).SetValue(o.Tc);
            ws.Cell(r, 4).SetValue(o.Tel);
            ws.Cell(r, 5).SetValue(o.Mail);
            ws.Cell(r, 6).SetValue(o.Adres);
            ws.Cell(r, 7).SetValue(o.Durum);
            ws.Cell(r, 8).SetValue(o.Tarih);

            var satirStil = ws.Range(r, 1, r, basliklar.Length);
            satirStil.Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF9C3");
            satirStil.Style.Font.FontColor = XLColor.FromHtml("#713F12");
            satirStil.Style.Font.Italic = true;
        }

        // Kullanıcıların dolduracağı temiz çerçeveli satırlar (50 satır hazır ızgara + açılır grup/durum doğrulaması)
        var tabloAralik = ws.Range(1, 1, 53, basliklar.Length);
        tabloAralik.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tabloAralik.Style.Border.OutsideBorderColor = XLColor.FromHtml("#94A3B8");
        tabloAralik.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        tabloAralik.Style.Border.InsideBorderColor = XLColor.FromHtml("#E2E8F0");

        if (gruplar.Count > 0)
        {
            var grupMetin = "\"" + string.Join(",", gruplar.Select(g => g.Ad.Replace("\"", ""))) + "\"";
            if (grupMetin.Length <= 250)
            {
                var dvGrup = ws.Range(2, 2, 500, 2).CreateDataValidation();
                dvGrup.List(grupMetin, true);
                dvGrup.IgnoreBlanks = true;
                dvGrup.ShowErrorMessage = false;
            }
        }

        var dvDurum = ws.Range(2, 7, 500, 7).CreateDataValidation();
        dvDurum.List("\"Aktif,Ayrıldı\"", true);
        dvDurum.IgnoreBlanks = true;
        dvDurum.ShowErrorMessage = false;

        ws.Column(1).Width = 28;
        ws.Column(2).Width = 18;
        ws.Column(3).Width = 16;
        ws.Column(4).Width = 18;
        ws.Column(5).Width = 24;
        ws.Column(6).Width = 36;
        ws.Column(7).Width = 13;
        ws.Column(8).Width = 18;
        ws.SheetView.FreezeRows(1);

        // 2. Sekme: Kullanım Kılavuzu
        var rehber = wb.Worksheets.Add("Açıklama");
        rehber.Cell(1, 1).Value = $"{dernekAd} — Toplu Üye Aktarım Kılavuzu";
        rehber.Range(1, 1, 1, 2).Merge();
        rehber.Range(1, 1, 1, 2).Style.Font.Bold = true;
        rehber.Range(1, 1, 1, 2).Style.Font.FontSize = 12;
        rehber.Range(1, 1, 1, 2).Style.Font.FontColor = XLColor.White;
        rehber.Range(1, 1, 1, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#0F2248");
        rehber.Row(1).Height = 25;

        var notlar = new (string Alan, string Aciklama)[]
        {
            ("Ad Soyad (Zorunlu)", "Üyenin tam adı ve soyadı. Sadece bu alan dolu olsa bile üye eklenir."),
            ("Grup (İsteğe Bağlı)", "Tanımlı gruplar: " + string.Join(", ", gruplar.Select(g => g.Ad)) + ". Boş bırakılırsa seçilen varsayılan gruba eklenir."),
            ("T.C. Kimlik No", "11 haneli T.C. kimlik numarası. Aynı T.C. ile kayıtlı üye varsa mükerrer kayıt açılmaz, atlanır."),
            ("Telefon", "Cep veya sabit telefon (Örn: 0532 123 45 67). Hücre metin formatındadır, baştaki 0 korunur."),
            ("E-posta / Adres", "İsteğe bağlı iletişim ve ikametgah adresi bilgileri."),
            ("Durum", "Aktif veya Ayrıldı yazılabilir. Boş bırakılırsa Aktif kabul edilir."),
            ("Üye Giriş Tarihi", "GG.AA.YYYY formatında tarih (Örn: 26.11.2026). Boş bırakılırsa bugünün tarihi yazılır."),
            ("Sarı Örnek Satırlar", "Üye Listesi sekmesindeki sarı 'Örnek Üye' satırlarını silebilir veya üzerine yazabilirsiniz; silmeseniz de sistem örnek satırları otomatik atlar.")
        };

        for (var i = 0; i < notlar.Length; i++)
        {
            var r = i + 2;
            rehber.Cell(r, 1).SetValue(notlar[i].Alan);
            rehber.Cell(r, 1).Style.Font.Bold = true;
            rehber.Cell(r, 2).SetValue(notlar[i].Aciklama);
        }
        rehber.Column(1).Width = 24;
        rehber.Column(2).Width = 88;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static List<UyeExcelSatir> Oku(Stream akis, string? dosyaAdi = null)
    {
        using var ms = new MemoryStream();
        akis.CopyTo(ms);
        if (ms.Length == 0) return [];

        var hamSatirlar = TabloSatirlariCikar(ms, dosyaAdi);
        return SatirlardanUyeCikar(hamSatirlar);
    }

    static List<UyeExcelSatir> SatirlardanUyeCikar(List<List<string>> satirlar)
    {
        if (satirlar.Count == 0) return [];

        int baslikSatirIdx = -1;
        int cAdSoyad = -1, cAd = -1, cSoyad = -1, cGrup = -1, cTc = -1, cTel = -1, cMail = -1, cAdres = -1, cDurum = -1, cTarih = -1;

        for (var r = 0; r < Math.Min(10, satirlar.Count); r++)
        {
            var s = satirlar[r];
            int bAdSoyad = -1, bAd = -1, bSoyad = -1, bGrup = -1, bTc = -1, bTel = -1, bMail = -1, bAdres = -1, bDurum = -1, bTarih = -1;
            for (var c = 0; c < s.Count; c++)
            {
                var k = AnahtarNorm(s[c]);
                if (k.Length == 0) continue;

                if (bAdSoyad < 0 && (k.Contains("adsoyad") || k.Contains("adisoyadi") || k is "uye" or "uyead" or "uyeadi" or "isimsoyisim"))
                    bAdSoyad = c;
                else if (bAd < 0 && k is "ad" or "adi" or "isim")
                    bAd = c;
                else if (bSoyad < 0 && k is "soyad" or "soyadi")
                    bSoyad = c;
                else if (bGrup < 0 && (k.Contains("grup") || k.Contains("uyeturu") || k is "tur" or "kategori"))
                    bGrup = c;
                else if (bTc < 0 && (k is "tc" or "tckn" or "kimlikno" || k.Contains("tckimlik") || k.Contains("tcno")))
                    bTc = c;
                else if (bTel < 0 && (k.Contains("tel") || k.Contains("gsm") || k.Contains("cep")))
                    bTel = c;
                else if (bMail < 0 && (k.Contains("posta") || k.Contains("mail") || k.Contains("email")))
                    bMail = c;
                else if (bAdres < 0 && (k.Contains("adres") || k.Contains("ikamet")))
                    bAdres = c;
                else if (bDurum < 0 && k.Contains("durum"))
                    bDurum = c;
                else if (bTarih < 0 && (k.Contains("giristarih") || k.Contains("uyegiris") || k.Contains("kayittarih") || k is "tarih"))
                    bTarih = c;
            }

            if (bAdSoyad >= 0 || (bAd >= 0 && bSoyad >= 0))
            {
                baslikSatirIdx = r;
                cAdSoyad = bAdSoyad;
                cAd = bAd;
                cSoyad = bSoyad;
                cGrup = bGrup;
                cTc = bTc;
                cTel = bTel;
                cMail = bMail;
                cAdres = bAdres;
                cDurum = bDurum;
                cTarih = bTarih;
                break;
            }
        }

        // Başlık satırı yoksa şablon sırasını varsay (A: Ad Soyad, B: Grup, C: TC, D: Tel, E: Eposta, F: Adres, G: Durum, H: Tarih)
        int baslangic = baslikSatirIdx >= 0 ? baslikSatirIdx + 1 : 0;
        if (baslikSatirIdx < 0)
        {
            cAdSoyad = 0; cGrup = 1; cTc = 2; cTel = 3; cMail = 4; cAdres = 5; cDurum = 6; cTarih = 7;
        }

        var sonuc = new List<UyeExcelSatir>();
        for (var r = baslangic; r < satirlar.Count; r++)
        {
            var s = satirlar[r];
            string Al(int idx) => idx >= 0 && idx < s.Count ? (s[idx] ?? "").Trim() : "";

            var adSoyad = cAdSoyad >= 0
                ? Al(cAdSoyad)
                : (Al(cAd) + " " + Al(cSoyad)).Trim();

            if (string.IsNullOrWhiteSpace(adSoyad))
                continue;

            // Şablondaki örnek satırları otomatik atla
            var kucukAd = adSoyad.ToLower(Tr);
            if (kucukAd.StartsWith("örnek üye", StringComparison.Ordinal) ||
                kucukAd.StartsWith("ornek uye", StringComparison.Ordinal))
                continue;

            // Sıra numarası gibi yalnız rakamdan oluşan yanlış sütunları atla
            if (adSoyad.All(ch => char.IsDigit(ch) || char.IsPunctuation(ch)))
                continue;

            sonuc.Add(new UyeExcelSatir
            {
                AdSoyad = Regex.Replace(adSoyad, @"\s+", " "),
                Grup = Al(cGrup),
                Tc = Al(cTc),
                Telefon = TelefonBiçimle(Al(cTel)),
                Eposta = Al(cMail),
                Adres = Al(cAdres),
                Durum = Al(cDurum),
                GirisTarih = Al(cTarih)
            });
        }

        return sonuc;
    }

    static string TelefonBiçimle(string s)
    {
        s = (s ?? "").Trim();
        if (s.Length == 0) return "";
        // Excel sayı olarak kaydedip baştaki 0'ı düşürdüyse (örn: 5321234567 -> 10 hane 5 ile başlıyorsa) 0 ekle
        if (s.Length == 10 && s[0] == '5' && s.All(char.IsAsciiDigit))
            return "0" + s;
        return s;
    }

    static string AnahtarNorm(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim().ToLower(Tr)
            .Replace('ı', 'i').Replace('ğ', 'g').Replace('ü', 'u')
            .Replace('ş', 's').Replace('ö', 'o').Replace('ç', 'c');
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (char.IsAsciiLetterOrDigit(ch)) sb.Append(ch);
        }
        return sb.ToString();
    }

    static List<List<string>> TabloSatirlariCikar(MemoryStream ms, string? dosyaAdi)
    {
        var bas = new byte[Math.Min(512, (int)ms.Length)];
        ms.Position = 0;
        _ = ms.Read(bas, 0, bas.Length);
        ms.Position = 0;

        var uzanti = (Path.GetExtension(dosyaAdi ?? "") ?? "").ToLowerInvariant();
        var isZip = bas.Length >= 4 && bas[0] == 0x50 && bas[1] == 0x4B && (bas[2] == 0x03 || bas[2] == 0x05);
        var isOleOrBiff = (bas.Length >= 8 && bas[0] == 0xD0 && bas[1] == 0xCF && bas[2] == 0x11 && bas[3] == 0xE0)
                          || (bas.Length >= 4 && bas[0] == 0x09 && (bas[1] == 0x00 || bas[1] == 0x02 || bas[1] == 0x04 || bas[1] == 0x08));

        if (isZip || isOleOrBiff)
        {
            if (isZip && uzanti == ".ods")
            {
                ms.Position = 0;
                var ods = OkuOds(ms);
                if (ods.Count > 0) return ods;
            }
            try
            {
                ms.Position = 0;
                return OkuExcelDataReader(ms);
            }
            catch
            {
                if (isZip)
                {
                    ms.Position = 0;
                    return OkuClosedXml(ms);
                }
                throw;
            }
        }

        ms.Position = 0;
        var metin = MetinCoz(ms.ToArray());
        var trim = metin.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');

        if (trim.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
            || trim.StartsWith("<Workbook", StringComparison.OrdinalIgnoreCase)
            || trim.StartsWith("<ss:Workbook", StringComparison.OrdinalIgnoreCase))
        {
            var xmlSatirlar = OkuXmlSpreadsheet(metin);
            if (xmlSatirlar.Count > 0) return xmlSatirlar;
        }

        if (Regex.IsMatch(trim, @"<(html|!doctype|table|tbody|tr|meta)\b", RegexOptions.IgnoreCase))
        {
            var htmlSatirlar = OkuHtmlTablo(metin);
            if (htmlSatirlar.Count > 0) return htmlSatirlar;
        }

        return OkuCsv(metin);
    }

    static List<List<string>> OkuExcelDataReader(Stream ms)
    {
        var conf = new ExcelReaderConfiguration { FallbackEncoding = Encoding.GetEncoding(1254) };
        using var reader = ExcelReaderFactory.CreateReader(ms, conf);
        List<List<string>>? ilkDoluSayfa = null;

        do
        {
            var ad = (reader.Name ?? "").Trim();
            if (ad.Equals("Açıklama", StringComparison.OrdinalIgnoreCase))
                continue;

            var sayfa = new List<List<string>>();
            while (reader.Read())
            {
                var sutunSayisi = reader.FieldCount;
                var satir = new List<string>(sutunSayisi);
                var doluVar = false;
                for (var c = 0; c < sutunSayisi; c++)
                {
                    var val = HucreMetni(reader.GetValue(c));
                    if (!string.IsNullOrWhiteSpace(val)) doluVar = true;
                    satir.Add(val);
                }
                if (doluVar) sayfa.Add(satir);
            }
            if (sayfa.Count > 0 && ilkDoluSayfa is null)
                ilkDoluSayfa = sayfa;
        } while (reader.NextResult());

        return ilkDoluSayfa ?? [];
    }

    static List<List<string>> OkuClosedXml(Stream ms)
    {
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheets.FirstOrDefault(w => !w.Name.Equals("Açıklama", StringComparison.OrdinalIgnoreCase)) ?? wb.Worksheets.First();
        var kullanilan = ws.RangeUsed();
        if (kullanilan is null) return [];

        var sonuc = new List<List<string>>();
        var ilkR = kullanilan.FirstRow().RowNumber();
        var sonR = kullanilan.LastRow().RowNumber();
        var sonC = kullanilan.LastColumn().ColumnNumber();

        for (var r = ilkR; r <= sonR; r++)
        {
            var satir = new List<string>();
            var dolu = false;
            for (var c = 1; c <= sonC; c++)
            {
                var v = ws.Cell(r, c).GetFormattedString().Trim();
                if (!string.IsNullOrWhiteSpace(v)) dolu = true;
                satir.Add(v);
            }
            if (dolu) sonuc.Add(satir);
        }
        return sonuc;
    }

    static string HucreMetni(object? val)
    {
        if (val is null || val is DBNull) return "";
        if (val is DateTime dt) return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (val is double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) return "";
            if (d == Math.Truncate(d) && Math.Abs(d) < 1e15)
                return ((long)d).ToString(CultureInfo.InvariantCulture);
            return d.ToString("0.######", CultureInfo.InvariantCulture);
        }
        if (val is decimal dec)
        {
            if (dec == Math.Truncate(dec))
                return ((long)dec).ToString(CultureInfo.InvariantCulture);
            return dec.ToString("0.######", CultureInfo.InvariantCulture);
        }
        return Convert.ToString(val, Tr)?.Trim() ?? "";
    }

    static string MetinCoz(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        var utf8Strict = new UTF8Encoding(false, true);
        try { return utf8Strict.GetString(bytes); }
        catch { return Encoding.GetEncoding(1254).GetString(bytes); }
    }

    static List<List<string>> OkuXmlSpreadsheet(string xml)
    {
        var sonuc = new List<List<string>>();
        try
        {
            var doc = XDocument.Parse(xml);
            foreach (var row in doc.Descendants().Where(e => e.Name.LocalName.Equals("Row", StringComparison.OrdinalIgnoreCase)))
            {
                var satir = new List<string>();
                var colIdx = 0;
                var dolu = false;
                foreach (var cell in row.Elements().Where(e => e.Name.LocalName.Equals("Cell", StringComparison.OrdinalIgnoreCase)))
                {
                    var indexAttr = cell.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals("Index", StringComparison.OrdinalIgnoreCase));
                    if (indexAttr is not null && int.TryParse(indexAttr.Value, out var birBazli) && birBazli > 0)
                    {
                        while (colIdx < birBazli - 1) { satir.Add(""); colIdx++; }
                    }
                    var dataEl = cell.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("Data", StringComparison.OrdinalIgnoreCase));
                    var val = (dataEl?.Value ?? cell.Value ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(val)) dolu = true;
                    satir.Add(val);
                    colIdx++;
                }
                if (dolu) sonuc.Add(satir);
            }
        }
        catch { }
        return sonuc;
    }

    static List<List<string>> OkuOds(Stream ms)
    {
        var sonuc = new List<List<string>>();
        try
        {
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true);
            var entry = zip.GetEntry("content.xml");
            if (entry is null) return sonuc;
            using var s = entry.Open();
            var doc = XDocument.Load(s);
            foreach (var row in doc.Descendants().Where(e => e.Name.LocalName == "table-row"))
            {
                var satir = new List<string>();
                var dolu = false;
                foreach (var cell in row.Elements().Where(e => e.Name.LocalName is "table-cell" or "covered-table-cell"))
                {
                    var val = string.Join(" ", cell.Elements().Where(p => p.Name.LocalName == "p").Select(p => p.Value)).Trim();
                    if (!string.IsNullOrWhiteSpace(val)) dolu = true;
                    var repeatAttr = cell.Attributes().FirstOrDefault(a => a.Name.LocalName == "number-columns-repeated");
                    var tekrar = repeatAttr is not null && int.TryParse(repeatAttr.Value, out var n) ? Math.Min(n, 32) : 1;
                    for (var i = 0; i < tekrar; i++) satir.Add(val);
                }
                if (dolu) sonuc.Add(satir);
            }
        }
        catch { }
        return sonuc;
    }

    static List<List<string>> OkuHtmlTablo(string html)
    {
        var sonuc = new List<List<string>>();
        foreach (Match tr in Regex.Matches(html, @"<tr\b[^>]*>(.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var hucreler = Regex.Matches(tr.Groups[1].Value, @"<t[dh]\b[^>]*>(.*?)</t[dh]>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (hucreler.Count == 0) continue;
            var satir = new List<string>();
            var dolu = false;
            foreach (Match td in hucreler)
            {
                var ic = Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(td.Groups[1].Value, @"<[^>]+>", " ")), @"\s+", " ").Trim();
                if (!string.IsNullOrWhiteSpace(ic)) dolu = true;
                satir.Add(ic);
            }
            if (dolu) sonuc.Add(satir);
        }
        return sonuc;
    }

    static List<List<string>> OkuCsv(string metin)
    {
        var satirMetinleri = metin.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var ornekler = satirMetinleri.Where(s => !string.IsNullOrWhiteSpace(s)).Take(5).ToList();
        if (ornekler.Count == 0) return [];

        var adaylar = new[] { '\t', ';', ',' };
        var ayirici = adaylar.OrderByDescending(ch => ornekler.Sum(s => s.Count(c => c == ch))).First();

        var sonuc = new List<List<string>>();
        foreach (var ham in satirMetinleri)
        {
            if (string.IsNullOrWhiteSpace(ham)) continue;
            var parcalar = CsvSatirBol(ham, ayirici);
            if (parcalar.Any(p => !string.IsNullOrWhiteSpace(p)))
                sonuc.Add(parcalar);
        }
        return sonuc;
    }

    static List<string> CsvSatirBol(string satir, char ayirici)
    {
        var liste = new List<string>();
        var sb = new StringBuilder();
        var tirnakIci = false;

        for (var i = 0; i < satir.Length; i++)
        {
            var c = satir[i];
            if (c == '"')
            {
                if (tirnakIci && i + 1 < satir.Length && satir[i + 1] == '"') { sb.Append('"'); i++; }
                else tirnakIci = !tirnakIci;
            }
            else if (c == ayirici && !tirnakIci)
            {
                liste.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else sb.Append(c);
        }
        liste.Add(sb.ToString().Trim());
        return liste;
    }
}
