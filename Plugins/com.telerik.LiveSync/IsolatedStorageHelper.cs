using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace Telerik.BlackDragon.LiveSync
{
	internal static class IsolatedStorageHelper
	{
		private static readonly IsolatedStorageSettings settings = IsolatedStorageSettings.ApplicationSettings;

		public static void DeleteDirectoryRecursively(this IsolatedStorageFile store, string directoryName)
		{
			if (string.IsNullOrEmpty(directoryName) || !store.DirectoryExists(directoryName))
			{
				return;
			}

			var pattern = Path.Combine(directoryName, "*");

			var files = store.GetFileNames(pattern);
			foreach (var file in files)
			{
				store.DeleteFile(Path.Combine(directoryName, file));
			}

			var directories = store.GetDirectoryNames(pattern);
			foreach (var directory in directories)
			{
				store.DeleteDirectoryRecursively(Path.Combine(directoryName, directory));
			}
			store.DeleteDirectory(directoryName);
		}

		public static void EnsureDirectoryExists(this IsolatedStorageFile store, string fileName)
		{
			store.CreateDirectory(Path.GetDirectoryName(fileName));
		}

		public static void StoreSetting<TValue>(string settingName, TValue value)
		{
			if (settings.Contains(settingName))
			{
				settings[settingName] = value;
			}
			else
			{
				settings.Add(settingName, value);
			}

			settings.Save();
		}

		public static bool TryGetSetting<TValue>(string settingName, out TValue value)
		{ 
			return settings.TryGetValue(settingName, out value);
		}

		public static async Task CopyFilesFromXap(this IsolatedStorageFile store, string sourceDirectory, string targetDirectory)
		{
			var files = IsolatedStorageHelper.GetFilesInXap(sourceDirectory);
			foreach (var filePath in files)
			{
				var file = await Package.Current.InstalledLocation.GetFileAsync(filePath);
				using (var fileStream = await file.OpenSequentialReadAsync())
				{
					var targetName = IsolatedStorageHelper.ReplaceFirst(filePath, sourceDirectory, targetDirectory);
					store.EnsureDirectoryExists(targetName);
					using (var targetStream = new IsolatedStorageFileStream(targetName, FileMode.CreateNew, store))
					using (var readableStream = fileStream.AsStreamForRead())
					{
						await readableStream.CopyToAsync(targetStream);
					}
				}
			}
		}

		private static IEnumerable<string> GetFilesInXap(string path)
		{
			return Directory.GetDirectories(path).SelectMany(GetFilesInXap).Concat(Directory.GetFiles(path));
		}

		private static string ReplaceFirst(string text, string search, string replace)
		{
			var pos = text.IndexOf(search);
			if (pos < 0)
			{
				return text;
			}

			return string.Join(string.Empty, text.Substring(0, pos), replace, text.Substring(pos + search.Length));
		}
	}
}