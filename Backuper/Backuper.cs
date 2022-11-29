using System;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Backuper;
static class Backuper
{
    public static string CurrentFolder = AppDomain.CurrentDomain.BaseDirectory.Replace('\\', '/');

    static SortedList<DateTime, BackupQuerry> BackupQueue = new SortedList<DateTime, BackupQuerry>();
    static object listLock = new object();

    static readonly string[] SizeSuffixes =
               { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
    static string SizeSuffix(Int64 value, int decimalPlaces = 1)
    {
        if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
        if (value < 0) { return "-" + SizeSuffix(-value, decimalPlaces); }
        if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

        // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
        int mag = (int)Math.Log(value, 1024);

        // 1L << (mag * 10) == 2 ^ (10 * mag) 
        // [i.e. the number of bytes in the unit corresponding to mag]
        decimal adjustedSize = (decimal)value / (1L << (mag * 10));

        // make adjustment when the value is large enough that
        // it would round up to 1000 or more
        if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
        {
            mag += 1;
            adjustedSize /= 1024;
        }

        return string.Format("{0:n" + decimalPlaces + "} {1}",
            adjustedSize,
            SizeSuffixes[mag]);
    }

    static void TitleWrite(string text)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Console.Title = text;
        else
            Console.Write($"\u001B]0;{text}\u0007");
    }
    struct BackupData
    {
        public string date;
        public long Size;
        public string error;

        public BackupData()
        {            
            date = "N/A";
            error = "";
            Size = 0;
        }
    }

    static void Main(string[] args)
    {

        string[] status = new string[Config.Querries.Length];
        BackupData[] dats = new BackupData[Config.Querries.Length];
        Queue<string>[] Backups = new Queue<string>[Config.Querries.Length];

        foreach (BackupQuerry querry in Config.Querries)
        {
            Console.WriteLine($"Initializing querry: \n{querry}");

            querry.BackupFolder = querry.BackupFolder.Replace('\\', '/');
            querry.FolderToBackup = querry.FolderToBackup.Replace('\\', '/');

            if (!querry.BackupFolder.EndsWith("/"))
                querry.BackupFolder += '/';

            if (!querry.FolderToBackup.EndsWith("/"))
                querry.FolderToBackup += '/';

            querry.Ignore = querry.Ignore?.Select(x => x.Replace('\\', '/')).ToArray();

            Backups[Array.IndexOf(Config.Querries, querry)] = new Queue<string>();
            dats[Array.IndexOf(Config.Querries, querry)] = new BackupData();

            QueueQuerry(querry);
        }

        Thread.Sleep(5000);

        Task.Run(() =>
        {
            try
            {
                int index;
                while (true)
                {
                    var item = BackupQueue.First();

                    index = Array.IndexOf(Config.Querries, item.Value);

                    while (item.Key > DateTime.Now)
                        Thread.Sleep(5000);

                    MakeBackup(item.Value.FolderToBackup, item.Value.BackupFolder, in Backups[index], item.Value.Ignore, ref dats[index], item.Value.Compressed);

                    if (Backups[index].Count > 1 && Backups[index].Count > item.Value.BackupCountToKeep)
                    {
                        string p = Backups[index].Dequeue();

                        if (Directory.Exists(p))
                            Directory.Delete(p, true);
                        else if (File.Exists(p + ".zip"))
                            File.Delete(p + ".zip");
                    }

                    lock (listLock)
                    {
                        BackupQueue.Remove(item.Key);
                        BackupQueue.Add(AddOccurence(item.Value, item.Key), item.Value);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        });


        int index;
        while (true)
        {
            Console.Clear();

            lock (listLock)
                foreach (var item in BackupQueue)
                {
                    index = Array.IndexOf(Config.Querries, item.Value);

                    Console.WriteLine($"""
                        Next backup of: {item.Value.FolderToBackup} At: {item.Key} In: {(item.Key - DateTime.Now):hh\:mm\:ss}
                        Last: {dats[index].date} {SizeSuffix(dats[index].Size)} {dats[index].error}
                        """);
                }


            Thread.Sleep(5000);
        }

    }

    static void QueueQuerry(BackupQuerry querry)
    {
        var date = querry.date.Split(':');

        DateTime t = DateTime.Today.AddHours(int.Parse(date[0])).AddMinutes(int.Parse(date[1]));

        while (DateTime.Now > t)
            t = AddOccurence(querry, t);

        BackupQueue.Add(t, querry);
    }

    static DateTime AddOccurence(BackupQuerry querry, DateTime date)
    {
        switch (querry.Occurences)
        {
            case BackupOccurences.Hourly:
                return date.AddHours(querry.OccurenceFactor);

            case BackupOccurences.Dayly:
                return date.AddDays(querry.OccurenceFactor);

            case BackupOccurences.Monthly:
                return date.AddDays(querry.OccurenceFactor * 30.437);
        }

        throw new InvalidDataException($"Occurence: {querry.Occurences} is not valid");
    }

    static void MakeBackup(string from, string to, in Queue<string> Backups, string[]? ignore, ref BackupData dats, bool Compressed = false)
    {
        dats.error = "";

        if (!Directory.Exists(from))
        {
            dats.error = $"{from} does not exist ({DateTime.Now})";
            return;
        }

        if (!Directory.Exists(to))
            Directory.CreateDirectory(to);

        if (ignore is null)
            ignore = Array.Empty<string>();

        IEnumerable<string> ignoredFolders = ignore.Where(x => Directory.Exists(from + x)).Select(x => x.EndsWith('/') ? x : x + '/');
        IEnumerable<string> ignoredFiles = ignore.Where(x => File.Exists(from + x));

        IEnumerable<string> files = Directory.GetFiles(from, "*", SearchOption.AllDirectories)
            .Select(x => x.Replace('\\', '/'))
            .Where(x => File.Exists(x) && !ignoredFolders.Any(y => x.Substring(from.Length).Contains(y)) && !ignoredFiles.Contains(x.Split('/').Last()));
        
        dats.Size = 0;
        int fileCount = 0;

        foreach (string s in files)
        {
            fileCount++;
            dats.Size += new FileInfo(s).Length;
        }

        if (!Directory.Exists(to))
            Directory.CreateDirectory(to);

        DateTime d = DateTime.Now;
        string date = $"{d.Year} {d.Month} {d.Day} {d.Hour}H{d.Minute}";
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
            date = $"{d.Year} {d.Month} {d.Day} {d.Hour}H{d.Minute}_{++i}";
            goto oupsi;
        }

        int last = 0;
        int done = 0;
        int errors = 0;

        if (Compressed)
        {
            using ZipArchive zip = ZipFile.Open(to + date + ".zip", ZipArchiveMode.Create);
            
            foreach (string f in files)
            {
                int a = (int)(DateTime.Now - d).TotalSeconds;

                if (a > last)
                {
                    last = a;
                    TitleWrite($"Transfer: {done}/{fileCount} {done / a}/s {(fileCount - done) / (done / a)}s");
                }

                try
                {
                    zip.CreateEntryFromFile(f, f.Substring(from.Length));
                }
                catch (Exception)
                {
                    errors++;
                }

                done++;
            }
        }
        else
        {
            foreach (string f in files)
            {
                int a = (int)(DateTime.Now - d).TotalSeconds;

                if (a > last)
                {
                    last = a;
                    TitleWrite($"Transfer: {done}/{fileCount} {done / a}/s {(fileCount - done) / (done / a)}s");
                }

                try
                {
                    string fpath = f.Substring(from.Length);
                    string fpath2 = fpath.Substring(0, fpath.Length - Path.GetFileName(f).Length);

                    if (!Directory.Exists(to + date + "/" + fpath2))
                        Directory.CreateDirectory(to + date + "/" + fpath2);

                    File.Copy(f, to + date + "/" + fpath);

                }
                catch (Exception)
                {
                    errors++;
                }

                done++;
            }
        }


        dats.error = "errors: " + errors.ToString();
    }

}
