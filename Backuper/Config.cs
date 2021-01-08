using SharpYaml.Serialization;
using System;
using System.IO;

namespace Backuper
{
    enum BackupOccurences
    {
        Hourly,
        Dayly,
        Monthly
    }
    class BackupQuerry
    {
        public BackupOccurences Occurences = BackupOccurences.Dayly;
        public float OccurenceFactor = 1;
        public int BackupCountToKeep = 3;
        public string FolderToBackup = "./ToBackup";
        public string BackupFolder = "./Backups";
        public string[] Ignore = null;
        public string date = "6:00";
        public bool Compressed = false;

        public override string ToString()
        {
            string res = "";

            res += $"Occurences: {Occurences}\n";
            res += $"OccurenceFactor: {OccurenceFactor}\n";
            res += $"BackupCountToKeep: {BackupCountToKeep}\n";
            res += $"FolderToSave: {FolderToBackup}\n";
            res += $"BackupFolder: {BackupFolder}\n";
            res += $"Compressed: {Compressed}\n";
            res += $"StartingDate" +
                $": {date}\n";
            res += $"Ingore:\n";

            foreach (string s in Ignore)
                res += $" {s}\n";

            return res;
        }

    }

    class Config
    {
        public BackupQuerry[] Querries;

        const string BaseConfig = @"#
#
#  ____             _                             _____             __ _       
# |  _ \           | |                           / ____|           / _(_)      
# | |_) | __ _  ___| | ___   _ _ __   ___ _ __  | |     ___  _ __ | |_ _  __ _ 
# |  _ < / _` |/ __| |/ / | | | '_ \ / _ \ '__| | |    / _ \| '_ \|  _| |/ _` |
# | |_) | (_| | (__|   <| |_| | |_) |  __/ |    | |___| (_) | | | | | | | (_| |
# |____/ \__,_|\___|_|\_\\__,_| .__/ \___|_|     \_____\___/|_| |_|_| |_|\__, |
#                             | |                                         __/ |
#                             |_|                                        |___/    
#
#This is the Backuper config file, feel free to edit it as you want
#You can use # to write comments in the config file

#Each Backup querry is an individual backup request, and will be independant form other querry, you can add as many as you want
Querries:

#Example: this will do a backup every two days at 16:42, it will keep last 3 Backups of /root/42

 - #Enum: Hourly,Dayly,Monthly
   Occurences:         Dayly

   #Multiplier for the above enum
   OccurenceFactor:    2.0

   #Starting date
   date:               16:42

   #Number of Backup to keep
   BackupCountToKeep:  3

   #Folder or file to backup
   FolderToBackup:     /root/42

   #Destination Folder of the Backups
   BackupFolder:       /root/Backups

   #Enable Zip Compression
   Compressed: true

   #Files or folders to ignore, all path are local path from the base folder
   Ignore:

    #exclude folder 42 from the backup
    - 42/
    #exclude file A_file.txt from the backup
    - A_file.txt

";

        public Config()
        {

        }

        public void Init()
        {
            if (!File.Exists(Backuper.CurrentFolder + "Config.yml"))
            {
                File.WriteAllText(Backuper.CurrentFolder + "Config.yml", BaseConfig);

                Console.WriteLine($"Config File was not found at {Backuper.CurrentFolder + "Config.yml"}, Created a new one\nPress enter to continue");


                Console.ReadLine();

            }

            string config = File.ReadAllText(Backuper.CurrentFolder + "Config.yml");

            try
            {
                Serializer s = new Serializer();
                s.DeserializeInto<Config>(config, this);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.WriteLine($"Config File is corrupted at {Backuper.CurrentFolder + "Config.yml"}\nPress enter to create a default one");

                Console.ReadLine();

                File.WriteAllText(Backuper.CurrentFolder + "Config.yml", BaseConfig);



            }


            Console.WriteLine("Successfully loaded Config file: \n" + config);
        }

    }
}
