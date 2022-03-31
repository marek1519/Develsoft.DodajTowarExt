using Develsoft.DodajTowarExt;
using Soneta.Business;
using Soneta.Business.App;
using Soneta.Business.Db;
using Soneta.CRM;
using Soneta.Towary;
using System;
using System.IO;

[assembly: Worker(typeof(DodajTowarWorker), typeof(Soneta.Towary.Towar))]

namespace Develsoft.DodajTowarExt
{
    public class DodajTowarWorker
    {
        private string dbName = "";
        private string dbUser = "";
        private string dbPassword = "";

        Log log = new Log("Synchronizacja");
        private Login login;
        private Database db;
        private Towar towar;

        [Context]
        public Towar Towar { get => towar; set => towar = value; }



        [Action("Dodaj towar",
            Description = "Dodanie karty towaru do drugiej bazy",
            Mode = ActionMode.Progress | ActionMode.SingleSession,
            Target = ActionTarget.LocalMenu | ActionTarget.Menu | ActionTarget.ToolbarWithText,
            Icon = ActionIcon.List
            )]
        public void DodajTowar()
        {
            db = BusApplication.Instance[dbName];
            login = db.Login(false, dbUser, dbPassword);

            if (login != null)
            {
                log.WriteLine("Zalogowano {0}, do bazy: {1}", new object[2] { dbUser, dbPassword});

                using (Session Session = login.CreateSession(false, false))
                {
                    TowaryModule twm = TowaryModule.GetInstance(Session);
                    BusinessModule bm = BusinessModule.GetInstance(Session);
                    CRMModule crm = CRMModule.GetInstance(Session);

                    using (ITransaction tr = Session.Logout(true))
                    {
                        Towar tNowy = twm.Towary.WgKodu[Towar.Kod];
                        if (tNowy == null)
                        {
                            tNowy = new Towar();
                            twm.Towary.AddRow(tNowy);
                            tNowy.Kod = Towar.Kod;
                        }

                        #region Zakladka - Ogolne
                        log.WriteLine("\t\tZakładka - Ogolne");
                        //tNowy.Guid = towar.Guid;
                        //tNowy.Kod = towar.Kod;
                        tNowy.Typ = towar.Typ;
                        tNowy.Nazwa = towar.Nazwa;
                        tNowy.EAN = towar.EAN;
                        tNowy.NumerKatalogowy = towar.NumerKatalogowy;
                        tNowy.Typ = towar.Typ;
                        //tNowy.Jednostka = towar.Jednostka;
                        tNowy.PKWiU = towar.PKWiU;
                        tNowy.Narzut = towar.Narzut;
                        tNowy.Marza = towar.Marza;
                        tNowy.CenaZakupuKartotekowa = towar.CenaZakupuKartotekowa;
                        tNowy.PodTypKoduDlaEAN = towar.PodTypKoduDlaEAN;
                        tNowy.DefinicjaStawki = towar.DefinicjaStawki;
                        tNowy.DefinicjaStawkiZakupu = towar.DefinicjaStawkiZakupu;

                        #endregion

                        #region Zakladka - Dodatkowe
                        log.WriteLine("\t\tZakładka - Dodatkowe");
                        tNowy.DoWyprzedazy = towar.DoWyprzedazy;
                        tNowy.Blokada = towar.Blokada;
                        tNowy.Precyzja = towar.Precyzja;
                        tNowy.AktualizujCeny = towar.AktualizujCeny;
                        tNowy.VATOdMarzy = towar.VATOdMarzy;
                        tNowy.NabywcaPodatnik = towar.NabywcaPodatnik;
                        if (towar.CN != null)
                            tNowy.CN = towar.CN;
                        tNowy.NazwaIntrastat = towar.NazwaIntrastat;
                        tNowy.KrajPochodzenia = towar.KrajPochodzenia;
                        if (towar.CN != null)
                            tNowy.CN.WymagaMasyNetto = towar.CN.WymagaMasyNetto;
                        tNowy.EdycjaNazwy = towar.EdycjaNazwy;
                        tNowy.Opis = towar.Opis;

                        #endregion

                        #region Zakladka - Jednostki i opakowania

                        log.WriteLine("\t\tZakładka - Jednostki i opakowania");
                        tNowy.MasaNetto = towar.MasaNetto;
                        tNowy.MasaBrutto = towar.MasaBrutto;
                        tNowy.JednostkaAlternatywna = towar.JednostkaAlternatywna;
                        tNowy.JednostkaReszty = towar.JednostkaReszty;
                        if (towar.CN != null)
                            tNowy.CN.JednostkaUzupelniajaca = towar.CN.JednostkaUzupelniajaca;

                        #endregion

                        #region Zakladka - Cechy
                        log.WriteLine("\t\tZakładka - Cechy");

                        string[] features = { "H1", "H2", "H3", "Głębokość", "Szerokość",
                                            "Średnica", "Angielski", "NazwaAngielska",
                                            "Francuski", "NazwaFrancuska","Niemiecki",
                                            "NazwaNiemiecka","EnovaNET","Kategoria" };
                        foreach (string feature in features)
                        {
                            try
                            {
                                tNowy.Features[feature] = towar.Features[feature];
                            }
                            catch (Exception exc)
                            {
                                log.WriteLine("\t\tDodawanie karty towaru-cechy: " + exc.Message, exc);
                            }
                        }

                        #endregion

                        #region Załączniki
                        log.WriteLine("\t\tZakładka - Zalaczniki");


                        foreach (Attachment at in towar.Attachments)
                        {
                            if (JestZalacznik(tNowy, at) || at.Type == AttachmentType.Note)
                            {
                                log.WriteLine("\t\t * pominięcie załącznika: " + at.Name);
                                continue;
                            }

                            Attachment atNowy = new Attachment(tNowy, AttachmentType.Attachments);
                            bm.Attachments.AddRow(atNowy);
                            log.WriteLine("\t\t * Zalacznik: " + at.Name);
                            atNowy.Name = at.Name;
                            //atNowy.Icon = at.Icon;

                            atNowy.VisibleInNet = at.VisibleInNet;
                            atNowy.Features["Dokument"] = at.Features["Dokument"];

                            string path = GetPath(at);
                            if (string.IsNullOrEmpty(path))
                            {
                                log.WriteLine("Pominięcie załacznika: " + at);
                                continue;
                            }
                            FileInfo fi = new FileInfo(path);
                            if (fi.Exists)
                            {
                                FileStream fs = fi.OpenRead();
                                atNowy.LoadFromStream(fs);
                            }
                        }

                        #endregion

                        #region Ceny
                        log.WriteLine("\t\tZakładka - Ceny");
                        foreach (Cena ce in towar.Ceny)
                        {
                            tNowy.Ceny[ce.Definicja].Netto = ce.Netto;
                        }

                        #endregion

                        #region Dostawcy

                        log.WriteLine("\t\tZakładka - Dostawcy");
                        foreach (DostawcaTowaru dtw in towar.Dostawcy)
                        {
                            if (JestDostawca(tNowy, dtw.Dostawca))
                            {
                                log.WriteLine("\t\t\t * pominięcie dostawcy: " + dtw.Dostawca);
                                continue;
                            }
                            Kontrahent kN = crm.Kontrahenci.WgKodu[dtw.Dostawca.Kod];
                            if (kN == null)
                            {
                                log.WriteLine("Brak kontrahenta: " + dtw.Dostawca);
                                continue;
                            }

                            DostawcaTowaru dt = new DostawcaTowaru();
                            twm.DostawcyTowaru.AddRow(dt);
                            dt.Towar = tNowy;
                            dt.Dostawca = kN;

                        }

                        #endregion

                        tr.CommitUI();
                    }
                    Session.Save();
                }
            }

            if (login != null)
                login.Dispose();
        }

        /// <summary>
        /// Sprawdzenie czy wybrany dostawca jest już na karcie towaru
        /// </summary>
        /// <param name="towar"></param>
        /// <param name="dostawca"></param>
        /// <returns></returns>
        private bool JestDostawca(Towar towar, Kontrahent dostawca)
        {
            foreach (DostawcaTowaru dt in towar.Dostawcy)
            {
                if (dostawca.Kod == dt.Dostawca.Kod)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Jest element kompletu
        /// </summary>
        /// <param name="towar"></param>
        /// <param name="dostawca"></param>
        /// <returns></returns>
        private ElementKompletu JestElementKompletu(Towar towar, Towar element)
        {
            foreach (ElementKompletu dt in towar.ElementyKompletu)
            {
                if (element.Kod == dt.Towar.Kod)
                    return dt;
            }

            return null;
        }

        /// <summary>
        /// Sprawdzenie czy załącznik już istnieje
        /// </summary>
        /// <param name="towar"></param>
        /// <param name="zalacznik"></param>
        /// <returns></returns>
        private bool JestZalacznik(Towar towar, Attachment zalacznik)
        {
            foreach (Attachment a in towar.Attachments)
            {
                if (a.Name == zalacznik.Name) return true;
            }
            return false;
        }

        public string GetPath(Soneta.Business.Db.Attachment a)
        {
            string dir = System.IO.Path.GetTempPath() + @"Soneta\Oferta\";
            Directory.CreateDirectory(dir);
            string file = dir + a.Name;

            System.IO.FileStream _FileStream =
             new System.IO.FileStream(file, System.IO.FileMode.Create,
                                      System.IO.FileAccess.Write);

            _FileStream.Write(a.RawData, 0, a.RawData.Length);
            _FileStream.Close();

            if (File.Exists(file))
                return file;

            return "";
        }

        public static bool IsVisibleDodajTowar(Towar towar)
        => true;
        

    }
}
