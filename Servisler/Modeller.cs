namespace DernekTakip.Servisler;

public sealed class DernekKayit
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";
    public string Telefon { get; set; } = "";
    public string Adres { get; set; } = "";
}

public sealed class KullaniciKayit
{
    public int Id { get; set; }
    public int? DernekId { get; set; }
    public string Ad { get; set; } = "";
    public string KullaniciAdi { get; set; } = "";
    public string Rol { get; set; } = "Kullanici";
    public bool Aktif { get; set; } = true;
}

public sealed class GrupKayit
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";
    public bool Aidatli { get; set; }
}

public sealed class UyeKayit
{
    public int Id { get; set; }
    public int GrupId { get; set; }
    public string GrupAd { get; set; } = "";
    public string AdSoyad { get; set; } = "";
    public string Tc { get; set; } = "";
    public string Telefon { get; set; } = "";
    public string Eposta { get; set; } = "";
    public string Adres { get; set; } = "";
    public string Durum { get; set; } = "Aktif";
    public string GirisTarih { get; set; } = "";
    public string AyrilisTarih { get; set; } = "";
    public decimal Borc { get; set; }
    public decimal Odenen { get; set; }
    public decimal Kalan => Borc - Odenen;
}

public sealed class UyeAdKayit
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";
    public string Tc { get; set; } = "";
}

public sealed class SayfaCubugu
{
    public const int Boyut = 20;
    public int Sayfa { get; init; }
    public int Toplam { get; init; }
    public int SayfaSayisi { get; init; }
    public string Taban { get; init; } = "";
    public string Anahtar { get; init; } = "sayfa";
    public bool Goster => SayfaSayisi > 1;
    public int Atla => (Sayfa - 1) * Boyut;

    public static SayfaCubugu Olustur(int toplam, int sayfa, string taban, string anahtar = "sayfa")
    {
        var n = Math.Max(1, (int)Math.Ceiling(toplam / (double)Boyut));
        if (sayfa < 1) sayfa = 1;
        if (sayfa > n) sayfa = n;
        return new SayfaCubugu { Sayfa = sayfa, Toplam = toplam, SayfaSayisi = n, Taban = taban, Anahtar = anahtar };
    }

    public string Adres(int s)
    {
        var ayir = Taban.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return Taban + ayir + Anahtar + "=" + s;
    }

    public List<T> Dilim<T>(List<T> kaynak) => kaynak.Skip(Atla).Take(Boyut).ToList();
}

public sealed class AidatTanimKayit
{
    public int GrupId { get; set; }
    public int Yil { get; set; }
    public decimal Giris { get; set; }
    public decimal[] Aylar { get; set; } = new decimal[12];
}

public sealed class AyBorcKayit
{
    public int Ay { get; set; }
    public decimal Tutar { get; set; }
}

public sealed class OdemeKayit
{
    public int Id { get; set; }
    public int UyeId { get; set; }
    public string UyeAd { get; set; } = "";
    public string Tarih { get; set; } = "";
    public decimal Tutar { get; set; }
    public string Aciklama { get; set; } = "";
    public string MakbuzNo { get; set; } = "";
}

public sealed class MakbuzKayit
{
    public OdemeKayit Odeme { get; set; } = new();
    public string DernekAd { get; set; } = "";
    public string DernekAdres { get; set; } = "";
    public string DernekTelefon { get; set; } = "";
    public string DernekLogo { get; set; } = "";
    public string UyeTc { get; set; } = "";
}

public sealed class KasaKayit
{
    public int Id { get; set; }
    public string Tarih { get; set; } = "";
    public string Tur { get; set; } = "Gelir";
    public decimal Tutar { get; set; }
    public string Aciklama { get; set; } = "";
    public string Makbuz { get; set; } = "";
}

public sealed class ToplantiKayit
{
    public int Id { get; set; }
    public string Tarih { get; set; } = "";
    public string Baslik { get; set; } = "";
    public string Notlar { get; set; } = "";
}

public sealed class EvrakKayit
{
    public int Id { get; set; }
    public string Baslik { get; set; } = "";
    public string DosyaAd { get; set; } = "";
    public string Kayit { get; set; } = "";
}

public sealed class PanelOzet
{
    public int Yil { get; set; }
    public int UyeSayisi { get; set; }
    public decimal ToplamBorc { get; set; }
    public decimal ToplamOdenen { get; set; }
    public decimal KasaGelir { get; set; }
    public decimal KasaGider { get; set; }
    public int ToplantiSayisi { get; set; }
    public int EvrakSayisi { get; set; }
    public decimal Kalan => ToplamBorc - ToplamOdenen;
    public decimal KasaBakiye => KasaGelir - KasaGider;
}

public sealed class YilRapor
{
    public int Yil { get; set; }
    public decimal Toplanacak { get; set; }
    public decimal Toplanan { get; set; }
    public decimal Devreden { get; set; }
    public decimal Toplam { get; set; }
}

public sealed class GorevliKayit
{
    public string AdSoyad { get; set; } = "";
    public string Unvan { get; set; } = "";
    public string Telefon { get; set; } = "";
}

public sealed class BankaKayit
{
    public string BankaAdi { get; set; } = "";
    public string HesapAdi { get; set; } = "";
    public string Iban { get; set; } = "";
}

public sealed class DernekProfil
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";
    public string KisaAd { get; set; } = "";
    public string KutukNo { get; set; } = "";
    public string KurulusTarihi { get; set; } = "";
    public string Statu { get; set; } = "Dernek";
    public string FaaliyetAlani { get; set; } = "";
    public string VergiDairesi { get; set; } = "";
    public string VergiNo { get; set; } = "";
    public string Amac { get; set; } = "";

    public string GorevDonemi { get; set; } = "";
    public string SonGenelKurulTarihi { get; set; } = "";
    public string KararDefterNo { get; set; } = "";

    public string Baskan { get; set; } = "";
    public string BaskanTelefon { get; set; } = "";
    public List<GorevliKayit> BaskanYardimcilari { get; set; } = [];
    public string GenelSekreter { get; set; } = "";
    public string GenelSekreterTelefon { get; set; } = "";
    public string Sayman { get; set; } = "";
    public string SaymanTelefon { get; set; } = "";
    public string Veznedar { get; set; } = "";
    public string VeznedarTelefon { get; set; } = "";
    public List<GorevliKayit> YonetimUyeleri { get; set; } = [];

    public string DenetimBaskani { get; set; } = "";
    public string DenetimBaskaniTelefon { get; set; } = "";
    public List<GorevliKayit> DenetimUyeleri { get; set; } = [];

    public string Telefon { get; set; } = "";
    public string Telefon2 { get; set; } = "";
    public string Eposta { get; set; } = "";
    public string KepAdresi { get; set; } = "";
    public string WebSitesi { get; set; } = "";
    public string SosyalMedya { get; set; } = "";
    public string Il { get; set; } = "";
    public string Ilce { get; set; } = "";
    public string Adres { get; set; } = "";
    public List<BankaKayit> BankaHesaplari { get; set; } = [];
    public string Notlar { get; set; } = "";
}
