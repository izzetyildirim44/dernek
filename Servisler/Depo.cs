using System.Globalization;
using System.Text.Json;
using System.Threading;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;

namespace DernekTakip.Servisler;

public sealed class Depo
{
    public static readonly string[] Aylar =
    [
        "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
        "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
    ];

    static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    static readonly PasswordHasher<object> Hasher = new();
    readonly string _cs;
    readonly string _kok;
    readonly string _evrakKok;

    public Depo(IWebHostEnvironment env)
    {
        _kok = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(_kok);
        _evrakKok = Path.Combine(_kok, "evrak");
        Directory.CreateDirectory(_evrakKok);
        _cs = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(_kok, "program.db"),
            ForeignKeys = true,
            Pooling = true,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 5
        }.ToString();
        Hazirla();
    }

    public string EvrakKlasor(int dernekId)
    {
        var yol = Path.Combine(_evrakKok, dernekId.ToString());
        Directory.CreateDirectory(yol);
        return yol;
    }

    static readonly AsyncLocal<SqliteConnection?> IstekBag = new();

    public IDisposable IstekAc()
    {
        var c = YeniBag();
        IstekBag.Value = c;
        return new IstekSon(c);
    }

    sealed class IstekSon(SqliteConnection c) : IDisposable
    {
        public void Dispose()
        {
            if (ReferenceEquals(IstekBag.Value, c)) IstekBag.Value = null;
            c.Dispose();
        }
    }

    sealed class Bag : IDisposable
    {
        readonly SqliteConnection _c;
        readonly bool _kapat;
        public Bag(SqliteConnection c, bool kapat) { _c = c; _kapat = kapat; }
        public SqliteCommand CreateCommand() => _c.CreateCommand();
        public SqliteTransaction BeginTransaction() => _c.BeginTransaction();
        public void Dispose() { if (_kapat) _c.Dispose(); }
        public static implicit operator SqliteConnection(Bag b) => b._c;
    }

    Bag Ac()
    {
        if (IstekBag.Value is { } olan)
            return new Bag(olan, false);
        return new Bag(YeniBag(), true);
    }

    SqliteConnection YeniBag()
    {
        var bag = new SqliteConnection(_cs);
        bag.Open();
        bag.CreateCollation("TURKCE", (x, y) =>
            string.Compare(x, y, Tr, CompareOptions.IgnoreCase));
        using var pr = bag.CreateCommand();
        pr.CommandText = "PRAGMA busy_timeout=3000;";
        pr.ExecuteNonQuery();
        return bag;
    }

    void Hazirla()
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = """
            CREATE TABLE IF NOT EXISTS Dernek (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              Ad TEXT NOT NULL,
              Telefon TEXT NOT NULL DEFAULT '',
              Adres TEXT NOT NULL DEFAULT '',
              Olusturma TEXT NOT NULL,
              ProfilJson TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS Kullanici (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              DernekId INTEGER,
              Ad TEXT NOT NULL,
              KullaniciAdi TEXT NOT NULL UNIQUE COLLATE NOCASE,
              SifreHash TEXT NOT NULL,
              Rol TEXT NOT NULL,
              Aktif INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE IF NOT EXISTS UyeGrup (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              DernekId INTEGER NOT NULL,
              Ad TEXT NOT NULL,
              Aidatli INTEGER NOT NULL DEFAULT 1,
              Sira INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS Uye (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              DernekId INTEGER NOT NULL,
              GrupId INTEGER NOT NULL,
              AdSoyad TEXT NOT NULL,
              Tc TEXT NOT NULL DEFAULT '',
              Telefon TEXT NOT NULL DEFAULT '',
              Eposta TEXT NOT NULL DEFAULT '',
              Adres TEXT NOT NULL DEFAULT '',
              Durum TEXT NOT NULL DEFAULT 'Aktif',
              Kayit TEXT NOT NULL,
              GirisTarih TEXT NOT NULL DEFAULT '',
              AyrilisTarih TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS AidatTanim (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              DernekId INTEGER NOT NULL,
              GrupId INTEGER NOT NULL,
              Yil INTEGER NOT NULL,
              A1 REAL NOT NULL DEFAULT 0, A2 REAL NOT NULL DEFAULT 0, A3 REAL NOT NULL DEFAULT 0,
              A4 REAL NOT NULL DEFAULT 0, A5 REAL NOT NULL DEFAULT 0, A6 REAL NOT NULL DEFAULT 0,
              A7 REAL NOT NULL DEFAULT 0, A8 REAL NOT NULL DEFAULT 0, A9 REAL NOT NULL DEFAULT 0,
              A10 REAL NOT NULL DEFAULT 0, A11 REAL NOT NULL DEFAULT 0, A12 REAL NOT NULL DEFAULT 0,
              Giris REAL NOT NULL DEFAULT 0,
              UNIQUE(DernekId, GrupId, Yil)
            );
            CREATE TABLE IF NOT EXISTS AidatBorc (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              DernekId INTEGER NOT NULL,
              UyeId INTEGER NOT NULL,
              Yil INTEGER NOT NULL,
              Ay INTEGER NOT NULL,
              Tutar REAL NOT NULL,
              UNIQUE(UyeId, Yil, Ay)
            );
            CREATE TABLE IF NOT EXISTS Odeme (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              DernekId INTEGER NOT NULL,
              UyeId INTEGER NOT NULL,
              Tarih TEXT NOT NULL,
              Tutar REAL NOT NULL,
              Aciklama TEXT NOT NULL DEFAULT '',
              MakbuzNo TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Kasa (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              DernekId INTEGER NOT NULL,
              Tarih TEXT NOT NULL,
              Tur TEXT NOT NULL,
              Tutar REAL NOT NULL,
              Aciklama TEXT NOT NULL,
              OdemeId INTEGER,
              Makbuz TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS Toplanti (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              DernekId INTEGER NOT NULL,
              Tarih TEXT NOT NULL,
              Baslik TEXT NOT NULL,
              Notlar TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS Evrak (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              DernekId INTEGER NOT NULL,
              Baslik TEXT NOT NULL,
              DosyaAd TEXT NOT NULL,
              Kayit TEXT NOT NULL
            );
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=NORMAL;
            PRAGMA temp_store=MEMORY;
            PRAGMA mmap_size=268435456;
            PRAGMA cache_size=-16000;
            CREATE INDEX IF NOT EXISTS ix_uye_dernek ON Uye(DernekId, Durum);
            CREATE INDEX IF NOT EXISTS ix_uyegrup_dernek ON UyeGrup(DernekId);
            CREATE INDEX IF NOT EXISTS ix_aidatborc_uye_yil ON AidatBorc(UyeId, Yil);
            CREATE INDEX IF NOT EXISTS ix_aidatborc_dernek_yil ON AidatBorc(DernekId, Yil);
            CREATE INDEX IF NOT EXISTS ix_odeme_uye_tarih ON Odeme(UyeId, Tarih);
            CREATE INDEX IF NOT EXISTS ix_odeme_dernek_tarih ON Odeme(DernekId, Tarih);
            CREATE INDEX IF NOT EXISTS ix_kasa_dernek_tarih ON Kasa(DernekId, Tarih);
            CREATE INDEX IF NOT EXISTS ix_toplanti_dernek ON Toplanti(DernekId, Tarih);
            CREATE INDEX IF NOT EXISTS ix_evrak_dernek ON Evrak(DernekId);
            CREATE INDEX IF NOT EXISTS ix_aidattanim_dernek ON AidatTanim(DernekId, Yil);
            """;
        k.ExecuteNonQuery();
        SemaGuncelle(bag);
    }

    static void SemaGuncelle(SqliteConnection bag)
    {
        SutunEkle(bag, "ALTER TABLE Uye ADD COLUMN GirisTarih TEXT NOT NULL DEFAULT ''");
        SutunEkle(bag, "ALTER TABLE Kasa ADD COLUMN Makbuz TEXT NOT NULL DEFAULT ''");
        SutunEkle(bag, "ALTER TABLE Uye ADD COLUMN AyrilisTarih TEXT NOT NULL DEFAULT ''");
        SutunEkle(bag, "ALTER TABLE Dernek ADD COLUMN ProfilJson TEXT NOT NULL DEFAULT ''");
        try
        {
            using var a = bag.CreateCommand();
            a.CommandText = "ALTER TABLE AidatTanim ADD COLUMN Giris REAL NOT NULL DEFAULT 0";
            a.ExecuteNonQuery();
        }
        catch (SqliteException) { /* sütun var */ }
        using (var k = bag.CreateCommand())
        {
            k.CommandText = """
                UPDATE Uye SET GirisTarih=substr(Kayit,1,10)
                WHERE GirisTarih='' AND length(Kayit)>=10
                """;
            k.ExecuteNonQuery();
        }
        using (var k = bag.CreateCommand())
        {
            k.CommandText = """
                UPDATE AidatTanim SET
                  A1=A1+A2+A3+A4+A5+A6+A7+A8+A9+A10+A11+A12,
                  A2=0,A3=0,A4=0,A5=0,A6=0,A7=0,A8=0,A9=0,A10=0,A11=0,A12=0
                WHERE A2+A3+A4+A5+A6+A7+A8+A9+A10+A11+A12 != 0;
                """;
            k.ExecuteNonQuery();
        }
        using (var k = bag.CreateCommand())
        {
            k.CommandText = "SELECT 1 FROM AidatBorc WHERE Ay BETWEEN 2 AND 12 LIMIT 1";
            if (k.ExecuteScalar() is null) return;
        }
        using (var k = bag.CreateCommand())
        {
            k.CommandText = """
                INSERT INTO AidatBorc(DernekId, UyeId, Yil, Ay, Tutar)
                SELECT DernekId, UyeId, Yil, 1, SUM(Tutar)
                FROM AidatBorc WHERE Ay BETWEEN 1 AND 12
                GROUP BY DernekId, UyeId, Yil
                ON CONFLICT(UyeId, Yil, Ay) DO UPDATE SET Tutar=excluded.Tutar;
                DELETE FROM AidatBorc WHERE Ay BETWEEN 2 AND 12;
                """;
            k.ExecuteNonQuery();
        }
    }

    static void SutunEkle(SqliteConnection bag, string sql)
    {
        try
        {
            using var a = bag.CreateCommand();
            a.CommandText = sql;
            a.ExecuteNonQuery();
        }
        catch (SqliteException) { /* sütun var */ }
    }

    int _kurulu = -1;
    List<DernekKayit>? _dernekOnbellek;
    readonly Dictionary<int, List<int>> _yilOnbellek = [];
    readonly Dictionary<int, List<int>> _aidatYilOnbellek = [];
    readonly object _onbellek = new();

    void OnbellekDusur(int dernekId = 0)
    {
        lock (_onbellek)
        {
            _dernekOnbellek = null;
            if (dernekId == 0)
            {
                _yilOnbellek.Clear();
                _aidatYilOnbellek.Clear();
            }
            else
            {
                _yilOnbellek.Remove(dernekId);
                _aidatYilOnbellek.Remove(dernekId);
            }
        }
    }

    public bool KuruluMu()
    {
        if (_kurulu == 1) return true;
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = "SELECT EXISTS(SELECT 1 FROM Kullanici LIMIT 1)";
        var varMi = Convert.ToInt32(k.ExecuteScalar()) == 1;
        if (varMi) _kurulu = 1;
        return varMi;
    }

    public void Kur(string dernekAd, string yoneticiAd, string kullaniciAdi, string sifre)
    {
        dernekAd = dernekAd.Trim();
        yoneticiAd = yoneticiAd.Trim();
        kullaniciAdi = kullaniciAdi.Trim();
        if (dernekAd.Length < 2 || yoneticiAd.Length < 2 || kullaniciAdi.Length < 3 || sifre.Length < 6)
            throw new InvalidOperationException("Dernek adı, yönetici adı, kullanıcı adı (en az 3) ve şifre (en az 6) gerekli.");
        using var bag = Ac();
        using var tx = bag.BeginTransaction();
        var dernekId = Ekle(bag, tx, "INSERT INTO Dernek(Ad, Olusturma) VALUES($a,$t)",
            ("$a", dernekAd), ("$t", Simdi()));
        GrupVarsayilan(bag, tx, dernekId);
        Ekle(bag, tx, """
            INSERT INTO Kullanici(DernekId, Ad, KullaniciAdi, SifreHash, Rol, Aktif)
            VALUES($d,$a,$k,$s,'Sistem',1)
            """,
            ("$d", dernekId), ("$a", yoneticiAd), ("$k", kullaniciAdi),
            ("$s", Hasher.HashPassword(null!, sifre)));
        tx.Commit();
        _kurulu = 1;
        OnbellekDusur();
    }

    void GrupVarsayilan(SqliteConnection bag, SqliteTransaction tx, int dernekId)
    {
        var gruplar = new (string Ad, int Aidat, int Sira)[]
        {
            ("Üyeler", 1, 1), ("Yöneticiler", 1, 2), ("Çalışanlar", 0, 3),
            ("Bağış", 0, 4), ("Diğer", 0, 5)
        };
        foreach (var g in gruplar)
        {
            Ekle(bag, tx, "INSERT INTO UyeGrup(DernekId, Ad, Aidatli, Sira) VALUES($d,$a,$t,$s)",
                ("$d", dernekId), ("$a", g.Ad), ("$t", g.Aidat), ("$s", g.Sira));
        }
    }

    public KullaniciKayit? GirisDogrula(string kullaniciAdi, string sifre)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = "SELECT Id, DernekId, Ad, KullaniciAdi, SifreHash, Rol, Aktif FROM Kullanici WHERE KullaniciAdi=$k";
        k.Parameters.AddWithValue("$k", kullaniciAdi.Trim());
        using var r = k.ExecuteReader();
        if (!r.Read() || r.GetInt32(6) == 0) return null;
        var hash = r.GetString(4);
        if (Hasher.VerifyHashedPassword(null!, hash, sifre) == PasswordVerificationResult.Failed)
            return null;
        return OkuKullanici(r);
    }

    public List<DernekKayit> DernekListe()
    {
        lock (_onbellek)
        {
            if (_dernekOnbellek is not null) return _dernekOnbellek;
        }
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = "SELECT Id, Ad, Telefon, Adres FROM Dernek ORDER BY Ad COLLATE TURKCE";
        var liste = OkuListe(k, OkuDernek);
        lock (_onbellek) { _dernekOnbellek = liste; }
        return liste;
    }

    public DernekKayit? DernekGetir(int id)
    {
        var liste = DernekListe();
        for (var i = 0; i < liste.Count; i++)
            if (liste[i].Id == id) return liste[i];
        return null;
    }

    public int DernekEkle(string ad, string telefon, string adres)
    {
        ad = ad.Trim();
        if (ad.Length < 2) throw new InvalidOperationException("Dernek adı gerekli.");
        using var bag = Ac();
        using var tx = bag.BeginTransaction();
        var id = Ekle(bag, tx, "INSERT INTO Dernek(Ad, Telefon, Adres, Olusturma) VALUES($a,$t,$d,$o)",
            ("$a", ad), ("$t", telefon.Trim()), ("$d", adres.Trim()), ("$o", Simdi()));
        GrupVarsayilan(bag, tx, id);
        tx.Commit();
        OnbellekDusur();
        return id;
    }

    public void DernekGuncelle(int id, string ad, string telefon, string adres)
    {
        ad = ad.Trim();
        if (ad.Length < 2) throw new InvalidOperationException("Dernek adı gerekli.");
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = "UPDATE Dernek SET Ad=$a, Telefon=$t, Adres=$d WHERE Id=$i";
        k.Parameters.AddWithValue("$a", ad);
        k.Parameters.AddWithValue("$t", telefon.Trim());
        k.Parameters.AddWithValue("$d", adres.Trim());
        k.Parameters.AddWithValue("$i", id);
        k.ExecuteNonQuery();
        OnbellekDusur();
    }

    public DernekProfil? DernekProfilGetir(int id)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = "SELECT Id, Ad, Telefon, Adres, IFNULL(ProfilJson, '') FROM Dernek WHERE Id=$i";
        k.Parameters.AddWithValue("$i", id);
        using var r = k.ExecuteReader();
        if (!r.Read()) return null;

        var json = r.GetString(4);
        DernekProfil? p = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try { p = JsonSerializer.Deserialize<DernekProfil>(json); }
            catch { p = null; }
        }
        p ??= new DernekProfil();
        p.Id = r.GetInt32(0);
        p.Ad = r.GetString(1);
        p.Telefon = r.GetString(2);
        p.Adres = r.GetString(3);
        p.BaskanYardimcilari ??= [];
        p.YonetimUyeleri ??= [];
        p.DenetimUyeleri ??= [];
        p.BankaHesaplari ??= [];
        return p;
    }

    public void DernekProfilKaydet(int id, DernekProfil p)
    {
        var ad = (p.Ad ?? "").Trim();
        if (ad.Length < 2) throw new InvalidOperationException("Dernek adı en az 2 karakter olmalıdır.");

        p.Id = id;
        p.Ad = ad;
        p.Telefon = (p.Telefon ?? "").Trim();
        p.Adres = (p.Adres ?? "").Trim();
        p.BaskanYardimcilari ??= [];
        p.YonetimUyeleri ??= [];
        p.DenetimUyeleri ??= [];
        p.BankaHesaplari ??= [];

        var json = JsonSerializer.Serialize(p);
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = "UPDATE Dernek SET Ad=$a, Telefon=$t, Adres=$d, ProfilJson=$j WHERE Id=$i";
        k.Parameters.AddWithValue("$a", p.Ad);
        k.Parameters.AddWithValue("$t", p.Telefon);
        k.Parameters.AddWithValue("$d", p.Adres);
        k.Parameters.AddWithValue("$j", json);
        k.Parameters.AddWithValue("$i", id);
        k.ExecuteNonQuery();
        OnbellekDusur();
    }

    public void DernekSil(int id, int calisanId)
    {
        if (id == calisanId)
            throw new InvalidOperationException("Çalışılan dernek silinemez. Önce başka dernek seçin.");
        using var bag = Ac();
        using var say = bag.CreateCommand();
        say.CommandText = "SELECT COUNT(*) FROM Dernek";
        if (Convert.ToInt32(say.ExecuteScalar()) <= 1)
            throw new InvalidOperationException("Son dernek silinemez.");
        using var tx = bag.BeginTransaction();
        void Sil(string sql)
        {
            using var k = bag.CreateCommand();
            k.Transaction = tx;
            k.CommandText = sql;
            k.Parameters.AddWithValue("$d", id);
            k.ExecuteNonQuery();
        }
        Sil("DELETE FROM Evrak WHERE DernekId=$d");
        Sil("DELETE FROM Toplanti WHERE DernekId=$d");
        Sil("DELETE FROM Kasa WHERE DernekId=$d");
        Sil("DELETE FROM Odeme WHERE DernekId=$d");
        Sil("DELETE FROM AidatBorc WHERE DernekId=$d");
        Sil("DELETE FROM AidatTanim WHERE DernekId=$d");
        Sil("DELETE FROM Uye WHERE DernekId=$d");
        Sil("DELETE FROM UyeGrup WHERE DernekId=$d");
        Sil("DELETE FROM Kullanici WHERE DernekId=$d AND Rol<>'Sistem'");
        using (var k = bag.CreateCommand())
        {
            k.Transaction = tx;
            k.CommandText = "UPDATE Kullanici SET DernekId=NULL WHERE DernekId=$d";
            k.Parameters.AddWithValue("$d", id);
            k.ExecuteNonQuery();
        }
        Sil("DELETE FROM Dernek WHERE Id=$d");
        tx.Commit();
        var evrakYol = Path.Combine(_evrakKok, id.ToString());
        try { if (Directory.Exists(evrakYol)) Directory.Delete(evrakYol, true); } catch { /* yedek */ }
        OnbellekDusur();
    }

    public int KullaniciSay(int? dernekId, bool sistem)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        if (sistem)
            k.CommandText = "SELECT COUNT(*) FROM Kullanici";
        else
        {
            k.CommandText = "SELECT COUNT(*) FROM Kullanici WHERE DernekId=$d";
            k.Parameters.AddWithValue("$d", dernekId ?? 0);
        }
        return Convert.ToInt32(k.ExecuteScalar());
    }

    public List<KullaniciKayit> KullaniciListe(int? dernekId, bool sistem, int atla = 0, int al = 0)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        if (sistem)
            k.CommandText = "SELECT Id, DernekId, Ad, KullaniciAdi, Rol, Aktif FROM Kullanici ORDER BY Ad COLLATE TURKCE";
        else
        {
            k.CommandText = "SELECT Id, DernekId, Ad, KullaniciAdi, Rol, Aktif FROM Kullanici WHERE DernekId=$d ORDER BY Ad COLLATE TURKCE";
            k.Parameters.AddWithValue("$d", dernekId ?? 0);
        }
        LimitEkle(k, atla, al);
        return OkuListe(k, OkuKullaniciSatir);
    }

    public KullaniciKayit? KullaniciGetir(int id, int? dernekId, bool sistem)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = "SELECT Id, DernekId, Ad, KullaniciAdi, Rol, Aktif FROM Kullanici WHERE Id=$i";
        k.Parameters.AddWithValue("$i", id);
        if (!sistem)
        {
            k.CommandText += " AND DernekId=$d";
            k.Parameters.AddWithValue("$d", dernekId ?? 0);
        }
        return OkuTek(k, OkuKullaniciSatir);
    }

    public void KullaniciEkle(int? dernekId, string ad, string kullaniciAdi, string sifre, string rol)
    {
        ad = ad.Trim();
        kullaniciAdi = kullaniciAdi.Trim();
        if (ad.Length < 2 || kullaniciAdi.Length < 3 || sifre.Length < 6)
            throw new InvalidOperationException("Ad, kullanıcı adı (en az 3) ve şifre (en az 6) gerekli.");
        if (rol is not (Oturum.Sistem or Oturum.Yonetici or Oturum.Kullanici))
            rol = Oturum.Kullanici;
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = """
            INSERT INTO Kullanici(DernekId, Ad, KullaniciAdi, SifreHash, Rol, Aktif)
            VALUES($d,$a,$k,$s,$r,1)
            """;
        k.Parameters.AddWithValue("$d", (object?)dernekId ?? DBNull.Value);
        k.Parameters.AddWithValue("$a", ad);
        k.Parameters.AddWithValue("$k", kullaniciAdi);
        k.Parameters.AddWithValue("$s", Hasher.HashPassword(null!, sifre));
        k.Parameters.AddWithValue("$r", rol);
        try { k.ExecuteNonQuery(); }
        catch (SqliteException) { throw new InvalidOperationException("Bu kullanıcı adı kayıtlı."); }
    }

    public void KullaniciGuncelle(int id, string ad, string kullaniciAdi, string? sifre, string rol)
    {
        ad = (ad ?? "").Trim();
        kullaniciAdi = (kullaniciAdi ?? "").Trim();
        sifre = string.IsNullOrWhiteSpace(sifre) ? null : sifre.Trim();
        if (ad.Length < 2 || kullaniciAdi.Length < 3)
            throw new InvalidOperationException("Ad ve kullanıcı adı (en az 3) gerekli.");
        if (sifre is not null && sifre.Length < 6)
            throw new InvalidOperationException("Şifre en az 6 karakter olmalı.");
        if (rol is not (Oturum.Sistem or Oturum.Yonetici or Oturum.Kullanici))
            rol = Oturum.Kullanici;
        using var bag = Ac();
        using var k = bag.CreateCommand();
        if (sifre is null)
        {
            k.CommandText = """
                UPDATE Kullanici SET Ad=$a, KullaniciAdi=$k, Rol=$r WHERE Id=$i
                """;
        }
        else
        {
            k.CommandText = """
                UPDATE Kullanici SET Ad=$a, KullaniciAdi=$k, Rol=$r, SifreHash=$s WHERE Id=$i
                """;
            k.Parameters.AddWithValue("$s", Hasher.HashPassword(null!, sifre));
        }
        k.Parameters.AddWithValue("$a", ad);
        k.Parameters.AddWithValue("$k", kullaniciAdi);
        k.Parameters.AddWithValue("$r", rol);
        k.Parameters.AddWithValue("$i", id);
        try { k.ExecuteNonQuery(); }
        catch (SqliteException) { throw new InvalidOperationException("Bu kullanıcı adı kayıtlı."); }
    }

    public void KullaniciSil(int id, int silenId)
    {
        if (id == silenId) throw new InvalidOperationException("Kendi hesabınızı silemezsiniz.");
        using var bag = Ac();
        using var say = bag.CreateCommand();
        say.CommandText = "SELECT COUNT(*) FROM Kullanici";
        if (Convert.ToInt32(say.ExecuteScalar()) <= 1)
            throw new InvalidOperationException("Son kullanıcı silinemez.");
        using var k = bag.CreateCommand();
        k.CommandText = "DELETE FROM Kullanici WHERE Id=$i";
        k.Parameters.AddWithValue("$i", id);
        k.ExecuteNonQuery();
    }

    public List<GrupKayit> GrupListe(int dernekId)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = "SELECT Id, Ad, Aidatli FROM UyeGrup WHERE DernekId=$d ORDER BY Sira, Ad COLLATE TURKCE";
        k.Parameters.AddWithValue("$d", dernekId);
        return OkuListe(k, r => new GrupKayit { Id = r.GetInt32(0), Ad = r.GetString(1), Aidatli = r.GetInt32(2) == 1 });
    }

    public List<GrupKayit> AidatliGruplar(int dernekId) =>
        GrupListe(dernekId).Where(g => g.Aidatli).ToList();

    public int UyeSay(int dernekId, string? durum = null)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        var durumSql = durum is "Aktif" or "Ayrıldı" ? " AND Durum=$s" : "";
        k.CommandText = "SELECT COUNT(*) FROM Uye WHERE DernekId=$d" + durumSql;
        k.Parameters.AddWithValue("$d", dernekId);
        if (durum is "Aktif" or "Ayrıldı") k.Parameters.AddWithValue("$s", durum);
        return Convert.ToInt32(k.ExecuteScalar());
    }

    public List<UyeKayit> UyeListe(int dernekId, int yil = 0, string? durum = null, int atla = 0, int al = 0, int? uyeId = null)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        var durumSql = durum is "Aktif" or "Ayrıldı" ? " AND u.Durum=$s" : "";
        var idSql = uyeId is int ? " AND u.Id=$i" : "";
        if (yil > 0)
        {
            k.CommandText = $"""
                SELECT u.Id, u.GrupId, g.Ad, u.AdSoyad, u.Tc, u.Telefon, u.Eposta, u.Adres, u.Durum,
                  u.GirisTarih, u.AyrilisTarih, IFNULL(b.Toplam, 0), IFNULL(o.Toplam, 0)
                FROM Uye u JOIN UyeGrup g ON g.Id=u.GrupId AND g.DernekId=u.DernekId
                LEFT JOIN (
                  SELECT UyeId, SUM(Tutar) AS Toplam FROM AidatBorc WHERE DernekId=$d AND Yil=$y GROUP BY UyeId
                ) b ON b.UyeId=u.Id
                LEFT JOIN (
                  SELECT UyeId, SUM(Tutar) AS Toplam FROM Odeme WHERE DernekId=$d AND Tarih>=$a AND Tarih<$z GROUP BY UyeId
                ) o ON o.UyeId=u.Id
                WHERE u.DernekId=$d{durumSql}{idSql}
                ORDER BY u.AdSoyad COLLATE TURKCE
                """;
            k.Parameters.AddWithValue("$y", yil);
            k.Parameters.AddWithValue("$a", yil + "-01-01");
            k.Parameters.AddWithValue("$z", (yil + 1) + "-01-01");
        }
        else
        {
            k.CommandText = $"""
                SELECT u.Id, u.GrupId, g.Ad, u.AdSoyad, u.Tc, u.Telefon, u.Eposta, u.Adres, u.Durum,
                  u.GirisTarih, u.AyrilisTarih, IFNULL(b.Toplam, 0), IFNULL(o.Toplam, 0)
                FROM Uye u JOIN UyeGrup g ON g.Id=u.GrupId AND g.DernekId=u.DernekId
                LEFT JOIN (SELECT UyeId, SUM(Tutar) AS Toplam FROM AidatBorc WHERE DernekId=$d GROUP BY UyeId) b ON b.UyeId=u.Id
                LEFT JOIN (SELECT UyeId, SUM(Tutar) AS Toplam FROM Odeme WHERE DernekId=$d GROUP BY UyeId) o ON o.UyeId=u.Id
                WHERE u.DernekId=$d{durumSql}{idSql}
                ORDER BY u.AdSoyad COLLATE TURKCE
                """;
        }
        k.Parameters.AddWithValue("$d", dernekId);
        if (durum is "Aktif" or "Ayrıldı") k.Parameters.AddWithValue("$s", durum);
        if (uyeId is int uid) k.Parameters.AddWithValue("$i", uid);
        LimitEkle(k, atla, al);
        return OkuListe(k, OkuUye);
    }

    public UyeKayit? UyeGetir(int dernekId, int uyeId, int yil = 0)
    {
        var l = UyeListe(dernekId, yil, null, 0, 1, uyeId);
        return l.Count == 0 ? null : l[0];
    }

    public List<UyeAdKayit> UyeAdListe(int dernekId, string? durum = null)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        var durumSql = durum is "Aktif" or "Ayrıldı" ? " AND Durum=$s" : "";
        k.CommandText = "SELECT Id, AdSoyad, Tc FROM Uye WHERE DernekId=$d" + durumSql + " ORDER BY AdSoyad COLLATE TURKCE";
        k.Parameters.AddWithValue("$d", dernekId);
        if (durum is "Aktif" or "Ayrıldı") k.Parameters.AddWithValue("$s", durum);
        return OkuListe(k, r => new UyeAdKayit { Id = r.GetInt32(0), Ad = r.GetString(1), Tc = r.GetString(2) });
    }

    const string BorcluFrom = """
        FROM Uye u JOIN UyeGrup g ON g.Id=u.GrupId AND g.DernekId=u.DernekId
        LEFT JOIN (
          SELECT UyeId, SUM(Tutar) AS Toplam FROM AidatBorc WHERE DernekId=$d AND Yil=$y GROUP BY UyeId
        ) b ON b.UyeId=u.Id
        LEFT JOIN (
          SELECT UyeId, SUM(Tutar) AS Toplam FROM Odeme WHERE DernekId=$d AND Tarih>=$a AND Tarih<$z GROUP BY UyeId
        ) o ON o.UyeId=u.Id
        WHERE u.DernekId=$d AND ($g=0 OR u.GrupId=$g)
          AND IFNULL(b.Toplam, 0) - IFNULL(o.Toplam, 0) > 0.004
        """;

    void BorcluParametre(SqliteCommand k, int dernekId, int yil, int grupId)
    {
        k.Parameters.AddWithValue("$d", dernekId);
        k.Parameters.AddWithValue("$y", yil);
        k.Parameters.AddWithValue("$a", yil + "-01-01");
        k.Parameters.AddWithValue("$z", (yil + 1) + "-01-01");
        k.Parameters.AddWithValue("$g", grupId);
    }

    public int UyeBorcluSay(int dernekId, int yil, int grupId)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = "SELECT COUNT(*) " + BorcluFrom;
        BorcluParametre(k, dernekId, yil, grupId);
        return Convert.ToInt32(k.ExecuteScalar());
    }

    public List<UyeKayit> UyeBorcluListe(int dernekId, int yil, int grupId, int atla = 0, int al = 0)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = $"""
            SELECT u.Id, u.GrupId, g.Ad, u.AdSoyad, u.Tc, u.Telefon, u.Eposta, u.Adres, u.Durum,
              u.GirisTarih, u.AyrilisTarih, IFNULL(b.Toplam, 0) AS Borc, IFNULL(o.Toplam, 0) AS Odenen
            {BorcluFrom}
            ORDER BY (IFNULL(b.Toplam, 0) - IFNULL(o.Toplam, 0)) DESC, u.AdSoyad COLLATE TURKCE
            """;
        BorcluParametre(k, dernekId, yil, grupId);
        LimitEkle(k, atla, al);
        return OkuListe(k, OkuUye);
    }

    public void UyeKaydet(int dernekId, int id, int grupId, string adSoyad, string tc, string telefon, string eposta, string adres, string durum, string girisTarih, string ayrilisTarih)
    {
        adSoyad = (adSoyad ?? "").Trim();
        if (adSoyad.Length < 2) throw new InvalidOperationException("Ad soyad gerekli.");
        if (durum is not ("Aktif" or "Ayrıldı")) durum = "Aktif";
        girisTarih = TarihGun(girisTarih);
        ayrilisTarih = TarihGun(ayrilisTarih);
        if (girisTarih.Length == 0) girisTarih = DateTime.Today.ToString("yyyy-MM-dd");
        GrupKontrol(dernekId, grupId);
        using var bag = Ac();
        using var k = bag.CreateCommand();
        if (id == 0)
        {
            k.CommandText = """
                INSERT INTO Uye(DernekId, GrupId, AdSoyad, Tc, Telefon, Eposta, Adres, Durum, Kayit, GirisTarih, AyrilisTarih)
                VALUES($d,$g,$a,$t,$p,$e,$r,$s,$k,$gi,$ay)
                """;
            k.Parameters.AddWithValue("$k", Simdi());
        }
        else
        {
            k.CommandText = """
                UPDATE Uye SET GrupId=$g, AdSoyad=$a, Tc=$t, Telefon=$p, Eposta=$e, Adres=$r, Durum=$s,
                  GirisTarih=$gi, AyrilisTarih=$ay
                WHERE Id=$i AND DernekId=$d
                """;
            k.Parameters.AddWithValue("$i", id);
        }
        k.Parameters.AddWithValue("$d", dernekId);
        k.Parameters.AddWithValue("$g", grupId);
        k.Parameters.AddWithValue("$a", adSoyad);
        k.Parameters.AddWithValue("$t", (tc ?? "").Trim());
        k.Parameters.AddWithValue("$p", (telefon ?? "").Trim());
        k.Parameters.AddWithValue("$e", (eposta ?? "").Trim());
        k.Parameters.AddWithValue("$r", (adres ?? "").Trim());
        k.Parameters.AddWithValue("$s", durum);
        k.Parameters.AddWithValue("$gi", girisTarih);
        k.Parameters.AddWithValue("$ay", ayrilisTarih);
        k.ExecuteNonQuery();
        if (id == 0)
        {
            using var idk = bag.CreateCommand();
            idk.CommandText = "SELECT last_insert_rowid()";
            var uyeId = Convert.ToInt32(idk.ExecuteScalar());
            YeniUyeBorcYaz(bag, dernekId, uyeId, grupId);
        }
        OnbellekDusur(dernekId);
    }

    static void YeniUyeBorcYaz(SqliteConnection bag, int dernekId, int uyeId, int grupId)
    {
        var yil = DateTime.Now.Year;
        using var k = bag.CreateCommand();
        k.CommandText = """
            SELECT g.Aidatli, IFNULL(t.A1,0), IFNULL(t.Giris,0)
            FROM UyeGrup g
            LEFT JOIN AidatTanim t ON t.DernekId=g.DernekId AND t.GrupId=g.Id AND t.Yil=$y
            WHERE g.Id=$g AND g.DernekId=$d
            """;
        k.Parameters.AddWithValue("$d", dernekId);
        k.Parameters.AddWithValue("$g", grupId);
        k.Parameters.AddWithValue("$y", yil);
        using var r = k.ExecuteReader();
        if (!r.Read() || r.GetInt32(0) == 0) return;
        var yillik = Convert.ToDecimal(r.GetDouble(1));
        var giris = Convert.ToDecimal(r.GetDouble(2));
        r.Close();
        if (yillik > 0) BorcSatirYaz(bag, null, dernekId, uyeId, yil, 1, yillik);
        if (giris > 0) BorcSatirYaz(bag, null, dernekId, uyeId, yil, 0, giris);
    }

    public void UyeSil(int dernekId, int id)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = "DELETE FROM AidatBorc WHERE UyeId=$i AND DernekId=$d; DELETE FROM Uye WHERE Id=$i AND DernekId=$d;";
        k.Parameters.AddWithValue("$i", id);
        k.Parameters.AddWithValue("$d", dernekId);
        k.ExecuteNonQuery();
        OnbellekDusur(dernekId);
    }

    public AidatTanimKayit? AidatTanimGetir(int dernekId, int grupId, int yil)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = "SELECT GrupId, Yil, A1,A2,A3,A4,A5,A6,A7,A8,A9,A10,A11,A12, Giris FROM AidatTanim WHERE DernekId=$d AND GrupId=$g AND Yil=$y";
        k.Parameters.AddWithValue("$d", dernekId);
        k.Parameters.AddWithValue("$g", grupId);
        k.Parameters.AddWithValue("$y", yil);
        return OkuTek(k, r =>
        {
            var t = new AidatTanimKayit { GrupId = r.GetInt32(0), Yil = r.GetInt32(1), Giris = Convert.ToDecimal(r.GetDouble(14)) };
            for (var i = 0; i < 12; i++) t.Aylar[i] = Convert.ToDecimal(r.GetDouble(2 + i));
            return t;
        });
    }

    public List<int> AidatYillari(int dernekId)
    {
        lock (_onbellek)
        {
            if (_aidatYilOnbellek.TryGetValue(dernekId, out var hazir)) return hazir;
        }
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = """
            SELECT DISTINCT Yil FROM AidatTanim WHERE DernekId=$d
            UNION
            SELECT DISTINCT Yil FROM AidatBorc WHERE DernekId=$d
            """;
        k.Parameters.AddWithValue("$d", dernekId);
        var set = new HashSet<int>();
        using (var r = k.ExecuteReader())
        {
            while (r.Read())
            {
                var y = r.GetInt32(0);
                if (YilGecerli(y)) set.Add(y);
            }
        }
        set.Add(DateTime.Now.Year);
        var liste = set.OrderByDescending(x => x).ToList();
        lock (_onbellek) { _aidatYilOnbellek[dernekId] = liste; }
        return liste;
    }

    public int AidatKaydetVeBorclandir(int dernekId, int grupId, int yil, decimal yillik, decimal giris)
    {
        GrupKontrol(dernekId, grupId);
        var g = GrupListe(dernekId).First(x => x.Id == grupId);
        if (!g.Aidatli) throw new InvalidOperationException("Bu grup aidat ödemez.");
        if (yil < 2000 || yil > 2100) throw new InvalidOperationException("Yıl geçersiz.");
        if (yillik <= 0) throw new InvalidOperationException("Yıllık aidat tutarı gerekli.");
        yillik = Math.Round(yillik, 2);
        giris = Math.Round(Math.Max(0, giris), 2);
        using var bag = Ac();
        using var tx = bag.BeginTransaction();
        using (var k = bag.CreateCommand())
        {
            k.Transaction = tx;
            k.CommandText = """
                INSERT INTO AidatTanim(DernekId, GrupId, Yil, A1,A2,A3,A4,A5,A6,A7,A8,A9,A10,A11,A12, Giris)
                VALUES($d,$g,$y,$1,0,0,0,0,0,0,0,0,0,0,0,$gi)
                ON CONFLICT(DernekId, GrupId, Yil) DO UPDATE SET
                  A1=$1,A2=0,A3=0,A4=0,A5=0,A6=0,A7=0,A8=0,A9=0,A10=0,A11=0,A12=0, Giris=$gi
                """;
            k.Parameters.AddWithValue("$d", dernekId);
            k.Parameters.AddWithValue("$g", grupId);
            k.Parameters.AddWithValue("$y", yil);
            k.Parameters.AddWithValue("$1", yillik);
            k.Parameters.AddWithValue("$gi", giris);
            k.ExecuteNonQuery();
        }
        List<(int Id, string Kayit)> uyeler;
        using (var k = bag.CreateCommand())
        {
            k.Transaction = tx;
            k.CommandText = "SELECT Id, Kayit FROM Uye WHERE DernekId=$d AND GrupId=$g AND Durum='Aktif'";
            k.Parameters.AddWithValue("$d", dernekId);
            k.Parameters.AddWithValue("$g", grupId);
            uyeler = [];
            using var r = k.ExecuteReader();
            while (r.Read()) uyeler.Add((r.GetInt32(0), r.GetString(1)));
        }
        using (var k = bag.CreateCommand())
        {
            k.Transaction = tx;
            k.CommandText = """
                DELETE FROM AidatBorc WHERE DernekId=$d AND Yil=$y AND Ay BETWEEN 2 AND 12
                  AND UyeId IN (SELECT Id FROM Uye WHERE DernekId=$d AND GrupId=$g)
                """;
            k.Parameters.AddWithValue("$d", dernekId);
            k.Parameters.AddWithValue("$y", yil);
            k.Parameters.AddWithValue("$g", grupId);
            k.ExecuteNonQuery();
        }
        var yilOn = yil.ToString() + "-";
        var say = 0;
        foreach (var (uyeId, kayit) in uyeler)
        {
            BorcSatirYaz(bag, tx, dernekId, uyeId, yil, 1, yillik);
            say++;
            var yeni = kayit.StartsWith(yilOn, StringComparison.Ordinal);
            if (giris > 0 && yeni)
            {
                BorcSatirYaz(bag, tx, dernekId, uyeId, yil, 0, giris);
                say++;
            }
            else
            {
                using var sil = bag.CreateCommand();
                sil.Transaction = tx;
                sil.CommandText = "DELETE FROM AidatBorc WHERE DernekId=$d AND UyeId=$u AND Yil=$y AND Ay=0";
                sil.Parameters.AddWithValue("$d", dernekId);
                sil.Parameters.AddWithValue("$u", uyeId);
                sil.Parameters.AddWithValue("$y", yil);
                sil.ExecuteNonQuery();
            }
        }
        tx.Commit();
        OnbellekDusur(dernekId);
        return say;
    }

    static void BorcSatirYaz(SqliteConnection bag, SqliteTransaction? tx, int dernekId, int uyeId, int yil, int ay, decimal tutar)
    {
        using var k = bag.CreateCommand();
        if (tx is not null) k.Transaction = tx;
        k.CommandText = """
            INSERT INTO AidatBorc(DernekId, UyeId, Yil, Ay, Tutar) VALUES($d,$u,$y,$a,$t)
            ON CONFLICT(UyeId, Yil, Ay) DO UPDATE SET Tutar=$t, DernekId=$d
            """;
        k.Parameters.AddWithValue("$d", dernekId);
        k.Parameters.AddWithValue("$u", uyeId);
        k.Parameters.AddWithValue("$y", yil);
        k.Parameters.AddWithValue("$a", ay);
        k.Parameters.AddWithValue("$t", tutar);
        k.ExecuteNonQuery();
    }

    public List<AyBorcKayit> UyeAyBorc(int dernekId, int uyeId, int yil)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = "SELECT Ay, Tutar FROM AidatBorc WHERE DernekId=$d AND UyeId=$u AND Yil=$y ORDER BY Ay";
        k.Parameters.AddWithValue("$d", dernekId);
        k.Parameters.AddWithValue("$u", uyeId);
        k.Parameters.AddWithValue("$y", yil);
        return OkuListe(k, r => new AyBorcKayit { Ay = r.GetInt32(0), Tutar = Convert.ToDecimal(r.GetDouble(1)) });
    }

    public int OdemeSay(int dernekId, int yil = 0)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        if (yil > 0)
        {
            k.CommandText = "SELECT COUNT(*) FROM Odeme WHERE DernekId=$d AND Tarih>=$a AND Tarih<$z";
            k.Parameters.AddWithValue("$a", yil + "-01-01");
            k.Parameters.AddWithValue("$z", (yil + 1) + "-01-01");
        }
        else
            k.CommandText = "SELECT COUNT(*) FROM Odeme WHERE DernekId=$d";
        k.Parameters.AddWithValue("$d", dernekId);
        return Convert.ToInt32(k.ExecuteScalar());
    }

    public List<OdemeKayit> OdemeListe(int dernekId, int yil = 0, int atla = 0, int al = 0)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        if (yil > 0)
        {
            k.CommandText = """
                SELECT o.Id, o.UyeId, u.AdSoyad, o.Tarih, o.Tutar, o.Aciklama, o.MakbuzNo
                FROM Odeme o JOIN Uye u ON u.Id=o.UyeId AND u.DernekId=o.DernekId
                WHERE o.DernekId=$d AND o.Tarih>=$a AND o.Tarih<$z
                ORDER BY o.Tarih DESC, o.Id DESC
                """;
            k.Parameters.AddWithValue("$a", yil + "-01-01");
            k.Parameters.AddWithValue("$z", (yil + 1) + "-01-01");
        }
        else
        {
            k.CommandText = """
                SELECT o.Id, o.UyeId, u.AdSoyad, o.Tarih, o.Tutar, o.Aciklama, o.MakbuzNo
                FROM Odeme o JOIN Uye u ON u.Id=o.UyeId AND u.DernekId=o.DernekId
                WHERE o.DernekId=$d ORDER BY o.Id DESC
                """;
        }
        k.Parameters.AddWithValue("$d", dernekId);
        if (al > 0) LimitEkle(k, atla, al);
        else if (yil == 0) k.CommandText += " LIMIT 200";
        return OkuListe(k, OkuOdeme);
    }

    public List<OdemeKayit> UyeOdemeListe(int dernekId, int uyeId, int yil)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = """
            SELECT o.Id, o.UyeId, u.AdSoyad, o.Tarih, o.Tutar, o.Aciklama, o.MakbuzNo
            FROM Odeme o JOIN Uye u ON u.Id=o.UyeId AND u.DernekId=o.DernekId
            WHERE o.DernekId=$d AND o.UyeId=$u AND o.Tarih>=$a AND o.Tarih<$z
            ORDER BY o.Tarih, o.Id
            """;
        k.Parameters.AddWithValue("$d", dernekId);
        k.Parameters.AddWithValue("$u", uyeId);
        k.Parameters.AddWithValue("$a", yil + "-01-01");
        k.Parameters.AddWithValue("$z", (yil + 1) + "-01-01");
        return OkuListe(k, OkuOdeme);
    }

    public OdemeKayit OdemeEkle(int dernekId, int uyeId, DateTime tarih, decimal tutar, string aciklama)
    {
        if (tutar <= 0) throw new InvalidOperationException("Tutar sıfırdan büyük olmalı.");
        UyeSahip(dernekId, uyeId);
        using var bag = Ac();
        using var tx = bag.BeginTransaction();
        var no = MakbuzNoUret(bag, tx, dernekId, tarih.Year);
        var id = Ekle(bag, tx, """
            INSERT INTO Odeme(DernekId, UyeId, Tarih, Tutar, Aciklama, MakbuzNo)
            VALUES($d,$u,$t,$p,$a,$m)
            """,
            ("$d", dernekId), ("$u", uyeId), ("$t", tarih.ToString("yyyy-MM-dd")),
            ("$p", tutar), ("$a", aciklama.Trim()), ("$m", no));
        Ekle(bag, tx, """
            INSERT INTO Kasa(DernekId, Tarih, Tur, Tutar, Aciklama, OdemeId)
            VALUES($d,$t,'Gelir',$p,$a,$o)
            """,
            ("$d", dernekId), ("$t", tarih.ToString("yyyy-MM-dd")),
            ("$p", tutar), ("$a", "Aidat tahsilatı " + no), ("$o", id));
        tx.Commit();
        OnbellekDusur(dernekId);
        return new OdemeKayit { Id = id, UyeId = uyeId, Tarih = tarih.ToString("yyyy-MM-dd"), Tutar = tutar, Aciklama = aciklama.Trim(), MakbuzNo = no };
    }

    public MakbuzKayit? MakbuzGetir(int dernekId, int odemeId)
    {
        var d = DernekGetir(dernekId);
        if (d is null) return null;
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = """
            SELECT o.Id, o.UyeId, u.AdSoyad, o.Tarih, o.Tutar, o.Aciklama, o.MakbuzNo, u.Tc
            FROM Odeme o JOIN Uye u ON u.Id=o.UyeId AND u.DernekId=o.DernekId
            WHERE o.Id=$i AND o.DernekId=$d
            """;
        k.Parameters.AddWithValue("$i", odemeId);
        k.Parameters.AddWithValue("$d", dernekId);
        return OkuTek(k, r => OkuMakbuz(r, d));
    }

    public List<MakbuzKayit> MakbuzListe(int dernekId, IReadOnlyList<int> odemeIdler)
    {
        var ids = odemeIdler.Where(i => i > 0).Distinct().Take(200).ToList();
        if (ids.Count == 0) return [];
        var d = DernekGetir(dernekId);
        if (d is null) return [];
        using var bag = Ac();
        using var k = bag.CreateCommand();
        var yer = new string[ids.Count];
        for (var i = 0; i < ids.Count; i++)
        {
            yer[i] = "$i" + i;
            k.Parameters.AddWithValue(yer[i], ids[i]);
        }
        k.CommandText = $"""
            SELECT o.Id, o.UyeId, u.AdSoyad, o.Tarih, o.Tutar, o.Aciklama, o.MakbuzNo, u.Tc
            FROM Odeme o JOIN Uye u ON u.Id=o.UyeId AND u.DernekId=o.DernekId
            WHERE o.DernekId=$d AND o.Id IN ({string.Join(",", yer)})
            """;
        k.Parameters.AddWithValue("$d", dernekId);
        var map = OkuListe(k, r => OkuMakbuz(r, d)).ToDictionary(m => m.Odeme.Id);
        var liste = new List<MakbuzKayit>(ids.Count);
        foreach (var id in ids)
            if (map.TryGetValue(id, out var m)) liste.Add(m);
        return liste;
    }

    public (decimal Gelir, decimal Gider, int Say) KasaOzet(int dernekId, int yil)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = """
            SELECT Tur, COUNT(*), IFNULL(SUM(Tutar), 0)
            FROM Kasa WHERE DernekId=$d AND Tarih>=$a AND Tarih<$z
            GROUP BY Tur
            """;
        k.Parameters.AddWithValue("$d", dernekId);
        k.Parameters.AddWithValue("$a", yil + "-01-01");
        k.Parameters.AddWithValue("$z", (yil + 1) + "-01-01");
        decimal gelir = 0, gider = 0;
        var say = 0;
        using var r = k.ExecuteReader();
        while (r.Read())
        {
            var n = r.GetInt32(1);
            var t = Convert.ToDecimal(r.GetDouble(2));
            say += n;
            if (r.GetString(0) == "Gelir") gelir = t;
            else gider = t;
        }
        return (gelir, gider, say);
    }

    public List<KasaKayit> KasaListe(int dernekId, int yil, int atla = 0, int al = 0)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = """
            SELECT Id, Tarih, Tur, Tutar, Aciklama, IFNULL(Makbuz,'') FROM Kasa
            WHERE DernekId=$d AND Tarih>=$a AND Tarih<$z
            ORDER BY Tarih DESC, Id DESC
            """;
        k.Parameters.AddWithValue("$d", dernekId);
        k.Parameters.AddWithValue("$a", yil + "-01-01");
        k.Parameters.AddWithValue("$z", (yil + 1) + "-01-01");
        LimitEkle(k, atla, al);
        return OkuListe(k, r => new KasaKayit
        {
            Id = r.GetInt32(0),
            Tarih = r.GetString(1),
            Tur = r.GetString(2),
            Tutar = Convert.ToDecimal(r.GetDouble(3)),
            Aciklama = r.GetString(4),
            Makbuz = r.GetString(5)
        });
    }

    public void KasaEkle(int dernekId, DateTime tarih, string tur, decimal tutar, string aciklama, string makbuz = "", int uyeId = 0)
    {
        if (tur is not ("Gelir" or "Gider")) throw new InvalidOperationException("Tür gelir veya gider olmalı.");
        if (tutar <= 0) throw new InvalidOperationException("Tutar sıfırdan büyük olmalı.");
        aciklama = aciklama.Trim();
        makbuz = (makbuz ?? "").Trim();
        if (aciklama.Length < 2) throw new InvalidOperationException("Açıklama gerekli.");
        if (uyeId > 0) UyeSahip(dernekId, uyeId);
        using var bag = Ac();
        using var tx = bag.BeginTransaction();
        int? odemeId = null;
        if (uyeId > 0 && tur == "Gelir")
        {
            if (makbuz.Length == 0) makbuz = MakbuzNoUret(bag, tx, dernekId, tarih.Year);
            odemeId = Ekle(bag, tx, """
                INSERT INTO Odeme(DernekId, UyeId, Tarih, Tutar, Aciklama, MakbuzNo)
                VALUES($d,$u,$t,$p,$a,$m)
                """,
                ("$d", dernekId), ("$u", uyeId), ("$t", tarih.ToString("yyyy-MM-dd")),
                ("$p", tutar), ("$a", aciklama), ("$m", makbuz));
        }
        using var k = bag.CreateCommand();
        k.Transaction = tx;
        k.CommandText = "INSERT INTO Kasa(DernekId, Tarih, Tur, Tutar, Aciklama, Makbuz, OdemeId) VALUES($d,$t,$u,$p,$a,$m,$o)";
        k.Parameters.AddWithValue("$d", dernekId);
        k.Parameters.AddWithValue("$t", tarih.ToString("yyyy-MM-dd"));
        k.Parameters.AddWithValue("$u", tur);
        k.Parameters.AddWithValue("$p", tutar);
        k.Parameters.AddWithValue("$a", aciklama);
        k.Parameters.AddWithValue("$m", makbuz);
        k.Parameters.AddWithValue("$o", (object?)odemeId ?? DBNull.Value);
        k.ExecuteNonQuery();
        tx.Commit();
        OnbellekDusur(dernekId);
    }

    public void KasaSil(int dernekId, int id)
    {
        using var bag = Ac();
        using var oku = bag.CreateCommand();
        oku.CommandText = "SELECT OdemeId, Aciklama FROM Kasa WHERE Id=$i AND DernekId=$d";
        oku.Parameters.AddWithValue("$i", id);
        oku.Parameters.AddWithValue("$d", dernekId);
        using var r = oku.ExecuteReader();
        if (!r.Read())
            throw new InvalidOperationException("Kasa satırı bulunamadı.");
        var odemeId = r.IsDBNull(0) ? 0 : r.GetInt32(0);
        var aciklama = r.GetString(1);
        r.Close();
        if (odemeId > 0 && aciklama.StartsWith("Aidat tahsilatı", StringComparison.Ordinal))
            throw new InvalidOperationException("Aidat tahsilatından oluşan kasa satırı silinmez; ödemeden gider.");
        using var tx = bag.BeginTransaction();
        if (odemeId > 0)
        {
            using var silOdeme = bag.CreateCommand();
            silOdeme.Transaction = tx;
            silOdeme.CommandText = "DELETE FROM Odeme WHERE Id=$o AND DernekId=$d";
            silOdeme.Parameters.AddWithValue("$o", odemeId);
            silOdeme.Parameters.AddWithValue("$d", dernekId);
            silOdeme.ExecuteNonQuery();
        }
        using var k = bag.CreateCommand();
        k.Transaction = tx;
        k.CommandText = "DELETE FROM Kasa WHERE Id=$i AND DernekId=$d";
        k.Parameters.AddWithValue("$i", id);
        k.Parameters.AddWithValue("$d", dernekId);
        k.ExecuteNonQuery();
        tx.Commit();
        OnbellekDusur(dernekId);
    }

    public int ToplantiSay(int dernekId)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = "SELECT COUNT(*) FROM Toplanti WHERE DernekId=$d";
        k.Parameters.AddWithValue("$d", dernekId);
        return Convert.ToInt32(k.ExecuteScalar());
    }

    public ToplantiKayit? ToplantiGetir(int dernekId, int id)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = "SELECT Id, Tarih, Baslik, Notlar FROM Toplanti WHERE DernekId=$d AND Id=$i";
        k.Parameters.AddWithValue("$d", dernekId);
        k.Parameters.AddWithValue("$i", id);
        return OkuTek(k, OkuToplanti);
    }

    public List<ToplantiKayit> ToplantiListe(int dernekId, int atla = 0, int al = 0)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = "SELECT Id, Tarih, Baslik, Notlar FROM Toplanti WHERE DernekId=$d ORDER BY Tarih DESC, Id DESC";
        k.Parameters.AddWithValue("$d", dernekId);
        LimitEkle(k, atla, al);
        return OkuListe(k, OkuToplanti);
    }

    public void ToplantiKaydet(int dernekId, int id, DateTime tarih, string baslik, string notlar)
    {
        baslik = baslik.Trim();
        if (baslik.Length < 2) throw new InvalidOperationException("Başlık gerekli.");
        using var bag = Ac();
        using var k = bag.CreateCommand();
        if (id == 0)
        {
            k.CommandText = "INSERT INTO Toplanti(DernekId, Tarih, Baslik, Notlar) VALUES($d,$t,$b,$n)";
        }
        else
        {
            k.CommandText = "UPDATE Toplanti SET Tarih=$t, Baslik=$b, Notlar=$n WHERE Id=$i AND DernekId=$d";
            k.Parameters.AddWithValue("$i", id);
        }
        k.Parameters.AddWithValue("$d", dernekId);
        k.Parameters.AddWithValue("$t", tarih.ToString("yyyy-MM-dd"));
        k.Parameters.AddWithValue("$b", baslik);
        k.Parameters.AddWithValue("$n", notlar.Trim());
        k.ExecuteNonQuery();
        OnbellekDusur(dernekId);
    }

    public void ToplantiSil(int dernekId, int id)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = "DELETE FROM Toplanti WHERE Id=$i AND DernekId=$d";
        k.Parameters.AddWithValue("$i", id);
        k.Parameters.AddWithValue("$d", dernekId);
        k.ExecuteNonQuery();
        OnbellekDusur(dernekId);
    }

    public int EvrakSay(int dernekId)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = "SELECT COUNT(*) FROM Evrak WHERE DernekId=$d";
        k.Parameters.AddWithValue("$d", dernekId);
        return Convert.ToInt32(k.ExecuteScalar());
    }

    public List<EvrakKayit> EvrakListe(int dernekId, int atla = 0, int al = 0)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = "SELECT Id, Baslik, DosyaAd, Kayit FROM Evrak WHERE DernekId=$d ORDER BY Id DESC";
        k.Parameters.AddWithValue("$d", dernekId);
        LimitEkle(k, atla, al);
        return OkuListe(k, r => new EvrakKayit
        {
            Id = r.GetInt32(0),
            Baslik = r.GetString(1),
            DosyaAd = r.GetString(2),
            Kayit = r.GetString(3)
        });
    }

    public EvrakKayit EvrakEkle(int dernekId, string baslik, string orijinalAd, Stream icerik)
    {
        baslik = baslik.Trim();
        if (baslik.Length < 2) throw new InvalidOperationException("Evrak başlığı gerekli.");
        var uz = Path.GetExtension(orijinalAd).ToLowerInvariant();
        if (uz is not (".pdf" or ".jpg" or ".jpeg" or ".png" or ".doc" or ".docx" or ".xls" or ".xlsx"))
            throw new InvalidOperationException("İzin verilen türler: PDF, resim, Word, Excel.");
        using var bag = Ac();
        using var tx = bag.BeginTransaction();
        var id = Ekle(bag, tx, "INSERT INTO Evrak(DernekId, Baslik, DosyaAd, Kayit) VALUES($d,$b,$f,$k)",
            ("$d", dernekId), ("$b", baslik), ("$f", "bekliyor"), ("$k", Simdi()));
        var ad = id + uz;
        var yol = Path.Combine(EvrakKlasor(dernekId), ad);
        using (var fs = File.Create(yol))
            icerik.CopyTo(fs);
        using (var k = bag.CreateCommand())
        {
            k.Transaction = tx;
            k.CommandText = "UPDATE Evrak SET DosyaAd=$f WHERE Id=$i";
            k.Parameters.AddWithValue("$f", ad);
            k.Parameters.AddWithValue("$i", id);
            k.ExecuteNonQuery();
        }
        tx.Commit();
        OnbellekDusur(dernekId);
        return new EvrakKayit { Id = id, Baslik = baslik, DosyaAd = ad, Kayit = Simdi() };
    }

    public string? EvrakYol(int dernekId, int id)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = "SELECT DosyaAd FROM Evrak WHERE Id=$i AND DernekId=$d";
        k.Parameters.AddWithValue("$i", id);
        k.Parameters.AddWithValue("$d", dernekId);
        var ad = k.ExecuteScalar() as string;
        if (string.IsNullOrEmpty(ad)) return null;
        var yol = Path.Combine(EvrakKlasor(dernekId), ad);
        return File.Exists(yol) ? yol : null;
    }

    public void EvrakSil(int dernekId, int id)
    {
        var yol = EvrakYol(dernekId, id);
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = "DELETE FROM Evrak WHERE Id=$i AND DernekId=$d";
        k.Parameters.AddWithValue("$i", id);
        k.Parameters.AddWithValue("$d", dernekId);
        k.ExecuteNonQuery();
        OnbellekDusur(dernekId);
        if (yol is not null)
        {
            try { File.Delete(yol); } catch { /* yedek */ }
        }
    }

    public List<int> KayitYillari(int dernekId)
    {
        lock (_onbellek)
        {
            if (_yilOnbellek.TryGetValue(dernekId, out var hazir)) return hazir;
        }
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = """
            SELECT DISTINCT Yil FROM AidatTanim WHERE DernekId=$d
            UNION
            SELECT DISTINCT Yil FROM AidatBorc WHERE DernekId=$d
            UNION
            SELECT DISTINCT CAST(substr(Tarih, 1, 4) AS INTEGER) FROM Odeme WHERE DernekId=$d
            UNION
            SELECT DISTINCT CAST(substr(Tarih, 1, 4) AS INTEGER) FROM Kasa WHERE DernekId=$d
            """;
        k.Parameters.AddWithValue("$d", dernekId);
        var set = new HashSet<int>();
        using (var r = k.ExecuteReader())
        {
            while (r.Read())
            {
                var y = r.GetInt32(0);
                if (YilGecerli(y)) set.Add(y);
            }
        }
        set.Add(DateTime.Now.Year);
        var liste = set.OrderByDescending(x => x).ToList();
        lock (_onbellek) { _yilOnbellek[dernekId] = liste; }
        return liste;
    }

    static bool YilGecerli(int yil) => yil is >= 2000 and <= 2100 && yil is not (2014 or 2019 or 2020 or 2021);

    public PanelOzet Ozet(int dernekId, int yil)
    {
        if (yil < 2000 || yil > 2100) yil = DateTime.Now.Year;
        var a = yil + "-01-01";
        var z = (yil + 1) + "-01-01";
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = """
            SELECT
              (SELECT COUNT(*) FROM Uye WHERE DernekId=$d AND Durum='Aktif'),
              (SELECT IFNULL(SUM(Tutar),0) FROM AidatBorc WHERE DernekId=$d AND Yil=$y),
              (SELECT IFNULL(SUM(Tutar),0) FROM Odeme WHERE DernekId=$d AND Tarih>=$a AND Tarih<$z),
              (SELECT IFNULL(SUM(Tutar),0) FROM Kasa WHERE DernekId=$d AND Tur='Gelir' AND Tarih>=$a AND Tarih<$z),
              (SELECT IFNULL(SUM(Tutar),0) FROM Kasa WHERE DernekId=$d AND Tur='Gider' AND Tarih>=$a AND Tarih<$z),
              (SELECT COUNT(*) FROM Toplanti WHERE DernekId=$d AND Tarih>=$a AND Tarih<$z),
              (SELECT COUNT(*) FROM Evrak WHERE DernekId=$d AND Kayit>=$a AND Kayit<$z)
            """;
        k.Parameters.AddWithValue("$d", dernekId);
        k.Parameters.AddWithValue("$y", yil);
        k.Parameters.AddWithValue("$a", a);
        k.Parameters.AddWithValue("$z", z);
        using var r = k.ExecuteReader();
        r.Read();
        return new PanelOzet
        {
            Yil = yil,
            UyeSayisi = r.GetInt32(0),
            ToplamBorc = Convert.ToDecimal(r.GetDouble(1)),
            ToplamOdenen = Convert.ToDecimal(r.GetDouble(2)),
            KasaGelir = Convert.ToDecimal(r.GetDouble(3)),
            KasaGider = Convert.ToDecimal(r.GetDouble(4)),
            ToplantiSayisi = r.GetInt32(5),
            EvrakSayisi = r.GetInt32(6)
        };
    }

    public List<YilRapor> AidatYilRapor(int dernekId)
    {
        var yillar = KayitYillari(dernekId);
        var borc = new Dictionary<int, decimal>();
        var odenen = new Dictionary<int, decimal>();
        using var bag = Ac();
        using (var k = bag.CreateCommand())
        {
            k.CommandText = "SELECT Yil, IFNULL(SUM(Tutar),0) FROM AidatBorc WHERE DernekId=$d GROUP BY Yil";
            k.Parameters.AddWithValue("$d", dernekId);
            using var r = k.ExecuteReader();
            while (r.Read())
            {
                var y = r.GetInt32(0);
                if (YilGecerli(y)) borc[y] = Convert.ToDecimal(r.GetDouble(1));
            }
        }
        using (var k = bag.CreateCommand())
        {
            k.CommandText = """
                SELECT CAST(substr(Tarih, 1, 4) AS INTEGER), IFNULL(SUM(Tutar),0)
                FROM Odeme WHERE DernekId=$d
                GROUP BY CAST(substr(Tarih, 1, 4) AS INTEGER)
                """;
            k.Parameters.AddWithValue("$d", dernekId);
            using var r = k.ExecuteReader();
            while (r.Read())
            {
                var y = r.GetInt32(0);
                if (YilGecerli(y)) odenen[y] = Convert.ToDecimal(r.GetDouble(1));
            }
        }
        decimal oncekiToplam = 0;
        var sirali = new List<YilRapor>();
        foreach (var y in yillar.OrderBy(x => x))
        {
            borc.TryGetValue(y, out var toplanacak);
            odenen.TryGetValue(y, out var toplanan);
            var devreden = oncekiToplam;
            var toplam = toplanan + devreden;
            oncekiToplam = toplam;
            sirali.Add(new YilRapor
            {
                Yil = y,
                Toplanacak = toplanacak,
                Toplanan = toplanan,
                Devreden = devreden,
                Toplam = toplam
            });
        }
        sirali.Reverse();
        return sirali;
    }

    public static string Tl(decimal n) => ParaKutu(n) + " ₺";
    public static string TlMutlak(decimal n) => Tl(Math.Abs(n));
    public static string ParaKutu(decimal n) => n.ToString("N2");

    public static bool ParaOku(string? s, out decimal n)
    {
        n = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var ham = s.Trim().Replace(" ", "", StringComparison.Ordinal);
        var sonNokta = ham.LastIndexOf('.');
        var sonVirgul = ham.LastIndexOf(',');
        if (sonVirgul > sonNokta)
            ham = ham.Replace(".", "", StringComparison.Ordinal).Replace(',', '.');
        else if (sonNokta >= 0 && sonVirgul < 0 && ham.Length - sonNokta - 1 == 3)
            ham = ham.Replace(".", "", StringComparison.Ordinal);
        else if (sonVirgul >= 0 && sonNokta < 0)
            ham = ham.Replace(',', '.');
        return decimal.TryParse(ham, NumberStyles.Number, CultureInfo.InvariantCulture, out n);
    }

    void GrupKontrol(int dernekId, int grupId)
    {
        if (!GrupListe(dernekId).Any(g => g.Id == grupId))
            throw new InvalidOperationException("Grup bu derneğe ait değil.");
    }

    void UyeSahip(int dernekId, int uyeId)
    {
        using var bag = Ac();
        using var k = bag.CreateCommand();
        k.CommandText = "SELECT COUNT(*) FROM Uye WHERE Id=$i AND DernekId=$d";
        k.Parameters.AddWithValue("$i", uyeId);
        k.Parameters.AddWithValue("$d", dernekId);
        if (Convert.ToInt32(k.ExecuteScalar()) == 0)
            throw new InvalidOperationException("Üye bulunamadı.");
    }

    static string MakbuzNoUret(SqliteConnection bag, SqliteTransaction tx, int dernekId, int yil)
    {
        using var k = bag.CreateCommand();
        k.Transaction = tx;
        k.CommandText = "SELECT COUNT(*) FROM Odeme WHERE DernekId=$d AND Tarih>=$a AND Tarih<$z";
        k.Parameters.AddWithValue("$d", dernekId);
        k.Parameters.AddWithValue("$a", yil + "-01-01");
        k.Parameters.AddWithValue("$z", (yil + 1) + "-01-01");
        var sira = Convert.ToInt32(k.ExecuteScalar()) + 1;
        return $"DT-{dernekId}-{yil}-{sira:0000}";
    }

    static int Ekle(SqliteConnection bag, SqliteTransaction tx, string sql, params (string, object)[] p)
    {
        using var k = bag.CreateCommand();
        k.Transaction = tx;
        k.CommandText = sql;
        foreach (var (n, v) in p) k.Parameters.AddWithValue(n, v);
        k.ExecuteNonQuery();
        using var id = bag.CreateCommand();
        id.Transaction = tx;
        id.CommandText = "SELECT last_insert_rowid()";
        return Convert.ToInt32(id.ExecuteScalar());
    }

    static int Say(SqliteConnection bag, string sql, int dernekId)
    {
        using var k = bag.CreateCommand();
        k.CommandText = sql;
        k.Parameters.AddWithValue("$d", dernekId);
        return Convert.ToInt32(k.ExecuteScalar());
    }

    static decimal Para(SqliteConnection bag, string sql, int dernekId)
    {
        using var k = bag.CreateCommand();
        k.CommandText = sql;
        k.Parameters.AddWithValue("$d", dernekId);
        return Convert.ToDecimal(k.ExecuteScalar());
    }

    static int SayEk(SqliteConnection bag, string sql, params (string, object)[] p)
    {
        using var k = bag.CreateCommand();
        k.CommandText = sql;
        foreach (var (n, v) in p) k.Parameters.AddWithValue(n, v);
        return Convert.ToInt32(k.ExecuteScalar());
    }

    static decimal ParaEk(SqliteConnection bag, string sql, params (string, object)[] p)
    {
        using var k = bag.CreateCommand();
        k.CommandText = sql;
        foreach (var (n, v) in p) k.Parameters.AddWithValue(n, v);
        return Convert.ToDecimal(k.ExecuteScalar());
    }

    static KullaniciKayit OkuKullanici(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        DernekId = r.IsDBNull(1) ? null : r.GetInt32(1),
        Ad = r.GetString(2),
        KullaniciAdi = r.GetString(3),
        Rol = r.GetString(5)
    };

    static DernekKayit OkuDernek(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        Ad = r.GetString(1),
        Telefon = r.GetString(2),
        Adres = r.GetString(3)
    };

    static void LimitEkle(SqliteCommand k, int atla, int al)
    {
        if (al <= 0) return;
        k.CommandText += " LIMIT $al OFFSET $at";
        k.Parameters.AddWithValue("$al", al);
        k.Parameters.AddWithValue("$at", Math.Max(0, atla));
    }

    static KullaniciKayit OkuKullaniciSatir(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        DernekId = r.IsDBNull(1) ? null : r.GetInt32(1),
        Ad = r.GetString(2),
        KullaniciAdi = r.GetString(3),
        Rol = r.GetString(4),
        Aktif = r.GetInt32(5) == 1
    };

    static ToplantiKayit OkuToplanti(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        Tarih = r.GetString(1),
        Baslik = r.GetString(2),
        Notlar = r.GetString(3)
    };

    static UyeKayit OkuUye(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        GrupId = r.GetInt32(1),
        GrupAd = r.GetString(2),
        AdSoyad = r.GetString(3),
        Tc = r.GetString(4),
        Telefon = r.GetString(5),
        Eposta = r.GetString(6),
        Adres = r.GetString(7),
        Durum = r.GetString(8),
        GirisTarih = r.IsDBNull(9) ? "" : r.GetString(9),
        AyrilisTarih = r.IsDBNull(10) ? "" : r.GetString(10),
        Borc = Convert.ToDecimal(r.GetDouble(11)),
        Odenen = Convert.ToDecimal(r.GetDouble(12))
    };

    static string TarihGun(string? s)
    {
        s = (s ?? "").Trim();
        if (s.Length >= 10 && DateTime.TryParse(s[..10], CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
            return t.ToString("yyyy-MM-dd");
        if (DateTime.TryParse(s, Tr, DateTimeStyles.None, out t))
            return t.ToString("yyyy-MM-dd");
        return "";
    }

    static MakbuzKayit OkuMakbuz(SqliteDataReader r, DernekKayit d) => new()
    {
        Odeme = OkuOdeme(r),
        UyeTc = r.GetString(7),
        DernekAd = d.Ad,
        DernekAdres = d.Adres,
        DernekTelefon = d.Telefon
    };

    static OdemeKayit OkuOdeme(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        UyeId = r.GetInt32(1),
        UyeAd = r.GetString(2),
        Tarih = r.GetString(3),
        Tutar = Convert.ToDecimal(r.GetDouble(4)),
        Aciklama = r.GetString(5),
        MakbuzNo = r.GetString(6)
    };

    static List<T> OkuListe<T>(SqliteCommand k, Func<SqliteDataReader, T> oku)
    {
        var liste = new List<T>();
        using var r = k.ExecuteReader();
        while (r.Read()) liste.Add(oku(r));
        return liste;
    }

    static T? OkuTek<T>(SqliteCommand k, Func<SqliteDataReader, T> oku) where T : class
    {
        using var r = k.ExecuteReader();
        return r.Read() ? oku(r) : null;
    }

    static string Simdi() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}
