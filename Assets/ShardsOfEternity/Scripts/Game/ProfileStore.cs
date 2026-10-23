using System;
using System.IO;
using ShardsOfEternity.Core;
using UnityEngine;

namespace ShardsOfEternity.Game
{
    /// <summary>
    /// Reads and writes the player's <see cref="Profile"/> as profile.json in Application.persistentDataPath.
    /// A save that can't be read is set aside as profile.damaged.json and the game starts a fresh profile.
    /// </summary>
    public static class ProfileStore
    {
        /// <summary>Where the save lives. Tests point this at a temp folder so they never touch the real save.</summary>
        public static string Folder;

        public static string FilePath => Path.Combine(Folder ?? Application.persistentDataPath, "profile.json");

        public static Profile Load()
        {
            string path = FilePath;
            if (!File.Exists(path)) return new Profile();
            try
            {
                return Profile.FromJson(File.ReadAllText(path));
            }
            catch (Exception e) when (e is FormatException || e is IOException)
            {
                string aside = Path.ChangeExtension(path, ".damaged.json");
                Debug.LogWarning($"Shards of Eternity: couldn't read the save at {path} ({e.Message}). Starting a new profile; the old file is kept as {aside}.");
                try
                {
                    File.Copy(path, aside, true);
                }
                catch (IOException) { }
                return new Profile();
            }
        }

        /// <summary>Writes to a temp file first and then swaps it in, so a crash mid-write can't corrupt the save.</summary>
        public static void Save(Profile profile)
        {
            if (profile == null) return;
            string path = FilePath;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + ".tmp";
                File.WriteAllText(temp, profile.ToJson());
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"Shards of Eternity: couldn't save progress to {path}: {e.Message}");
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (IOException e)
            {
                Debug.LogWarning($"Shards of Eternity: couldn't delete the save at {FilePath}: {e.Message}");
            }
        }
    }
}
