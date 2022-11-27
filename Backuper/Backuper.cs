using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Backuper;
static class Backuper
{
    public static string CurrentFolder = AppDomain.CurrentDomain.BaseDirectory.Replace('\\', '/');
    public static void TitleWrite(string text)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Console.Title = text;
        else
            Console.Write($"\u001B]0;{text}\u0007");
    }
    struct BackupData
    {
        public string date;
        public string Size;
        public string error;
    }
    class stringContainer
    {
        public string s;
    }

    static void Main(string[] args)
    {
        Config config = new Config();

        config.Init();

        stringContainer[] status = new stringContainer[config.Querries.Length];

        for (int i = 0; i < config.Querries.Length; i++)
        {
            Console.WriteLine("Initializing querry: \n" + config.Querries[i].ToString());

            status[i] = new stringContainer();
            HandleQuerry(config.Querries[i], status[i]);
        }

        Thread.Sleep(5000);

        while (true)
        {
            Console.Clear();
            foreach (stringContainer sc in status)
            {
                Console.WriteLine(sc.s);
            }


            Thread.Sleep(2000);
        }

    }

    static void HandleQuerry(BackupQuerry q, stringContainer status)
    {
        Task.Run(() =>
        {

            q.BackupFolder = q.BackupFolder.Replace('\\', '/');
            q.FolderToBackup = q.FolderToBackup.Replace('\\', '/');

            for (int i = 0; i < q.Ignore.Length; i++)
            {
                q.Ignore[i] = q.Ignore[i].Replace('\\', '/');
            }

            int hours = int.Parse(q.date.Split(':')[0]);
            int minutes = int.Parse(q.date.Split(':')[1]);

            Queue<string> Backups = new Queue<string>();

            DateTime t = DateTime.Today.AddHours(hours).AddMinutes(minutes);

            while (DateTime.Now > t)
                switch (q.Occurences)
                {
                    case BackupOccurences.Hourly:

                        t = t.AddHours(q.OccurenceFactor);

                        break;
                    case BackupOccurences.Dayly:

                        t = t.AddDays(q.OccurenceFactor);

                        break;
                    case BackupOccurences.Monthly:

                        t = t.AddDays(q.OccurenceFactor * 30.437);

                        break;
                    default:
                        break;
                }

            BackupData dats = new BackupData();

            dats.date = "N/A";
            dats.error = "";
            dats.Size = "";


            status.s = "Next backup of: " + q.FolderToBackup + " At: " + t + " In: " + (t - DateTime.Now).ToString("hh\\:mm\\:ss") + "\nLast: " + dats.date + " " + dats.Size + " " + dats.error + "              ";


            while (true)
            {
                if ((t - DateTime.Now).TotalSeconds < 30)
                {
                    MakeBackup(q.FolderToBackup, q.BackupFolder, in Backups, q.Ignore, ref dats,q.Compressed);

                    Console.WriteLine("Next backup: " + t);

                    if (Backups.Count > 1 && Backups.Count > q.BackupCountToKeep)
                    {
                        string p = Backups.Dequeue();

                        if (Directory.Exists(p))
                        {
                            Directory.Delete(p, true);
                        }
                        else if (File.Exists(p + ".zip"))
                        {
                            File.Delete(p + ".zip");
                        }

                    }

                    switch (q.Occurences)
                    {
                        case BackupOccurences.Hourly:

                            t = t.AddHours(q.OccurenceFactor);

                            break;
                        case BackupOccurences.Dayly:

                            t = t.AddDays(q.OccurenceFactor);

                            break;
                        case BackupOccurences.Monthly:

                            t = t.AddDays(q.OccurenceFactor * 30.437);

                            break;
                        default:
                            break;
                    }


                }
                else
                {


                    status.s = "Next backup of: " + q.FolderToBackup + " At: " + t + " In: " + (t - DateTime.Now).ToString("hh\\:mm\\:ss") + "\nLast: " + dats.date + " " + dats.Size + " " + dats.error + "              ";
                }

                Thread.Sleep(1000);
            }

        });

    }


    static void MakeBackup(string from, string to, in Queue<string> Backups, string[] ignore, ref BackupData dats, bool Compressed = false)
    {
        if (!from.EndsWith("/"))
            from = from + "/";

        if (!to.EndsWith("/"))
            to = to + "/";

        dats.error = "";

        if (!Directory.Exists(from))
        {
            dats.error = $"{from} does not exist ({DateTime.Now})";
            return;
        }

        if (!Directory.Exists(to))
        {
            Directory.CreateDirectory(to);
        }

        string[] files = Directory.GetFiles(from, "*", SearchOption.AllDirectories);

        Int64 dataLenght = 0;

        List<string> tmp = new List<string>();

        foreach (string ss in files)
        {
            string s = ss.Replace('\\', '/');
            try
            {
                FileInfo fi = new FileInfo(s);

                bool ok = true;

                foreach (string ig in ignore)
                {
                    if (ig.EndsWith("/") && s.Contains(from + ig))
                    {
                        ok = false;
                        break;
                    }
                    else if (!ig.EndsWith("/") && s.EndsWith(ig))
                    {
                        ok = false;
                        break;
                    }

                }

                if (!ok)
                    continue;

                tmp.Add(s);
                if (fi.Exists)
                    dataLenght += fi.Length;

            }
            catch (Exception)
            {

            }
        }

        files = tmp.ToArray();

        dats.Size = (dataLenght / 1048576.0).ToString("F2") + " MiBytes";

        if (!Directory.Exists(to))
        {
            Directory.CreateDirectory(to);
        }

        Thread.Sleep(1000);

        DateTime d = DateTime.Now;

        string date = d.Year + " " + d.Month + " " + d.Day + " " + d.Hour + "H" + d.Minute;
        UInt64 i = 0;

        dats.date = date;

    oupsi:

        if (!Directory.Exists(to + date) && !File.Exists(to + date + ".zip"))
        {
            if (!Compressed)
                Directory.CreateDirectory(to + date);

            Backups.Enqueue(to + date);
        }
        else
        {
            i++;
            date = d.Year + " " + d.Month + " " + d.Day + " " + d.Hour + "H" + d.Minute + "_" + i;
            goto oupsi;
        }

        int last = 0;
        UInt64 errors = 0;

        if (Compressed)
        {
            ZipArchive zip = ZipFile.Open(to + date + ".zip", ZipArchiveMode.Create);

            for (UInt64 i2 = 0; i2 < (UInt64)files.Length; i2++)
            {
                string s = files[i2];

                int a = (DateTime.Now - d).Seconds;

                if (a > last)
                {
                    last = a;
                    try
                    {
                        TitleWrite($"Transfer: {i2}/{files.Length} {i2 / (UInt64)a}/s {((UInt64)files.Length - i2) / (i2 / (UInt64)a)}s");
                    }
                    catch (Exception)
                    {

                    }

                }


                try
                {
                    string fpath = s.Substring(from.Length);
                    string fpath2 = fpath.Substring(0, fpath.Length - Path.GetFileName(s).Length);

                    zip.CreateEntryFromFile(s, fpath);


                }
                catch (Exception)
                {
                    errors++;
                }
            }
         
            zip.Dispose();


            dats.Size += $" (Compressed: {(new FileInfo(to + date + ".zip").Length / 1048576.0).ToString("F2")} MiBytes)";

        }
        else
        {
            for (UInt64 i2 = 0; i2 < (UInt64)files.Length; i2++)
            {
                string s = files[i2];

                int a = (DateTime.Now - d).Seconds;

                if (a > last)
                {
                    last = a;
                    try
                    {
                        TitleWrite($"Transfer: {i2}/{files.Length} {i2 / (UInt64)a}/s {((UInt64)files.Length - i2) / (i2 / (UInt64)a)}s");
                    }
                    catch (Exception)
                    {

                    }

                }


                try
                {
                    string fpath = s.Substring(from.Length);
                    string fpath2 = fpath.Substring(0, fpath.Length - Path.GetFileName(s).Length);


                    if (!Directory.Exists(to + date + "/" + fpath2))
                        Directory.CreateDirectory(to + date + "/" + fpath2);

                    File.Copy(s, to + date + "/" + fpath);

                }
                catch (Exception)
                {
                    errors++;
                }
            }


        }


        dats.error = "errors: " + errors.ToString();
        TitleWrite("Backuper powered by MCSharp.fr");

    }

}
